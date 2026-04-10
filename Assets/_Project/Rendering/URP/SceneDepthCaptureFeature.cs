using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WFCTechTest.Rendering {
    public sealed class SceneDepthCaptureFeature : ScriptableRendererFeature {
        public const string RawDepthGlobalTexture = "_CapturedRawDepthTexture";
        public const string LinearDepthGlobalTexture = "_CapturedLinearDepthTexture";

        [SerializeField] private Shader depthCaptureShader;
        [SerializeField] private RenderTexture rawDepthTexture;
        [SerializeField] private RenderTexture linearDepthTexture;

        private Material _material;
        private SceneDepthCapturePass _rawPass;
        private SceneDepthCapturePass _linearPass;
        private bool _loggedConfigurationError;

        public override void Create() {
            if (depthCaptureShader != null) _material = CoreUtils.CreateEngineMaterial(depthCaptureShader);
            if (_rawPass == null) _rawPass = new SceneDepthCapturePass("SceneDepthCapture.Raw", 0);
            if (_linearPass == null) _linearPass = new SceneDepthCapturePass("SceneDepthCapture.Linear", 1);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) {
            if (!ShouldExecute(renderingData.cameraData)) return;
            if (!TryValidateConfiguration(out _)) return;

            _rawPass.Setup(_material, rawDepthTexture, RawDepthGlobalTexture);
            _linearPass.Setup(_material, linearDepthTexture, LinearDepthGlobalTexture);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (!ShouldExecute(renderingData.cameraData)) return;

            if (!TryValidateConfiguration(out string error)) {
                if (!_loggedConfigurationError) {
                    _loggedConfigurationError = true;
                    Debug.LogError($"[{nameof(SceneDepthCaptureFeature)}] {error}");
                }
                return;
            }

            renderer.EnqueuePass(_rawPass);
            renderer.EnqueuePass(_linearPass);
        }

        protected override void Dispose(bool disposing) {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private bool TryValidateConfiguration(out string error) {
            if (depthCaptureShader == null) {
                error = "Depth capture shader is missing.";
                return false;
            }

            if (_material == null) {
                error = "Depth capture material could not be created from the configured shader.";
                return false;
            }

            if (rawDepthTexture == null) {
                error = "Raw depth RenderTexture is missing.";
                return false;
            }

            if (linearDepthTexture == null) {
                error = "Linear depth RenderTexture is missing.";
                return false;
            }

            _loggedConfigurationError = false;
            error = null;
            return true;
        }

        private static bool ShouldExecute(in CameraData cameraData) {
            if (cameraData.isPreviewCamera) return false;
            if (cameraData.camera == null || cameraData.camera.cameraType != CameraType.Game) return false;
            return cameraData.renderType == CameraRenderType.Base;
        }
    }
}
