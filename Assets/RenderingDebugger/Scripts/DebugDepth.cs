using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    internal struct DebugOutputSettings
    {
        public bool EnableDebugOutput;
        public float DisplayHeightRatio;

        public DebugOutputSettings(bool enableDebug, float displayHeightRatio)
        {
            EnableDebugOutput = enableDebug;
            DisplayHeightRatio = displayHeightRatio;
        }
    }

    [DisallowMultipleRendererFeature("Depth Debug Output")]
    [Tooltip("The debug output for depth information in the rendering pipeline.")]
    public class DebugDepth : ScriptableRendererFeature
    {
        [SerializeField] private Material debugDepthMaterial;

        private class DepthOutputRenderPass : ScriptableRenderPass
        {
            private readonly Material _debugSplitMaterial;
            private readonly DebugOutputSettings _settings;
            private const string ProfilerTag = "Debug Depth";
            private readonly ProfilingSampler _profilingSampler = new(ProfilerTag);
            
            // 临时渲染目标
            private RTHandle _tempRenderTarget;

            public DepthOutputRenderPass(DebugOutputSettings settings, Material debugSplitMaterial)
            {
                _debugSplitMaterial = debugSplitMaterial;
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempRenderTarget, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugDepthTarget");
                ConfigureInput(ScriptableRenderPassInput.Color);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                var colorTarget = cameraData.renderer.cameraColorTargetHandle;

                var cmd = CommandBufferPool.Get(ProfilerTag);
                
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    cmd.Blit(colorTarget.rt, _tempRenderTarget);
                    
                    cmd.SetRenderTarget(colorTarget);
                    
                    cmd.SetGlobalTexture(DebugConstant.DebugColorInputId, _tempRenderTarget);
                    cmd.SetGlobalFloat(DebugConstant.DebugDisplayHeightRatioId, _settings.DisplayHeightRatio);
                    cmd.SetGlobalInt(DebugConstant.DebugScreenWidthId, renderingData.cameraData.cameraTargetDescriptor.width);
                    cmd.SetGlobalInt(DebugConstant.DebugScreenHeightId, renderingData.cameraData.cameraTargetDescriptor.height);
                    
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

        private DepthOutputRenderPass _depthOutputPass;

        public override void Create()
        {
            _depthOutputPass = new DepthOutputRenderPass(
                new DebugOutputSettings(true, 0.5f),
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
                renderer.EnqueuePass(_depthOutputPass);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            _depthOutputPass?.Dispose();
        }
    }
}