using UnityEngine;

namespace KinectEx
{
    [RequireComponent(typeof(CutoutBodyByDepth))]
    [RequireComponent(typeof(BoneMapper))]
    public class BodyCutoutViewController : MonoBehaviour
    {
        [Header("映像反転設定")] 
        [Tooltip("左右反転"), SerializeField]
        private bool mirror = false;
        [Tooltip("上下反転"),SerializeField]
        private bool flip = false;

        [Header("人物認識範囲設定")] 
        [Range(0.5f, 8.0f)]
        [Tooltip("表示する最大距離（メートル）"), SerializeField]
        private float maxDistance = 8.0f;

        [Tooltip("true: 人の輪郭に合わせて切り抜き \nfalse: Bone検出範囲を表示"), SerializeField]
        private bool visualizeCaptureDistance = false;

        [Header("切り抜き精度設定")]
        [Range(1, 8)]
        [Tooltip("Depthデータのダウンサンプリング値　\n値が小さいほど高解像度"), SerializeField] 
        private int downsample = 1;
    
        [Header("Bone表示設定")]
        [Tooltip("ボーンの表示/非表示"), SerializeField]
        private bool bonesVisible = true;
        [Tooltip("Bone位置の微調整"), SerializeField]
        private Vector2 bonePosOffset = Vector2.zero;
        [Tooltip("ボーン表示用マテリアル"), SerializeField]
        private Material boneMaterial;
        [Tooltip("関節キューブの大きさ"), SerializeField]
        private float jointBaseSize = 0.3f;
        [Tooltip("ライン幅"), SerializeField] 
        private float lineBaseWidth = 0.05f;
    
        private CutoutBodyByDepth _cutoutBodyByDepthSourceView;
        private BoneMapper _bodyMappingView;
    
        private bool _initialized = false;

        private void Awake()
        {
            _cutoutBodyByDepthSourceView = GetComponent<CutoutBodyByDepth>();
            _bodyMappingView = GetComponent<BoneMapper>();
            if (_cutoutBodyByDepthSourceView == null) Debug.LogError("DepthSourceTransparentView component not found!");
            if (_bodyMappingView == null) Debug.LogError("BodySourceMappingView component not found!");

            ApplySettingsToComponents();

            _cutoutBodyByDepthSourceView.Initialize();
            _bodyMappingView.Initialize();
        
            _initialized = true;
        }

        /// <summary>
        /// 各コンポーネントに設定を適用
        /// パラメーター変更時はこのメソッドを呼び出すこと
        /// </summary>
        public void ApplySettingsToComponents()
        {
            _cutoutBodyByDepthSourceView.ApplySettings(mirror, flip, maxDistance, visualizeCaptureDistance, downsample);
            _bodyMappingView.ApplySettings(mirror, flip, maxDistance, boneMaterial,
                jointBaseSize, lineBaseWidth, bonePosOffset, bonesVisible);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !_initialized) return;

            // OnValidateを同一フレームで実行するとアラートが出る場合があるため、
            // EditorApplication.delayCallで次のフレームに遅延実行
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && _initialized)
                {
                    ApplySettingsToComponents();
                }
            };
        }
#endif
    }
}