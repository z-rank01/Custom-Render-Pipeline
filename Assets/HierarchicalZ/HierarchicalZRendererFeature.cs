using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[System.Serializable]
public class HierarchicalZRenderSettings
{
    public List<Renderer> renderObjects;
    public ComputeShader computeShader;

    // Debug
    public bool debug;
    public Material debugHiZTextureMaterial;
    public int debugMipLevel;
    [Range(0.1f, 1f)] public float debugHeightRatio;
    public RTHandle tempRenderTarget;
}

public class HierarchicalZRendererFeature : ScriptableRendererFeature
{
    public HierarchicalZRenderSettings settings = new();

    class HiZRenderPass : ScriptableRenderPass
    {
        private HierarchicalZResources _hiZResources;
        private HierarchicalZRenderSettings _settings;

        private static readonly int kBuildHiZId = Shader.PropertyToID("BuildHiZDown");
        private static readonly int kSrcMipTextureId = Shader.PropertyToID("_SrcMipTexture");
        private static readonly int kDstMipTextureId = Shader.PropertyToID("_DstMipTexture");
        private static readonly int kSrcMipLevelId = Shader.PropertyToID("_SrcMipLevel");
        private static readonly int kDstMipLevelId = Shader.PropertyToID("_DstMipLevel");
        private static readonly int kFullScreenWidthId = Shader.PropertyToID("_FullScreenWidth");
        private static readonly int kFullScreenHeightId = Shader.PropertyToID("_FullScreenHeight");
        private static readonly int kMipTotalId = Shader.PropertyToID("_MipTotal");
        // Kernels
        private int kBuildHiZFirst = -1;
        private int kBuildHiZDown = -1;
        private int kFrustumOcclusionCull = -1;

        public HiZRenderPass(HierarchicalZRenderSettings settings)
        {
            _settings = settings;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var colorTextureDisc = renderingData.cameraData.cameraTargetDescriptor;
            _hiZResources ??= new HierarchicalZResources(colorTextureDisc.width, colorTextureDisc.height, _settings.renderObjects.ToArray(), Camera.current);
            _hiZResources.UpdateObjects(_settings.renderObjects.ToArray(), Camera.current);
            if (_settings.computeShader && kBuildHiZFirst < 0)
            {
                kBuildHiZFirst = _settings.computeShader.FindKernel("BuildHiZFirst");
                kBuildHiZDown = _settings.computeShader.FindKernel("BuildHiZDown");
                kFrustumOcclusionCull = _settings.computeShader.FindKernel("FrustumOcclusionCull");
            }
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Hierarchical Z Pass");

            // 1. Generate Hi-Z depth mip map
            BuildHiZPyramid(cmd, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            // 1.1 Debug output hi-z texture
            if (_settings.debug)
            {
                var colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var tempColorTexture = _settings.tempRenderTarget;
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref tempColorTexture, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SceneColor");
                cmd.Blit(colorTarget.rt, tempColorTexture);
                cmd.SetGlobalTexture("_DebugColorInput", tempColorTexture);
                cmd.SetGlobalTexture("_DebugHiZTexture", _hiZResources.HiZTexture);
                cmd.SetGlobalVector("_DebugParams", new Vector4(_settings.debugMipLevel, _settings.debugHeightRatio, 0, 0));
                cmd.SetRenderTarget(colorTarget);
                cmd.DrawProcedural(Matrix4x4.identity, _settings.debugHiZTextureMaterial, 0, MeshTopology.Triangles, 3, 1);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // 2. Culling

            // 3. DrawIndirect

            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        // 构建 Hi-Z 金字塔占位接口
        private void BuildHiZPyramid(CommandBuffer cmd, RTHandle cameraDepth)
        {
            // TODO:
            // 1. 拷贝 depth 到 mip 0 (或用 Blit / Compute)
            // 2. 循环 dispatch 生成后续 mip (每次上一次的 mip 作为输入)
            // 3. 保持与 mipCount 一致

            // copy to mip 0
            cmd.CopyTexture(cameraDepth, 0, 0, _hiZResources.HiZTexture, 0, 0);
            // build down
            int width = _hiZResources.HiZTexture.rt.width;
            int height = _hiZResources.HiZTexture.rt.height;
            int mipCount = _hiZResources.MipCount;
            cmd.SetComputeIntParam(_settings.computeShader, kFullScreenWidthId, width);
            cmd.SetComputeIntParam(_settings.computeShader, kFullScreenHeightId, height);
            cmd.SetComputeIntParam(_settings.computeShader, kMipTotalId, mipCount);
            for (int i = 1; i < mipCount; i++)
            {
                int mipWidth = Mathf.Max(1, width >> i);
                int mipHeight = Mathf.Max(1, height >> i);
                int gx = (mipWidth + 7) / 8;
                int gy = (mipHeight + 7) / 8;
                cmd.SetComputeIntParam(_settings.computeShader, kSrcMipLevelId, i - 1);
                cmd.SetComputeIntParam(_settings.computeShader, kDstMipLevelId, i);
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZId, kSrcMipTextureId, _hiZResources.HiZTexture, i - 1);      // source mip level
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZId, kDstMipTextureId, _hiZResources.HiZTexture, i);          // dest mip level
                cmd.DispatchCompute(_settings.computeShader, kBuildHiZDown, gx, gy, 1);
            }
#if UNITY_EDITOR
            Debug.Log($"Build HiZ Texture: {width}x{height}, MipCount: {mipCount}");
#endif
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

        // 间接绘制可见物体
        private void DrawVisibleIndirect(CommandBuffer cmd, ComputeShader cs, int kernelTest)
        {

        }
    }

    HiZRenderPass m_HiZPass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_HiZPass = new HiZRenderPass(settings)
        {
            // need to be after depth prepass while before opaque pass
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_HiZPass);
    }
}
