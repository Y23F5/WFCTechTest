using System.Collections.Generic;
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

        // 每个 channel 的隐藏 Base 相机 + 其 targetTexture
        private Camera[]        _hiddenCameras  = new Camera[4];
        private RenderTexture[] _hiddenColorRTs = new RenderTexture[4];

        // 每个 channel 的隐藏 Overlay 相机（与真实相机 cameraStack 对应）
        private List<Camera>[] _realOverlayCameras   = new List<Camera>[4];
        private List<Camera>[] _hiddenOverlayCameras = new List<Camera>[4];

        // 每个 channel 的中间深度 RT（R32F）
        private RenderTexture[] _depthRTs = new RenderTexture[4];

        // 最终输出 RT（PackedDepth 资产 或 Build 回退 RT）
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

            for (int i = 0; i < 4; i++) {
                _passes[i]               = new SceneDepthCapturePass($"SceneDepthCapture.Ch{i}");
                _realOverlayCameras[i]   = new List<Camera>();
                _hiddenOverlayCameras[i] = new List<Camera>();
            }

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

            string[] names = {
                Coerce(camera1Name, "Camera1"), Coerce(camera2Name, "Camera2"),
                Coerce(camera3Name, "Camera3"), Coerce(camera4Name, "Camera4")
            };

            var all = Camera.allCameras;
            for (int i = 0; i < 4; i++) {
                Camera real = FindCameraByName(all, names[i]);
                if (real == null) { DestroyHiddenCamera(i); continue; }

                var realStack = real.GetUniversalAdditionalCameraData().cameraStack;

                // 首次创建，或 Overlay 堆栈发生变化时重建
                if (_hiddenCameras[i] == null || OverlayStackChanged(i, realStack)) {
                    DestroyHiddenCamera(i);
                    CreateHiddenCamera(i, real, realStack);
                }

                // 每帧同步 Base 相机变换
                SyncCamera(real, _hiddenCameras[i]);

                // 每帧同步 Overlay 相机变换
                for (int j = 0; j < _realOverlayCameras[i].Count; j++) {
                    var ro = _realOverlayCameras[i][j];
                    var ho = _hiddenOverlayCameras[i][j];
                    if (ro != null && ho != null) SyncCamera(ro, ho);
                }
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

        private void CreateHiddenCamera(int i, Camera real, IList<Camera> realStack) {
            // Base 相机的 targetTexture（需要 depth=24，URP 才会生成 _CameraDepthTexture）
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
            cam.cullingMask     = real.cullingMask;   // 与源相机一致
            cam.enabled         = true;
            SyncCamera(real, cam);

            var baseData = cam.GetUniversalAdditionalCameraData();
            baseData.SetRenderer(urpRendererIndex);
            baseData.requiresDepthTexture = true;

            _hiddenCameras[i] = cam;

            // 为源相机的每个 Overlay 创建对应的隐藏 Overlay 相机
            _realOverlayCameras[i].Clear();
            _hiddenOverlayCameras[i].Clear();

            foreach (var realOverlay in realStack) {
                if (realOverlay == null) continue;

                var ovGo  = new GameObject($"_DepthCap_{realOverlay.name}_OV")
                    { hideFlags = HideFlags.DontSave };
                ovGo.transform.SetParent(go.transform);

                var ovCam = ovGo.AddComponent<Camera>();
                ovCam.cullingMask = realOverlay.cullingMask;   // 与 Overlay 源相机一致
                ovCam.enabled     = true;
                SyncCamera(realOverlay, ovCam);

                var ovData = ovCam.GetUniversalAdditionalCameraData();
                ovData.renderType  = CameraRenderType.Overlay;
                ovData.SetRenderer(urpRendererIndex);

                // 加入隐藏 Base 相机的 stack
                baseData.cameraStack.Add(ovCam);

                _realOverlayCameras[i].Add(realOverlay);
                _hiddenOverlayCameras[i].Add(ovCam);
            }

            if (debugMode) {
                Debug.Log($"[DepthCapture] 创建隐藏相机: {go.name}" +
                          $"  overlays={_hiddenOverlayCameras[i].Count}");
            }
        }

        private void DestroyHiddenCamera(int i) {
            // 先清 Overlay 列表（子 GameObject 随 Base 一起销毁）
            _realOverlayCameras[i].Clear();
            _hiddenOverlayCameras[i].Clear();

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

        // 检测 Overlay 堆栈是否变化（数量或内容）
        private bool OverlayStackChanged(int i, IList<Camera> realStack) {
            if (realStack.Count != _realOverlayCameras[i].Count) return true;
            for (int j = 0; j < realStack.Count; j++)
                if (realStack[j] != _realOverlayCameras[i][j]) return true;
            return false;
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

        // 只匹配隐藏 Base 相机（Overlay 不直接触发 pass）
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
