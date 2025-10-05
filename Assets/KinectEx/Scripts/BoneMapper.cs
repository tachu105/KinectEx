using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kinect = Windows.Kinect;

namespace KinectEx
{
    [RequireComponent(typeof(MeshFilter))]
    public class BoneMapper : MonoBehaviour
    {
        // 外部設定から受け取る値
        private Material _boneMaterial;
        private float _jointBaseSize = 0.3f;
        private float _lineBaseWidth = 0.05f;
        private Vector2 _bonePosOffset = Vector2.zero;
        private bool _bonesVisible = true;
        private bool _mirror = false;
        private bool _flip = false;
        private float _maxDistance = 3.0f;

        // Kinect内部
        private Kinect.KinectSensor _sensor;
        private Kinect.CoordinateMapper _mapper;
        private Kinect.BodyFrameReader _bodyReader;
        private Kinect.Body[] _bodyData;

        // Body管理用Dictionary
        private readonly Dictionary<ulong, GameObject> _bodies = new();
        private readonly Dictionary<ulong, Dictionary<Kinect.JointType, JointComponents>> _jointComponents = new();

        // 共有マテリアル（ライン/キューブで共用）
        private Material _boneMatInstance;

        // DepthSpace 解像度（v2 既定）
        private int _depthW = 512, _depthH = 424;

        // 平面ローカルAABB（sharedMesh から一度だけ取得／差し替わり時に再取得）
        private MeshFilter _meshFilter;
        private Mesh _cachedMesh;
        private float _minX, _maxX, _minY, _maxY;
        private bool _planeReady;
    
        // lineを平面(Transparent=3000)より後に描画
        private const int LINE_RENDER_QUEUE = 3501;

        // 変更検知用
        private float _prevJointSize, _prevLineWidth;
        private int _prevRenderQueue;
        private bool _prevVisible;

        // 骨の接続マップ
        private static readonly Dictionary<Kinect.JointType, Kinect.JointType> BoneMap = new()
        {
            { Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft },
            { Kinect.JointType.AnkleLeft, Kinect.JointType.KneeLeft },
            { Kinect.JointType.KneeLeft, Kinect.JointType.HipLeft },
            { Kinect.JointType.HipLeft, Kinect.JointType.SpineBase },
            { Kinect.JointType.FootRight, Kinect.JointType.AnkleRight },
            { Kinect.JointType.AnkleRight, Kinect.JointType.KneeRight },
            { Kinect.JointType.KneeRight, Kinect.JointType.HipRight },
            { Kinect.JointType.HipRight, Kinect.JointType.SpineBase },
            { Kinect.JointType.HandTipLeft, Kinect.JointType.HandLeft },
            { Kinect.JointType.ThumbLeft, Kinect.JointType.HandLeft },
            { Kinect.JointType.HandLeft, Kinect.JointType.WristLeft },
            { Kinect.JointType.WristLeft, Kinect.JointType.ElbowLeft },
            { Kinect.JointType.ElbowLeft, Kinect.JointType.ShoulderLeft },
            { Kinect.JointType.ShoulderLeft, Kinect.JointType.SpineShoulder },
            { Kinect.JointType.HandTipRight, Kinect.JointType.HandRight },
            { Kinect.JointType.ThumbRight, Kinect.JointType.HandRight },
            { Kinect.JointType.HandRight, Kinect.JointType.WristRight },
            { Kinect.JointType.WristRight, Kinect.JointType.ElbowRight },
            { Kinect.JointType.ElbowRight, Kinect.JointType.ShoulderRight },
            { Kinect.JointType.ShoulderRight, Kinect.JointType.SpineShoulder },
            { Kinect.JointType.SpineBase, Kinect.JointType.SpineMid },
            { Kinect.JointType.SpineMid, Kinect.JointType.SpineShoulder },
            { Kinect.JointType.SpineShoulder, Kinect.JointType.Neck },
            { Kinect.JointType.Neck, Kinect.JointType.Head },
        };

        /// <summary>
        /// ジョイント関連コンポーネントをまとめた構造体
        /// </summary>
        private struct JointComponents
        {
            public Transform Transform;
            public LineRenderer LineRenderer;
            public MeshRenderer MeshRenderer;
            public BoxCollider2D Collider;
        }

        public void Initialize()
        {
            _sensor = Kinect.KinectSensor.GetDefault();
            if (_sensor == null)
            {
                Debug.LogError("[BodySourceMappingView] KinectSensor not found.");
                enabled = false;
                return;
            }

            _mapper = _sensor.CoordinateMapper;
            _bodyReader = _sensor.BodyFrameSource.OpenReader();

            // Depth解像度を取得
            var depthFrameDesc = _sensor.DepthFrameSource.FrameDescription;
            if (depthFrameDesc != null)
            {
                _depthW = depthFrameDesc.Width;
                _depthH = depthFrameDesc.Height;
            }

            // マテリアルインスタンス作成
            _boneMatInstance = _boneMaterial != null 
                ? new Material(_boneMaterial) 
                : new Material(Shader.Find("Unlit/Color"));
            _boneMatInstance.renderQueue = LINE_RENDER_QUEUE;

            // MeshFilterをキャッシュ
            _meshFilter = GetComponent<MeshFilter>();
            CachePlaneRect(force: true);

            // 変更検知用初期化
            _prevJointSize = _jointBaseSize;
            _prevLineWidth = _lineBaseWidth;
            _prevRenderQueue = LINE_RENDER_QUEUE;
            _prevVisible = _bonesVisible;

            if (!_sensor.IsOpen)
            {
                _sensor.Open();
            }
        }

        void Update()
        {
            UpdateBodyData();
        
            CachePlaneRect(force: false);
            if (!_planeReady) return;

            ApplySettingsIfChanged();

            if (_bodyData == null) return;

            UpdateTrackedBodies();
        }

        /// <summary>
        /// BodyFrameReaderから最新のBodyデータを取得
        /// </summary>
        private void UpdateBodyData()
        {
            if (_bodyReader == null) return;

            var frame = _bodyReader.AcquireLatestFrame();
            if (frame == null) return;

            _bodyData ??= new Kinect.Body[_sensor.BodyFrameSource.BodyCount];
            frame.GetAndRefreshBodyData(_bodyData);
            frame.Dispose();
        }

        /// <summary>
        /// トラッキング中のBodyを更新し、ロストしたBodyを削除
        /// </summary>
        private void UpdateTrackedBodies()
        {
            // トラッキング中のIDを収集
            var trackedIds = new HashSet<ulong>(
                _bodyData.Where(b => b != null && b.IsTracked)
                    .Select(b => b.TrackingId)
            );

            // ロストしたBodyを削除
            RemoveUntrackedBodies(trackedIds);

            // トラッキング中のBodyを更新
            foreach (var body in _bodyData)
            {
                if (body == null || !body.IsTracked) continue;

                if (!_bodies.ContainsKey(body.TrackingId))
                {
                    _bodies[body.TrackingId] = CreateBodyObject(body.TrackingId);
                }

                RefreshBodyJoints(body);
            }
        }

        /// <summary>
        /// トラッキングされていないBodyオブジェクトを削除
        /// </summary>
        private void RemoveUntrackedBodies(HashSet<ulong> trackedIds)
        {
            var untrackedIds = _bodies.Keys.Where(id => !trackedIds.Contains(id)).ToList();
        
            foreach (var id in untrackedIds)
            {
                if (_bodies.TryGetValue(id, out var bodyObj))
                {
                    Destroy(bodyObj);
                }
                _bodies.Remove(id);
                _jointComponents.Remove(id);
            }
        }

        void OnDestroy()
        {
            _bodyReader?.Dispose();
        
            if (_sensor != null && _sensor.IsOpen)
            {
                _sensor.Close();
            }
        }

        /// <summary>
        /// ボーンの表示/非表示を切り替え
        /// </summary>
        public void SetBonesVisible(bool visible)
        {
            _bonesVisible = visible;
            ApplySettingsIfChanged();
        }

        /// <summary>
        /// 平面メッシュのAABBをキャッシュ（座標変換に使用）
        /// </summary>
        private void CachePlaneRect(bool force)
        {
            var mesh = _meshFilter != null ? _meshFilter.sharedMesh : null;
            if (!force && mesh == _cachedMesh) return;
        
            _cachedMesh = mesh;

            if (mesh == null || mesh.vertexCount == 0)
            {
                _planeReady = false;
                return;
            }

            // メッシュのAABBを計算
            var vertices = mesh.vertices;
            _minX = vertices.Min(v => v.x);
            _maxX = vertices.Max(v => v.x);
            _minY = vertices.Min(v => v.y);
            _maxY = vertices.Max(v => v.y);
            _planeReady = true;
        }

        /// <summary>
        /// 設定値が変更された場合、既存のすべてのBodyに反映
        /// </summary>
        private void ApplySettingsIfChanged()
        {
            bool sizeDirty = !Mathf.Approximately(_prevJointSize, _jointBaseSize);
            bool widthDirty = !Mathf.Approximately(_prevLineWidth, _lineBaseWidth);
            bool rqDirty = _prevRenderQueue != LINE_RENDER_QUEUE;
            bool visDirty = _prevVisible != _bonesVisible;

            if (!(sizeDirty || widthDirty || rqDirty || visDirty)) return;

            if (rqDirty) _boneMatInstance.renderQueue = LINE_RENDER_QUEUE;

            // すべてのBodyに設定を適用
            foreach (var jointDict in _jointComponents.Values)
            {
                ApplySettingsToJoints(jointDict, sizeDirty, widthDirty, rqDirty, visDirty);
            }

            // 前回値を更新
            _prevJointSize = _jointBaseSize;
            _prevLineWidth = _lineBaseWidth;
            _prevRenderQueue = LINE_RENDER_QUEUE;
            _prevVisible = _bonesVisible;
        }

        /// <summary>
        /// 個別のジョイント群に設定を適用
        /// </summary>
        private void ApplySettingsToJoints(Dictionary<Kinect.JointType, JointComponents> joints, 
            bool sizeDirty, bool widthDirty, bool rqDirty, bool visDirty)
        {
            foreach (var comp in joints.Values)
            {
                if (visDirty)
                {
                    comp.MeshRenderer.enabled = _bonesVisible;
                    comp.LineRenderer.enabled = _bonesVisible;
                }

                if (sizeDirty)
                {
                    comp.Transform.localScale = Vector3.one * _jointBaseSize;
                }

                if (widthDirty)
                {
                    comp.LineRenderer.startWidth = _lineBaseWidth;
                    comp.LineRenderer.endWidth = _lineBaseWidth;
                }

                if (rqDirty && comp.LineRenderer.material != null)
                {
                    comp.LineRenderer.material.renderQueue = LINE_RENDER_QUEUE;
                }
            }
        }

        /// <summary>
        /// 指定したIDの新しいBodyオブジェクトを作成
        /// </summary>
        private GameObject CreateBodyObject(ulong id)
        {
            var root = new GameObject($"BodyMapped:{id}");
            _jointComponents[id] = new Dictionary<Kinect.JointType, JointComponents>();

            for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
            {
                var jointObj = CreateJointGameObject(jt, root.transform);
                _jointComponents[id][jt] = ExtractJointComponents(jointObj);
            }

            return root;
        }

        /// <summary>
        /// 単一のジョイント用GameObjectを作成
        /// </summary>
        private GameObject CreateJointGameObject(Kinect.JointType jointType, Transform parent)
        {
            var jointObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jointObj.name = jointType.ToString();
            jointObj.transform.SetParent(parent, false);
            jointObj.transform.localScale = Vector3.one * _jointBaseSize;

            // 3D Colliderを削除して2D Colliderに置き換え
            var col3D = jointObj.GetComponent<Collider>();
            if (col3D) DestroyImmediate(col3D);
            jointObj.AddComponent<BoxCollider2D>().isTrigger = true;

            // MeshRendererの設定
            var mr = jointObj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _boneMatInstance;
            mr.enabled = _bonesVisible;

            // LineRendererの追加
            var lr = jointObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.material = _boneMatInstance;
            lr.startWidth = _lineBaseWidth;
            lr.endWidth = _lineBaseWidth;
            lr.enabled = _bonesVisible;

            return jointObj;
        }

        /// <summary>
        /// GameObjectからジョイント関連コンポーネントを抽出
        /// </summary>
        private JointComponents ExtractJointComponents(GameObject jointObj)
        {
            return new JointComponents
            {
                Transform = jointObj.transform,
                LineRenderer = jointObj.GetComponent<LineRenderer>(),
                MeshRenderer = jointObj.GetComponent<MeshRenderer>(),
                Collider = jointObj.GetComponent<BoxCollider2D>()
            };
        }

        /// <summary>
        /// Bodyの全ジョイントの位置とボーン（LineRenderer）を更新
        /// </summary>
        private void RefreshBodyJoints(Kinect.Body body)
        {
            ulong id = body.TrackingId;
            var joints = _jointComponents[id];

            for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
            {
                var joint = body.Joints[jt];
                var components = joints[jt];

                // DepthSpaceへの変換
                var depthPoint = _mapper.MapCameraPointToDepthSpace(CreateCameraPoint(joint.Position));

                // 無効なDepth座標または距離外の場合は非表示
                if (!IsValidDepthPoint(depthPoint) || !IsWithinDistance(joint.Position.Z))
                {
                    DisableJointComponents(components);
                    continue;
                }

                // ジョイントの位置を更新
                Vector3 worldPos = CalculateWorldPosition(depthPoint);
                components.Transform.position = worldPos;
            
                // Renderer/Colliderの制御
                components.MeshRenderer.enabled = _bonesVisible;
                components.Collider.enabled = true; // 距離内は常に有効

                // ボーン（LineRenderer）の更新
                UpdateBoneLine(body, jt, components.LineRenderer, worldPos);
            }
        }

        /// <summary>
        /// ボーン（2つのジョイント間のライン）を更新
        /// </summary>
        private void UpdateBoneLine(Kinect.Body body, Kinect.JointType jointType, 
            LineRenderer lineRenderer, Vector3 startWorldPos)
        {
            // 接続先ジョイントがない場合は非表示
            if (!BoneMap.TryGetValue(jointType, out var targetJointType))
            {
                lineRenderer.enabled = false;
                return;
            }

            var targetJoint = body.Joints[targetJointType];
            var targetDepthPoint = _mapper.MapCameraPointToDepthSpace(CreateCameraPoint(targetJoint.Position));

            // 接続先が無効または距離外の場合は非表示
            if (!IsValidDepthPoint(targetDepthPoint) || !IsWithinDistance(targetJoint.Position.Z))
            {
                lineRenderer.enabled = false;
                return;
            }

            // ラインの両端を設定
            Vector3 endWorldPos = CalculateWorldPosition(targetDepthPoint);
            lineRenderer.SetPosition(0, startWorldPos);
            lineRenderer.SetPosition(1, endWorldPos);
            lineRenderer.startColor = GetColorForTrackingState(body.Joints[jointType].TrackingState);
            lineRenderer.endColor = GetColorForTrackingState(targetJoint.TrackingState);
            lineRenderer.enabled = _bonesVisible;
        }

        /// <summary>
        /// ジョイント関連コンポーネントをすべて非表示にする
        /// </summary>
        private void DisableJointComponents(JointComponents components)
        {
            components.LineRenderer.enabled = false;
            components.MeshRenderer.enabled = false;
            components.Collider.enabled = false;
        }

        /// <summary>
        /// Kinectの3D座標からCameraSpacePointを生成
        /// </summary>
        private Kinect.CameraSpacePoint CreateCameraPoint(Kinect.CameraSpacePoint position)
        {
            return new Kinect.CameraSpacePoint
            {
                X = position.X,
                Y = position.Y,
                Z = position.Z
            };
        }

        /// <summary>
        /// DepthSpacePointが有効な範囲内かチェック
        /// </summary>
        private bool IsValidDepthPoint(Kinect.DepthSpacePoint point)
        {
            if (float.IsNaN(point.X) || float.IsNaN(point.Y) || 
                float.IsInfinity(point.X) || float.IsInfinity(point.Y))
            {
                return false;
            }
            return point.X >= 0f && point.X < _depthW && point.Y >= 0f && point.Y < _depthH;
        }

        /// <summary>
        /// Z距離が設定範囲内かチェック
        /// </summary>
        private bool IsWithinDistance(float distanceZ)
        {
            return distanceZ > 0f && distanceZ <= _maxDistance;
        }

        /// <summary>
        /// DepthSpace座標を平面上のワールド座標に変換
        /// </summary>
        private Vector3 CalculateWorldPosition(Kinect.DepthSpacePoint depthPoint)
        {
            // 正規化座標（0-1）
            float u = depthPoint.X / _depthW;
            float v = depthPoint.Y / _depthH;

            // ミラー/フリップ適用
            if (_mirror) u = 1f - u;
            if (_flip) v = 1f - v;

            // 平面ローカル座標
            Vector3 localPos = new Vector3(
                Mathf.Lerp(_minX, _maxX, u) + _bonePosOffset.x,
                Mathf.Lerp(_maxY, _minY, v) + _bonePosOffset.y,
                0f
            );

            return transform.TransformPoint(localPos);
        }

        /// <summary>
        /// トラッキング状態に応じた色を取得
        /// </summary>
        private static Color GetColorForTrackingState(Kinect.TrackingState state)
        {
            return state switch
            {
                Kinect.TrackingState.Tracked => Color.green,
                Kinect.TrackingState.Inferred => Color.red,
                _ => Color.black
            };
        }

        /// <summary>
        /// 外部から設定値を適用
        /// </summary>
        public void ApplySettings(bool mirrorValue, bool flipValue, float maxDistanceValue, 
            Material boneMaterialValue, float jointBaseSizeValue, float lineBaseWidthValue, 
            Vector2 pixelOffsetValue, bool bonesVisibleValue)
        {
            _mirror = mirrorValue;
            _flip = flipValue;
            _maxDistance = maxDistanceValue;
            _boneMaterial = boneMaterialValue;
            _jointBaseSize = jointBaseSizeValue;
            _lineBaseWidth = lineBaseWidthValue;
            _bonePosOffset = pixelOffsetValue;
            _bonesVisible = bonesVisibleValue;

            // マテリアルインスタンスを更新
            if (_boneMaterial != null)
            {
                _boneMatInstance = new Material(_boneMaterial);
                _boneMatInstance.renderQueue = LINE_RENDER_QUEUE;
            }

            // 変更を適用
            if (Application.isPlaying)
            {
                ApplySettingsIfChanged();
            }
        }
    }
}
