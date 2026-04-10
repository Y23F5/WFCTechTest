using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WFCTechTest.Rendering {
    internal sealed class SceneDepthCapturePass : ScriptableRenderPass {
        private readonly int _shaderPassIndex;
        private readonly ProfilingSampler _profilingSampler;

        private Material _material;
        private RenderTexture _targetTexture;
        private string _globalTextureName;

        public SceneDepthCapturePass(string profilerTag, int shaderPassIndex) {
            _shaderPassIndex = shaderPassIndex;
            _profilingSampler = new ProfilingSampler(profilerTag);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public void Setup(Material material, RenderTexture targetTexture, string globalTextureName) {
            _material = material;
            _targetTexture = targetTexture;
            _globalTextureName = globalTextureName;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            if (_material == null || _targetTexture == null || string.IsNullOrWhiteSpace(_globalTextureName)) return;
            if (!_targetTexture.IsCreated()) _targetTexture.Create();

            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler)) {
                var target = new RenderTargetIdentifier(_targetTexture);
                CoreUtils.SetRenderTarget(cmd, target, ClearFlag.None);
                CoreUtils.DrawFullScreen(cmd, _material, shaderPassId: _shaderPassIndex);
                cmd.SetGlobalTexture(_globalTextureName, _targetTexture);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
