using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HierarchicalZRendererFeature : ScriptableRendererFeature
{
    class HiZRenderPass : ScriptableRenderPass
    {
        HierarchicalZResources hiZResources;
        
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var colorTextureDisc = renderingData.cameraData.cameraTargetDescriptor;
            
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 1. Generate Hi-Z depth mip map
            
            // 2. Filter scene object
            
            // 3. DrawIndirect
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
        
        // 构建 Hi-Z 金字塔占位接口
        private void BuildHiZPyramid(CommandBuffer cmd, RTHandle cameraDepth, ComputeShader cs, int kernel0)
        {
            // TODO:
            // 1. 拷贝 depth 到 mip 0 (或用 Blit / Compute)
            // 2. 循环 dispatch 生成后续 mip (每次上一次的 mip 作为输入)
            // 3. 保持与 mipCount 一致
        }

        // 遮挡测试占位接口
        private void DispatchOcclusionTest(CommandBuffer cmd, ComputeShader cs, int kernelTest)
        {
            // TODO:
            // 传入:
            //  - AABB center / extent
            //  - 对象矩阵或已转换的裁剪空间包围盒
            //  - Hi-Z 贴图 (所有 mip)
            // 输出:
            //  - _visibilityResultBuffer 或 Append 列表
        }
    }

    HiZRenderPass m_HiZPass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_HiZPass = new HiZRenderPass
        {
            // need to be after depth prepass while before opaque pass
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_HiZPass);
    }
}


