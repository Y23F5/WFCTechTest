using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WFCTechTest.Rendering {
    public sealed class SceneDepthCaptureFeature : ScriptableRendererFeature {
        public const string PackedDepthGlobalTexture = "_CapturedPackedDepthTexture";

        private const string PackedDepthAssetPath =
            "Assets/_Project/Rendering/RenderTextures/PackedDepth.renderTexture";

        [SerializeField] private string camera1Name = "Camera1";
        [SerializeField] private string camera2Name = "Camera2";
        [SerializeField] private string camera3Name = "Camera3";
        [SerializeField] private string camera4Name = "Camera4";

        [SerializeField] private Shader depthCaptureShader;
        [SerializeField] private Shader combineShader;

        [SerializeField] private bool debugMode        = false;
        [SerializeField] private int  urpRendererIndex = 0;

        private Material                _captureMat;
        private Material                _combineMat;
        private SceneDepthCapturePass[] _passes = new SceneDepthCapturePass[4];

        private Camera[]        _hiddenCameras  = new Camera[4];
        private RenderTexture[] _hiddenColorRTs = new RenderTexture[4];
        private RenderTexture[] _depthRTs       = new RenderTexture[4];

        private RenderTexture _packedRT;
        private RenderTexture _runtimePackedRT;

        // ── Unity 生命周期 ────────────────────────────────────────────────────

        public override void Create() {
            var capShader  = depthCaptureShader != null
                ? depthCaptureShader : Shader.Find("Hidden/SceneDepthCapture");
            var combShader = combineShader != null
                ? combineShader : Shader.Find("Hidden/DepthCombine");

            if (capShader  != null) _captureMat = CoreUtils.CreateEngineMaterial(capShader);
            if (combShader != null) _combineMat = CoreUtils.CreateEngineMaterial(combShader);

            for (int i = 0; i < 4; i++)
                _passes[i] = new SceneDepthCapturePass($"SceneDepthCapture.Ch{i}");

            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
            RenderPipelineManager.endFrameRendering   += OnEndFrameRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (_captureMat == null) return;
            var cam = renderingData.cameraData.camera;
            if (!ShouldExecute(renderingData.cameraData)) return;

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
            _packedRT = null;
        }

        // ── 事件回调 ──────────────────────────────────────────────────────────

        private void OnBeginFrameRendering(ScriptableRenderContext ctx, Camera[] _) {
            if (!Application.isPlaying) return;

            // Camera.allCameras 由 URP 管理，每帧只返回当前渲染的所有 Game 相机。
            // 用名字过滤排除我们自己的隐藏相机（_DepthCap_ 前缀）。
            // 注意：overlay 相机不会出现在 Camera.allCameras 里
            // （Camera.allCameras 只包含 cameraType == Game 的相机，
            // overlay 相机的 cameraType 依然是 Game 但它们属于 base 相机的 stack，
            // URP 内部处理，不在 allCameras 列表中暴露）。
            var all = Camera.allCameras;
            for (int i = 0; i < all.Length; i++) {
                var c = all[i];
                if (c == null) continue;
                // 跳过我们自己创建的隐藏相机
                if (c.name.StartsWith("_DepthCap_")) continue;

                int ch = MatchCameraName(c.name);
                if (ch < 0) continue;

                if (_hiddenCameras[ch] == null)
                    CreateHiddenCamera(ch, c);
                SyncCamera(c, _hiddenCameras[ch]);
            }
        }

        private void OnEndFrameRendering(ScriptableRenderContext ctx, Camera[] _) {
            if (!Application.isPlaying) return;
            if (_combineMat == null) return;

            bool anyReady = false;
            for (int i = 0; i < 4; i++) {
                if (_depthRTs[i] != null && _depthRTs[i].IsCreated()) {
                    _combineMat.SetTexture($"_Depth{i}", _depthRTs[i]);
                    anyReady = true;
                }
            }
            if (!anyReady) return;

            var rt = GetPackedRT();
            if (rt == null) return;

            Graphics.Blit(null, rt, _combineMat);
            Shader.SetGlobalTexture(PackedDepthGlobalTexture, rt);
        }

        // ── 隐藏相机管理 ──────────────────────────────────────────────────────

        private void CreateHiddenCamera(int i, Camera real) {
            _hiddenColorRTs[i] = new RenderTexture(
                Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24) {
                hideFlags = HideFlags.DontSave,
                name      = $"_HiddenColor{i}"
            };

            var go  = new GameObject($"_DepthCap_{real.name}") { hideFlags = HideFlags.DontSave };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture   = _hiddenColorRTs[i];
            cam.cullingMask     = real.cullingMask;
            cam.enabled         = true;
            SyncCamera(real, cam);

            var data = cam.GetUniversalAdditionalCameraData();
            data.SetRenderer(urpRendererIndex);
            data.requiresDepthTexture = true;

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

        // ── 输出 RT ───────────────────────────────────────────────────────────

        private RenderTexture GetPackedRT() {
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);

#if UNITY_EDITOR
            if (_packedRT == null)
                _packedRT = AssetDatabase.LoadAssetAtPath<RenderTexture>(PackedDepthAssetPath);
#endif

            if (_packedRT == null) {
                if (_runtimePackedRT == null ||
                    _runtimePackedRT.width  != w ||
                    _runtimePackedRT.height != h) {
                    if (_runtimePackedRT != null) {
                        _runtimePackedRT.Release();
                        CoreUtils.Destroy(_runtimePackedRT);
                    }
                    _runtimePackedRT = new RenderTexture(w, h, 0,
                        GraphicsFormat.R32G32B32A32_SFloat) {
                        name       = "PackedDepth_Runtime",
                        filterMode = FilterMode.Point,
                        wrapMode   = TextureWrapMode.Clamp,
                        hideFlags  = HideFlags.DontSave
                    };
                    _runtimePackedRT.Create();
                }
                _packedRT = _runtimePackedRT;
            }

            if (_packedRT.width != w || _packedRT.height != h || !_packedRT.IsCreated()) {
                _packedRT.Release();
                _packedRT.width  = w;
                _packedRT.height = h;
                _packedRT.Create();
            }
            return _packedRT;
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

        private int MatchCameraName(string name) {
            if (name == Coerce(camera1Name, "Camera1")) return 0;
            if (name == Coerce(camera2Name, "Camera2")) return 1;
            if (name == Coerce(camera3Name, "Camera3")) return 2;
            if (name == Coerce(camera4Name, "Camera4")) return 3;
            return -1;
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
