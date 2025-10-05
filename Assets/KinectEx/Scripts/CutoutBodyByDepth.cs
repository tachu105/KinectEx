using Windows.Kinect;
using UnityEngine;

namespace KinectEx
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CutoutBodyByDepth : MonoBehaviour
    {
        // 定数
        private const int DEPTH_WIDTH = 512;
        private const int DEPTH_HEIGHT = 424;
        private const int COLOR_WIDTH = 1920;
        private const int COLOR_HEIGHT = 1080;
        private const byte BODY_INDEX_NOT_TRACKED = 255;
        private const int BYTES_PER_PIXEL = 4;
        private const float MM_TO_METERS = 0.001f;

        // 外部設定から受け取る値
        private int _downsample = 1;
        private bool _mirror = false;
        private bool _flip = false;
        private float _maxDistance = 8.0f;
        private bool _visualizeCaptureDistance = false;

        // Kinect
        private KinectSensor _sensor;
        private MultiSourceFrameReader _frameReader;
        private CoordinateMapper _coordinateMapper;

        // フレームバッファ（同一フレームをラッチ）
        private ushort[] _depthData;
        private ColorSpacePoint[] _colorSpaceMap;
        private byte[] _bodyIndexData;
        private byte[] _colorDataBgra;
        private byte[] _colorDataRgba;
        private Texture2D _colorTexture;

        // Mesh（ランタイムは .mesh を更新）
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Mesh _gridMesh;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color32[] _vertexColors;
        private int[] _triangles;
        private int _gridWidth, _gridHeight;

        /// <summary>
        /// 初期化処理
        /// </summary>
        public void Initialize()
        {
            // Kinect
            _sensor = KinectSensor.GetDefault();
            if (_sensor == null)
            {
                Debug.LogError("[DSTV] KinectSensor not found.");
                enabled = false;
                return;
            }

            _coordinateMapper = _sensor.CoordinateMapper;
            _frameReader = _sensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.BodyIndex);

            // Components
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            // ランタイム用メッシュを作成
            _gridMesh = _meshFilter.mesh;
            _gridMesh.name = "DSTV_RuntimeMesh";

            // マテリアルとテクスチャ
            _material = _meshRenderer.sharedMaterial;
            _colorTexture = CreateColorTexture();
            if (_material != null)
            {
                _material.mainTexture = _colorTexture;
                _meshRenderer.sharedMaterial = _material;
            }

            // バッファ初期化
            int depthPixelCount = DEPTH_WIDTH * DEPTH_HEIGHT;
            int colorPixelCount = COLOR_WIDTH * COLOR_HEIGHT;
        
            _depthData = new ushort[depthPixelCount];
            _colorSpaceMap = new ColorSpacePoint[depthPixelCount];
            _bodyIndexData = new byte[depthPixelCount];
            _colorDataBgra = new byte[colorPixelCount * BYTES_PER_PIXEL];
            _colorDataRgba = new byte[colorPixelCount * BYTES_PER_PIXEL];

            BuildGridMesh();

            if (!_sensor.IsOpen) _sensor.Open();
        }
    
        void Update()
        {
            EnsureGridIntegrity();
        
            if (_frameReader == null) return;
            var multiSourceFrame = _frameReader.AcquireLatestFrame();
            if (multiSourceFrame == null) return;

            // Depthフレーム取得
            if (!TryAcquireDepthFrame(multiSourceFrame)) return;
        
            // BodyIndexフレーム取得
            if (!TryAcquireBodyIndexFrame(multiSourceFrame)) return;
        
            // Colorフレーム取得と変換
            if (!TryAcquireAndConvertColorFrame(multiSourceFrame)) return;

            // Depth→Colorマッピング
            _coordinateMapper.MapDepthFrameToColorSpace(_depthData, _colorSpaceMap);

            // メッシュ更新
            UpdateMeshUVAndAlpha();
        }

        /// <summary>
        /// カラー用テクスチャを生成する
        /// </summary>
        private Texture2D CreateColorTexture()
        {
            return new Texture2D(COLOR_WIDTH, COLOR_HEIGHT, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        /// <summary>
        /// 必要なコンポーネントが揃っているか確認し、足りない場合は取得・生成する
        /// </summary>
        private bool EnsureComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
        
            if (_gridMesh == null && _meshFilter != null)
            {
                _gridMesh = _meshFilter.mesh;
                _gridMesh.name = "DSTV_RuntimeMesh";
            }
        
            if (_colorTexture == null)
            {
                _colorTexture = CreateColorTexture();
                if (_material != null) _material.mainTexture = _colorTexture;
            }
        
            if (_material == null && _meshRenderer != null)
            {
                _material = _meshRenderer.sharedMaterial;
            }
        
            if (_meshRenderer != null && _material != null)
            {
                _meshRenderer.sharedMaterial = _material;
            }

            return _meshFilter != null && _meshRenderer != null && _gridMesh != null && _colorTexture != null;
        }

        /// <summary>
        /// グリッドメッシュを構築する
        /// </summary>
        private void BuildGridMesh()
        {
            if (!EnsureComponents())
            {
                Debug.LogWarning("[DSTV] BuildGridMesh aborted (components not ready).");
                return;
            }

            int downsampleValue = Mathf.Max(1, _downsample);
            _gridWidth = Mathf.Max(2, DEPTH_WIDTH / downsampleValue);
            _gridHeight = Mathf.Max(2, DEPTH_HEIGHT / downsampleValue);

            int vertexCount = _gridWidth * _gridHeight;
            int triangleCount = (_gridWidth - 1) * (_gridHeight - 1) * 6;

            _vertices = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _vertexColors = new Color32[vertexCount];
            _triangles = new int[triangleCount];

            // 頂点とUV初期化
            for (int y = 0; y < _gridHeight; y++)
            {
                float normalizedY = (float)y / (_gridHeight - 1);
                float posY = Mathf.Lerp(-0.5f, 0.5f, normalizedY);
            
                for (int x = 0; x < _gridWidth; x++)
                {
                    float normalizedX = (float)x / (_gridWidth - 1);
                    float posX = Mathf.Lerp(-0.5f, 0.5f, normalizedX);

                    int index = y * _gridWidth + x;
                    _vertices[index] = new Vector3(posX, posY, 0f);
                    _uvs[index] = new Vector2(normalizedX, normalizedY);
                    _vertexColors[index] = new Color32(255, 255, 255, 0);
                }
            }

            // 三角形インデックス生成
            int triangleIndex = 0;
            for (int y = 0; y < _gridHeight - 1; y++)
            {
                for (int x = 0; x < _gridWidth - 1; x++)
                {
                    int bottomLeft = y * _gridWidth + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + _gridWidth;
                    int topRight = topLeft + 1;

                    // 第一三角形
                    _triangles[triangleIndex++] = bottomLeft;
                    _triangles[triangleIndex++] = topLeft;
                    _triangles[triangleIndex++] = bottomRight;
                
                    // 第二三角形
                    _triangles[triangleIndex++] = bottomRight;
                    _triangles[triangleIndex++] = topLeft;
                    _triangles[triangleIndex++] = topRight;
                }
            }

            _gridMesh.Clear(false);
            _gridMesh.indexFormat = vertexCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _gridMesh.vertices = _vertices;
            _gridMesh.uv = _uvs;
            _gridMesh.colors32 = _vertexColors;
            _gridMesh.triangles = _triangles;
            _gridMesh.RecalculateBounds();

            if (_material != null && _material.mainTexture != _colorTexture)
            {
                _material.mainTexture = _colorTexture;
            }
        }

        /// <summary>
        /// メッシュの整合性とパラメータの値変化をチェックし、必要に応じてリビルドする
        /// </summary>
        private void EnsureGridIntegrity()
        {
            int downsampleValue = Mathf.Max(1, _downsample);
            int expectedWidth = Mathf.Max(2, DEPTH_WIDTH / downsampleValue);
            int expectedHeight = Mathf.Max(2, DEPTH_HEIGHT / downsampleValue);
            int expectedVertexCount = expectedWidth * expectedHeight;

            bool needsRebuild =
                _gridWidth != expectedWidth ||
                _gridHeight != expectedHeight ||
                _vertices == null || _vertices.Length != expectedVertexCount ||
                _uvs == null || _uvs.Length != expectedVertexCount ||
                _vertexColors == null || _vertexColors.Length != expectedVertexCount ||
                _gridMesh == null || _gridMesh.vertexCount != expectedVertexCount;

            if (needsRebuild)
            {
                Debug.LogWarning($"[DSTV] Rebuilding grid mesh. Current: ({_gridWidth},{_gridHeight}), Expected: ({expectedWidth},{expectedHeight})");
                BuildGridMesh();
            }
        }

        /// <summary>
        /// Depthフレームを取得し、ushort配列にコピーする
        /// </summary>
        private bool TryAcquireDepthFrame(MultiSourceFrame multiSourceFrame)
        {
            var depthFrame = multiSourceFrame.DepthFrameReference?.AcquireFrame();
            if (depthFrame == null) return false;
        
            depthFrame.CopyFrameDataToArray(_depthData);
            depthFrame.Dispose();
            return true;
        }

        private bool TryAcquireBodyIndexFrame(MultiSourceFrame multiSourceFrame)
        {
            var bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference?.AcquireFrame();
            if (bodyIndexFrame == null) return false;
        
            bodyIndexFrame.CopyFrameDataToArray(_bodyIndexData);
            bodyIndexFrame.Dispose();
            return true;
        }

        /// <summary>
        /// Colorフレームを取得し、RGBAフォーマットに変換する
        /// </summary>
        private bool TryAcquireAndConvertColorFrame(MultiSourceFrame multiSourceFrame)
        {
            var colorFrame = multiSourceFrame.ColorFrameReference?.AcquireFrame();
            if (colorFrame == null) return false;

            // BGRAデータ取得
            if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                colorFrame.CopyRawFrameDataToArray(_colorDataBgra);
            else
                colorFrame.CopyConvertedFrameDataToArray(_colorDataBgra, ColorImageFormat.Bgra);
            colorFrame.Dispose();

            // BGRA → RGBA変換
            ConvertBGRAtoRGBA();
            _colorTexture.LoadRawTextureData(_colorDataRgba);
            _colorTexture.Apply(false, false);
        
            return true;
        }

        /// <summary>
        /// BGRAフォーマットのカラー配列をRGBAフォーマットへ変換する
        /// </summary>
        private void ConvertBGRAtoRGBA()
        {
            int pixelCount = COLOR_WIDTH * COLOR_HEIGHT;
            for (int i = 0; i < pixelCount; i++)
            {
                int sourceIndex = i * BYTES_PER_PIXEL;
                int destIndex = i * BYTES_PER_PIXEL;
            
                _colorDataRgba[destIndex + 0] = _colorDataBgra[sourceIndex + 2]; // R
                _colorDataRgba[destIndex + 1] = _colorDataBgra[sourceIndex + 1]; // G
                _colorDataRgba[destIndex + 2] = _colorDataBgra[sourceIndex + 0]; // B
                _colorDataRgba[destIndex + 3] = _colorDataBgra[sourceIndex + 3]; // A
            }
        }

        /// <summary>
        /// メッシュのUV座標と頂点カラー（透明度）を更新する
        /// </summary>
        private void UpdateMeshUVAndAlpha()
        {
            int downsampleStep = Mathf.Max(1, _downsample);
            int vertexIndex = 0;

            for (int y = 0, depthY = 0; y < _gridHeight; y++, depthY += downsampleStep)
            {
                for (int x = 0, depthX = 0; x < _gridWidth; x++, depthX += downsampleStep, vertexIndex++)
                {
                    // 反転処理を適用した座標計算
                    int actualX = _mirror ? (DEPTH_WIDTH - 1 - depthX) : depthX;
                    int actualY = _flip ? depthY : (DEPTH_HEIGHT - 1 - depthY);
                
                    int depthIndex = actualY * DEPTH_WIDTH + actualX;

                    // 透明度の計算
                    _vertexColors[vertexIndex].a = CalculateAlpha(depthIndex);

                    // UV座標の計算
                    UpdateUV(vertexIndex, depthIndex);
                }
            }

            _gridMesh.uv = _uvs;
            _gridMesh.colors32 = _vertexColors;
        }

        /// <summary>
        /// 対象のDepthピクセルの透明度（表示or非表示）を計算する
        /// </summary>
        private byte CalculateAlpha(int depthIndex)
        {
            // 距離チェック
            ushort depthMm = _depthData[depthIndex];
            if (depthMm == 0) return 0;
        
            float depthMeters = depthMm * MM_TO_METERS;
            bool isWithinDistance = depthMeters <= _maxDistance;

            if (!isWithinDistance) return 0;

            // 距離ベース表示モードの場合は距離のみで判定
            if (_visualizeCaptureDistance) return 255;

            // BodyIndexベース表示モードの場合は人物判定も追加
            bool isTrackedBody = _bodyIndexData[depthIndex] != BODY_INDEX_NOT_TRACKED;
            return isTrackedBody ? (byte)255 : (byte)0;
        }

        /// <summary>
        /// 指定した頂点のUV座標を更新する
        /// </summary>
        private void UpdateUV(int vertexIndex, int depthIndex)
        {
            var colorSpacePoint = _colorSpaceMap[depthIndex];
            float u = colorSpacePoint.X / COLOR_WIDTH;
            float v = colorSpacePoint.Y / COLOR_HEIGHT;

            // 無効な値のチェック
            if (!float.IsFinite(u) || !float.IsFinite(v))
            {
                u = v = -1f; // 範囲外へ
            }

            _uvs[vertexIndex].x = u;
            _uvs[vertexIndex].y = v;
        }

        /// <summary>
        /// 外部クラスから注入するパラメータを設定してリフレッシュする処理
        /// </summary>
        public void ApplySettings(bool shouldMirror, bool shouldFlip, float maxDistanceValue, 
            bool visualizeCaptureDistanceSetting, int downsampleValue)
        {
            _mirror = shouldMirror;
            _flip = shouldFlip;
            _maxDistance = maxDistanceValue;
            _visualizeCaptureDistance = visualizeCaptureDistanceSetting;
            _downsample = downsampleValue;
        
            if (Application.isPlaying)
            {
                EnsureGridIntegrity();
            }
        }
    
        void OnDestroy()
        {
            if (_frameReader != null)
            {
                _frameReader.Dispose();
                _frameReader = null;
            }
            if (_sensor != null && _sensor.IsOpen)
            {
                _sensor.Close();
            }
            if (_colorTexture != null)
            {
                Destroy(_colorTexture);
            }
        }
    }
}
