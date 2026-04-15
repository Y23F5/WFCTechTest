using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WFCTechTest.Rendering {
    public sealed class SceneDepthCaptureFeature : ScriptableRendererFeature {
        public const string PackedDepthGlobalTexture = "_CapturedPackedDepthTexture";

        // 与场景相机名对应（有默认值，通常不需要修改）
        [SerializeField] private string camera1Name = "Camera1";
        [SerializeField] private string camera2Name = "Camera2";
        [SerializeField] private string camera3Name = "Camera3";
        [SerializeField] private string camera4Name = "Camera4";

        // Shader 字段可选：留空则自动 Shader.Find 回退
        [SerializeField] private Shader        depthCaptureShader;
        [SerializeField] private Shader        combineShader;

        [SerializeField] private bool          useHalfPrecision = true;
        [SerializeField] private bool          debugMode        = false;

        // 可选：指定输出 RT 资产。留空 = 运行时自动按屏幕尺寸创建。
        [SerializeField] private RenderTexture outputPackedRT;

        // 含 SceneDepthCaptureFeature 的 Renderer 序号（通常为 0）
        [SerializeField] private int           urpRendererIndex = 0;

        private Material               _captureMat;
        private Material               _combineMat;
        private SceneDepthCapturePass[] _passes = new SceneDepthCapturePass[4];

        // 运行时过程产物（全部 HideFlags.DontSave，退出后自动消失）
        private Camera[]        _hiddenCameras  = new Camera[4];
        private RenderTexture[] _hiddenColorRTs = new RenderTexture[4];
        private RenderTexture[] _depthRTs       = new RenderTexture[4];
        private RenderTexture   _runtimePackedRT;

        // ── Unity 生命周期 ────────────────────────────────────────────────────

        public override void Create() {
            var capShader  = depthCaptureShader != null
                ? depthCaptureShader
                : Shader.Find("Hidden/SceneDepthCapture");
            var combShader = combineShader != null
                ? combineShader
                : Shader.Find("Hidden/DepthCombine");

            if (capShader  != null) _captureMat = CoreUtils.CreateEngineMaterial(capShader);
            if (combShader != null) _combineMat = CoreUtils.CreateEngineMaterial(combShader);

            for (int i = 0; i < 4; i++)
                _passes[i] = new SceneDepthCapturePass($"SceneDepthCapture.Ch{i}");

            // 订阅 URP 帧事件（主线程，可直接调 Graphics API）
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
            RenderPipelineManager.endFrameRendering   += OnEndFrameRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (_captureMat == null) return;
            var cam = renderingData.cameraData.camera;
            if (!ShouldExecute(renderingData.cameraData)) return;

            // 只对我们自己创建的隐藏相机入队
            int ch = IndexOfHiddenCamera(cam);
            if (ch < 0) return;

            EnsureDepthRT(ch, renderingData.cameraData);
            if (_depthRTs[ch] == null) return;

            if (debugMode) _captureMat.EnableKeyword("DEBUG_SOLID");
            else           _captureMat.DisableKeyword("DEBUG_SOLID");

            _passes[ch].Setup(_captureMat, _depthRTs[ch]);
            renderer.EnqueuePass(_passes[ch]);
        }

        protected override void Dispose(bool disposing) {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
            RenderPipelineManager.endFrameRendering   -= OnEndFrameRendering;

            CoreUtils.Destroy(_captureMat);
            CoreUtils.Destroy(_combineMat);
            _captureMat = _combineMat = null;

            for (int i = 0; i < 4; i++) DestroyHiddenCamera(i);

            if (_runtimePackedRT != null) {
                _runtimePackedRT.Release();
                CoreUtils.Destroy(_runtimePackedRT);
                _runtimePackedRT = null;
            }
        }

        // ── 事件回调 ──────────────────────────────────────────────────────────

        // 每帧所有相机渲染前：查找真实相机，按需创建/同步隐藏相机
        private void OnBeginFrameRendering(ScriptableRenderContext ctx, Camera[] _) {
            if (!Application.isPlaying) return;  // 只在 Play Mode 创建隐藏相机
            string[] names = {
                Coerce(camera1Name, "Camera1"), Coerce(camera2Name, "Camera2"),
                Coerce(camera3Name, "Camera3"), Coerce(camera4Name, "Camera4")
            };

            var all = Camera.allCameras;   // 所有启用的 Game 相机
            for (int i = 0; i < 4; i++) {
                Camera real = FindCameraByName(all, names[i]);
                if (real == null) {
                    DestroyHiddenCamera(i);
                    continue;
                }
                if (_hiddenCameras[i] == null) CreateHiddenCamera(i, real);
                SyncCamera(real, _hiddenCameras[i]);
            }
        }

        // 每帧所有相机渲染完毕后：合并 4 个 R32F 深度 RT → 1 张 RGBA packed RT
        private void OnEndFrameRendering(ScriptableRenderContext ctx, Camera[] _) {
            if (!Application.isPlaying) return;  // 只在 Play Mode 合并
            if (_combineMat == null) return;

            bool anyReady = false;
            for (int i = 0; i < 4; i++) {
                if (_depthRTs[i] != null && _depthRTs[i].IsCreated()) {
                    _combineMat.SetTexture($"_Depth{i}", _depthRTs[i]);
                    anyReady = true;
                }
            }
            if (!anyReady) return;

            var rt = outputPackedRT ?? EnsureRuntimePackedRT();
            Graphics.Blit(null, rt, _combineMat);
            Shader.SetGlobalTexture(PackedDepthGlobalTexture, rt);
        }

        // ── 隐藏相机管理 ──────────────────────────────────────────────────────

        private void CreateHiddenCamera(int i, Camera real) {
            // 带 depth=24 的颜色 RT，作为隐藏相机的 targetTexture
            // depth buffer 必须存在，URP 才能正确生成 _CameraDepthTexture
            _hiddenColorRTs[i] = new RenderTexture(
                Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24) {
                hideFlags = HideFlags.DontSave,
                name      = $"_HiddenColor{i}"
            };

            var go  = new GameObject($"_DepthCap_{real.name}") { hideFlags = HideFlags.DontSave };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;  // 不渲染天空盒，节省开销
            cam.backgroundColor = Color.black;
            cam.targetTexture   = _hiddenColorRTs[i];
            cam.cullingMask     = real.cullingMask;
            cam.enabled         = true;
            SyncCamera(real, cam);

            var data = cam.GetUniversalAdditionalCameraData();
            data.SetRenderer(urpRendererIndex);
            data.requiresDepthTexture = true;  // 确保 _CameraDepthTexture 生成

            _hiddenCameras[i] = cam;
            if (debugMode) Debug.Log($"[DepthCapture] 创建隐藏相机: {go.name}");
        }

        private void DestroyHiddenCamera(int i) {
            if (_hiddenCameras[i] != null) {
                CoreUtils.Destroy(_hiddenCameras[i].gameObject);
                _hiddenCameras[i] = null;
            }
            if (_hiddenColorRTs[i] != null) {
                _hiddenColorRTs[i].Release();
                CoreUtils.Destroy(_hiddenColorRTs[i]);
                _hiddenColorRTs[i] = null;
            }
        }

        private void EnsureDepthRT(int i, in CameraData cd) {
            var size = GetRTSize(cd);
            if (_depthRTs[i] != null &&
                _depthRTs[i].width  == size.x &&
                _depthRTs[i].height == size.y) return;

            if (_depthRTs[i] != null) {
                _depthRTs[i].Release();
                CoreUtils.Destroy(_depthRTs[i]);
            }

            _depthRTs[i] = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.RFloat) {
                hideFlags  = HideFlags.DontSave,
                name       = $"_DepthRT{i}",
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };
            _depthRTs[i].Create();
        }

        private RenderTexture EnsureRuntimePackedRT() {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (_runtimePackedRT != null &&
                _runtimePackedRT.width  == w &&
                _runtimePackedRT.height == h)
                return _runtimePackedRT;

            if (_runtimePackedRT != null) {
                _runtimePackedRT.Release();
                CoreUtils.Destroy(_runtimePackedRT);
            }

            var fmt = useHalfPrecision
                ? GraphicsFormat.R16G16B16A16_SFloat
                : GraphicsFormat.R32G32B32A32_SFloat;

            _runtimePackedRT = new RenderTexture(w, h, 0, fmt) {
                name        = "PackedSceneDepth_Runtime",
                filterMode  = FilterMode.Point,
                wrapMode    = TextureWrapMode.Clamp,
                hideFlags   = HideFlags.DontSave,
                useMipMap   = false,
                autoGenerateMips = false
            };
            _runtimePackedRT.Create();
            return _runtimePackedRT;
        }

        // ── 工具函数 ──────────────────────────────────────────────────────────

        private static void SyncCamera(Camera src, Camera dst) {
            dst.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            dst.fieldOfView      = src.fieldOfView;
            dst.nearClipPlane    = src.nearClipPlane;
            dst.farClipPlane     = src.farClipPlane;
            dst.orthographic     = src.orthographic;
            dst.orthographicSize = src.orthographicSize;
        }

        private int IndexOfHiddenCamera(Camera cam) {
            for (int i = 0; i < 4; i++)
                if (_hiddenCameras[i] == cam) return i;
            return -1;
        }

        private static Camera FindCameraByName(Camera[] all, string name) {
            foreach (var c in all)
                if (c != null && c.name == name) return c;
            return null;
        }

        private static Vector2Int GetRTSize(in CameraData cd) {
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);
            if (w <= 1) w = Mathf.Max(1, cd.camera.pixelWidth);
            if (h <= 1) h = Mathf.Max(1, cd.camera.pixelHeight);
            return new Vector2Int(w, h);
        }

        private static bool ShouldExecute(in CameraData cd) {
            if (cd.isPreviewCamera) return false;
            if (cd.camera == null)  return false;
            if (cd.camera.cameraType != CameraType.Game) return false;
            if (cd.renderType != CameraRenderType.Base)  return false;
            return true;
        }

        private static string Coerce(string s, string fallback) =>
            string.IsNullOrEmpty(s) ? fallback : s;
    }
}
