using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    internal class DebugOutputSettings
    {
        public bool EnableDebugOutput = true;
        public bool EnableColorDebugOutput = false; // 是否启用颜色调试输出
        public bool EnableDepthDebugOutput = false; // 是否启用深度调试输出
        public float DisplayHeightRatio = 0.5f; // Default height ratio for the debug display
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
            private const string ProfilerTag = "Depth Debug Output";
            private readonly ProfilingSampler _profilingSampler = new(ProfilerTag);
            
            // 临时渲染目标
            private RTHandle _tempColorTarget;

            public DepthOutputRenderPass(DebugOutputSettings settings, Material debugSplitMaterial)
            {
                _debugSplitMaterial = debugSplitMaterial;
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                
                // 确保临时目标有正确的格式
                cameraTargetDescriptor.depthBufferBits = 0; // 不需要深度缓冲
                
                // 创建临时渲染目标
                RenderingUtils.ReAllocateIfNeeded(ref _tempColorTarget, cameraTargetDescriptor, 
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempDebugColor");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                var colorTarget = cameraData.renderer.cameraColorTargetHandle;

                var cmd = CommandBufferPool.Get(ProfilerTag);
                
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    cmd.Blit(colorTarget.rt, _tempColorTarget);
                    
                    cmd.SetRenderTarget(colorTarget);
                    
                    cmd.SetGlobalTexture(DebugConstant.DebugColorInputId, _tempColorTarget);
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
                _tempColorTarget?.Release();
            }
        }

        private DepthOutputRenderPass _depthOutputPass;

        public override void Create()
        {
            _depthOutputPass = new DepthOutputRenderPass(
                new DebugOutputSettings(),
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