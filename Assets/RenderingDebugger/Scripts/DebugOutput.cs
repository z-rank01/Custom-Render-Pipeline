using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

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
    public class DebugOutput : ScriptableRendererFeature
    {
        [SerializeField] private Material debugSplitMaterial;
        [SerializeField] private Material debugBlitMaterial;
        
        private class DepthOutputRenderPass : ScriptableRenderPass
        {
            private RTHandle _debugOutputTarget; // 临时RT用于存储调试结果
            private readonly Material _debugBlitMaterial;
            private readonly Material _debugSplitMaterial;
            private readonly DebugOutputSettings _settings;
            private const string ProfilerTag = "Depth Debug Output";

            // 用于绘制全屏四边形
            private readonly Mesh _fullscreenMesh;
            private readonly Matrix4x4 _fullscreenMatrix;
            
            public DepthOutputRenderPass(DebugOutputSettings settings, Material debugSplitMaterial, Material debugBlitMaterial)
            {
                _debugSplitMaterial = debugSplitMaterial;
                _debugBlitMaterial = debugBlitMaterial;
                _settings = settings;
                
                // 创建全屏四边形的网格
                _fullscreenMesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(-1, -1, 0),
                        new Vector3(-1, 1, 0),
                        new Vector3(1, -1, 0),
                        new Vector3(1, 1, 0)
                    },
                    uv = new[]
                    {
                        new Vector2(0, 0),
                        new Vector2(0, 1),
                        new Vector2(1, 0),
                        new Vector2(1, 1)
                    },
                    triangles = new[] { 0, 1, 2, 2, 1, 3 }
                };

                // 设置全屏绘制的矩阵
                _fullscreenMatrix = Matrix4x4.identity;
            }
            
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // 创建一个与相机颜色目标相同格式和大小的临时RT
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                RenderingUtils.ReAllocateIfNeeded(ref _debugOutputTarget, descriptor, 
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_DebugOutputTarget");
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(ProfilerTag);
                var cameraData = renderingData.cameraData;
                var depthTarget = cameraData.renderer.cameraDepthTargetHandle;
                var colorTarget = cameraData.renderer.cameraColorTargetHandle;
                
                
                if (!_settings.EnableDebugOutput)
                {
                    cmd.ReleaseTemporaryRT(DebugConstant.DebugColorTargetId);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                    return;
                }
                
                cmd.SetGlobalFloat(DebugConstant.DebugDisplayHeightRatioId, _settings.DisplayHeightRatio);
                cmd.SetGlobalInt(DebugConstant.DebugScreenWidthId, renderingData.cameraData.cameraTargetDescriptor.width);
                cmd.SetGlobalInt(DebugConstant.DebugScreenHeightId, renderingData.cameraData.cameraTargetDescriptor.height);
                cmd.SetGlobalTexture(DebugConstant.DebugColorTargetId, colorTarget);
                
                // 首先将原始颜色复制到我们的调试目标（模拟 Color Debug Pass）
                cmd.SetRenderTarget(_debugOutputTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawMesh(_fullscreenMesh, _fullscreenMatrix, _debugBlitMaterial);
                
                // 然后在调试目标上叠加深度信息
                cmd.SetRenderTarget(_debugOutputTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                cmd.DrawMesh(_fullscreenMesh, _fullscreenMatrix, _debugSplitMaterial, 0, 0);
                
                // 最后将调试结果拷贝回相机目标
                cmd.SetRenderTarget(colorTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cmd.SetGlobalTexture(DebugConstant.DebugColorTargetId, _debugOutputTarget);
                cmd.DrawMesh(_fullscreenMesh, _fullscreenMatrix, _debugBlitMaterial);
                
                // 告诉渲染器使用原始的颜色和深度目标，这样不会影响其他渲染过程
                cameraData.renderer.ConfigureCameraTarget(colorTarget, depthTarget);
                
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
            
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }
        }

        private DepthOutputRenderPass _depthOutputPass;
        
        public override void Create()
        {
            _depthOutputPass = new DepthOutputRenderPass(
                new DebugOutputSettings(),
                debugSplitMaterial, 
                debugBlitMaterial
            )
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_depthOutputPass);
        }
    }
}
