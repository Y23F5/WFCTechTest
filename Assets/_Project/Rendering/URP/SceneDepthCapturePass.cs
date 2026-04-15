using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WFCTechTest.Rendering {
    internal sealed class SceneDepthCapturePass : ScriptableRenderPass {
        private readonly ProfilingSampler _profilingSampler;

        private Material      _material;
        private RenderTexture _targetRT;
        private int           _lastClearedFrame = -1;

        public SceneDepthCapturePass(string profilerTag) {
            _profilingSampler = new ProfilingSampler(profilerTag);
            renderPassEvent   = RenderPassEvent.AfterRendering;  // 修复天空盒变黑：在所有渲染完成后再执行
        }

        public void Setup(Material material, RenderTexture targetRT) {
            _material = material;
            _targetRT = targetRT;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
            // 告知 URP 本 pass 需要深度输入，确保 _CameraDepthTexture 在此时已生成
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            if (_material == null || _targetRT == null) return;
            if (!_targetRT.IsCreated()) _targetRT.Create();

            var cmd    = CommandBufferPool.Get(_profilingSampler.name);
            var target = new RenderTargetIdentifier(_targetRT);

            using (new ProfilingScope(cmd, _profilingSampler)) {
                // 每帧首次执行时清除 RT（避免残留数据）
                if (_lastClearedFrame != Time.frameCount) {
                    _lastClearedFrame = Time.frameCount;
                    float far = SystemInfo.usesReversedZBuffer ? 0f : 1f;
                    CoreUtils.SetRenderTarget(cmd, target, ClearFlag.Color,
                        new Color(far, far, far, far));
                } else {
                    CoreUtils.SetRenderTarget(cmd, target, ClearFlag.None);
                }

                // 全屏绘制：把本相机的 _CameraDepthTexture 直接写入 R32F RT
                CoreUtils.DrawFullScreen(cmd, _material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
