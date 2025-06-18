using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    [DisallowMultipleRendererFeature("Debug Depth")]
    [Tooltip("Render Feature for debugging depth information.")]
    public class DebugDepth : ScriptableRendererFeature
    {
        public DebugDepthSettings settings = new();
        [SerializeField] private Material debugDepthMaterial;
        private DebugDepthPass _debugDepthOutputPass;
        
        [System.Serializable]
        public class DebugDepthSettings
        {
            public bool enableDebugOutput;
            [Range(0.1f, 1f)] public float displayHeightRatio = 0.5f;
            public int depthDetectionThreshold = 100;
        }
        private class DebugDepthPass : ScriptableRenderPass
        {
            private readonly Material _debugSplitMaterial;
            private readonly DebugDepthSettings _settings;
            private const string ProfilerTag = "Debug Depth";
            private readonly ProfilingSampler _profilingSampler = new(ProfilerTag);

            // 临时渲染目标
            private RTHandle _tempRenderTarget;

            public DebugDepthPass(DebugDepthSettings settings, Material debugSplitMaterial)
            {
                _debugSplitMaterial = debugSplitMaterial;
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (!_settings.enableDebugOutput) return;
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempRenderTarget, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugDepthTarget");
                ConfigureInput(ScriptableRenderPassInput.Color);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!_settings.enableDebugOutput) return;
                
                var cameraData = renderingData.cameraData;
                var colorTarget = cameraData.renderer.cameraColorTargetHandle;

                var cmd = CommandBufferPool.Get(ProfilerTag);
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    cmd.Blit(colorTarget.rt, _tempRenderTarget);

                    cmd.SetGlobalTexture(DebugConstant.DebugColorInputId, _tempRenderTarget);
                    cmd.SetGlobalFloat(DebugConstant.DebugDisplayHeightRatioId, _settings.displayHeightRatio);
                    cmd.SetGlobalInt(DebugConstant.DebugSaturationThresholdId, _settings.depthDetectionThreshold);
                    cmd.SetRenderTarget(colorTarget);
                    cmd.DrawProcedural(Matrix4x4.identity, _debugSplitMaterial, 0, MeshTopology.Triangles, 3, 1);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }

            public void Dispose()
            {
                _tempRenderTarget?.Release();
            }
        }

        public override void Create()
        {
            _debugDepthOutputPass = new DebugDepthPass(
                settings,
                debugDepthMaterial
            )
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (debugDepthMaterial != null)
            {
                renderer.EnqueuePass(_debugDepthOutputPass);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            _debugDepthOutputPass?.Dispose();
        }
    }
}