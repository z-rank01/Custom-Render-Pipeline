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

        private static readonly int kSrcMipTextureId = Shader.PropertyToID("_SrcMipTexture");
        private static readonly int kDstMipTextureId = Shader.PropertyToID("_DstMipTexture");
        private static readonly int kSrcMipLevelId = Shader.PropertyToID("_SrcMipLevel");
        private static readonly int kDstMipLevelId = Shader.PropertyToID("_DstMipLevel");
        // Correct parameter names matching compute shader (_HiZWidth/_HiZHeight/_HiZMipCount)
        private static readonly int kHiZWidthId = Shader.PropertyToID("_HiZWidth");
        private static readonly int kHiZHeightId = Shader.PropertyToID("_HiZHeight");
        private static readonly int kHiZMipCountId = Shader.PropertyToID("_HiZMipCount");
        private static readonly int kSrcDepthTextureId = Shader.PropertyToID("_SrcDepthTexture");
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
            // 1. 收集场景内物体
            CollectSceneObjects();

            // 2. 初始化或更新资源
            var colorTextureDisc = renderingData.cameraData.cameraTargetDescriptor;
            _hiZResources ??= new HierarchicalZResources(colorTextureDisc.width, colorTextureDisc.height, _settings.renderObjects.ToArray(), Camera.main);
            _hiZResources.RecreateHiZIfNeeded(colorTextureDisc.width, colorTextureDisc.height);
            _hiZResources.UpdateObjects(_settings.renderObjects.ToArray(), Camera.main);

            // 3. 缓存 kernel
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
                RenderingUtils.ReAllocateIfNeeded(ref tempColorTexture, cameraTargetDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SceneColor");
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

        // 收集场景内物体
        private void CollectSceneObjects()
        {
            // TODO:
            // 遍历场景内所有物体，筛选出需要进行 Hi-Z 测试的物体（例如根据标签、图层等条件）
            // 更新 _hiZResources 中的对象数据（AABB、变换矩阵等）
            var objects = GameObject.FindObjectsOfType<Renderer>();
            _settings.renderObjects = new List<Renderer>(objects);
        }

        // 构建 Hi-Z 金字塔占位接口
        private void BuildHiZPyramid(CommandBuffer cmd, RTHandle cameraDepth)
        {
            int width = _hiZResources.HiZTexture.rt.width;
            int height = _hiZResources.HiZTexture.rt.height;
            int mipCount = _hiZResources.MipCount;

            // 用 Compute 生成 mip0 (摄像机深度 -> R32F)，避免 CopyTexture 跨格式报错
            if (_settings.computeShader && kBuildHiZFirst >= 0)
            {
                int gx0 = (width + 7) / 8;
                int gy0 = (height + 7) / 8;
                cmd.SetComputeIntParam(_settings.computeShader, kHiZWidthId, width);
                cmd.SetComputeIntParam(_settings.computeShader, kHiZHeightId, height);
                cmd.SetComputeIntParam(_settings.computeShader, kHiZMipCountId, mipCount);
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZFirst, kSrcDepthTextureId, cameraDepth);                // 深度输入
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZFirst, kDstMipTextureId, _hiZResources.HiZTexture, 0);  // 写入 mip0
                cmd.DispatchCompute(_settings.computeShader, kBuildHiZFirst, gx0, gy0, 1);
            }
            else
            {
                // 兜底: Blit 方式 (需要一个简单 shader 采样深度输出 R32F；若当前材质不具备则只能占位)
                cmd.Blit(cameraDepth, _hiZResources.HiZTexture); // 仅写 base level
            }

            // 逐级生成剩余 mip
            if (_settings.computeShader && kBuildHiZDown >= 0)
            {
                for (int i = 1; i < mipCount; i++)
                {
                    int mipWidth = Mathf.Max(1, width >> i);
                    int mipHeight = Mathf.Max(1, height >> i);
                    int gx = (mipWidth + 7) / 8;
                    int gy = (mipHeight + 7) / 8;

                    cmd.SetComputeIntParam(_settings.computeShader, kSrcMipLevelId, i - 1);
                    cmd.SetComputeIntParam(_settings.computeShader, kDstMipLevelId, i);
                    // 传递基础尺寸（部分平台可能每个 kernel 需要再次设置）
                    cmd.SetComputeIntParam(_settings.computeShader, kHiZWidthId, width);
                    cmd.SetComputeIntParam(_settings.computeShader, kHiZHeightId, height);
                    cmd.SetComputeIntParam(_settings.computeShader, kHiZMipCountId, mipCount);
                    cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZDown, kSrcMipTextureId, _hiZResources.HiZTexture, i - 1);
                    cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZDown, kDstMipTextureId, _hiZResources.HiZTexture, i);
                    cmd.DispatchCompute(_settings.computeShader, kBuildHiZDown, gx, gy, 1);
                }
            }
#if UNITY_EDITOR
            Debug.Log($"Build HiZ Texture (Compute Path:{_settings.computeShader != null}): {width}x{height}, MipCount: {mipCount}");
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
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_HiZPass);
    }
}
