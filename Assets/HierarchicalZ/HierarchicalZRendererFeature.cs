using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
    [Range(0, 10)] public int debugMipLevel;
    [Range(0.1f, 1f)] public float debugHeightRatio;
}

public class HierarchicalZRendererFeature : ScriptableRendererFeature
{
    public HierarchicalZRenderSettings settings = new();
    private HierarchicalZPassOutput hierarchicalZPassOutput = new();

    class HiZRenderPass : ScriptableRenderPass
    {
        private HierarchicalZPassResources _hiZPassResources;
        private readonly HierarchicalZRenderSettings _settings;
        private readonly HierarchicalZPassOutput _hiZPassOutput;

        private static readonly int kSrcMipTextureId = Shader.PropertyToID("_SrcMipTexture");
        private static readonly int kDstMipTextureId = Shader.PropertyToID("_DstMipTexture");
        private static readonly int kSrcMipLevelId = Shader.PropertyToID("_SrcMipLevel");
        private static readonly int kDstMipLevelId = Shader.PropertyToID("_DstMipLevel");
        private static readonly int kHiZWidthId = Shader.PropertyToID("_HiZWidth");
        private static readonly int kHiZHeightId = Shader.PropertyToID("_HiZHeight");
        private static readonly int kHiZMipCountId = Shader.PropertyToID("_HiZMipCount");
        private static readonly int kSrcDepthTextureId = Shader.PropertyToID("_SrcDepthTexture");
        private static readonly int kCameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        // Kernels
        private int kBuildHiZFirst = -1;
        private int kBuildHiZDown = -1;
        private int kFrustumOcclusionCull = -1;

        public HiZRenderPass(HierarchicalZRenderSettings settings, HierarchicalZPassOutput hierarchicalZPassOutput)
        {
            _settings = settings;
            _hiZPassOutput = hierarchicalZPassOutput;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 1. 收集场景内物体
            CollectSceneObjects();

            // 2. 初始化或更新资源
            var colorTextureDisc = renderingData.cameraData.cameraTargetDescriptor;
            var width = colorTextureDisc.width;
            var height = colorTextureDisc.height;
            var cam = renderingData.cameraData.camera;
            _hiZPassResources ??= new HierarchicalZPassResources(width, height, _settings.renderObjects.ToArray(), cam);
            _hiZPassResources.UpdateObjects(_settings.renderObjects.ToArray(), cam);
            _hiZPassOutput.ReAllocateIfNeeded(width, height, () =>
            {
                var mipCount = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2f)) + 1;
                var desc = new RenderTextureDescriptor(width, height)
                {
                    enableRandomWrite = true,
                    dimension = TextureDimension.Tex2D,
                    useMipMap = true,
                    autoGenerateMips = false,
                    graphicsFormat = GraphicsFormat.R32_SFloat,
                    mipCount = mipCount,
                    msaaSamples = 1
                };
                return RTHandles.Alloc(desc, name: "_HiZPyramid");
            });

            // 3. 缓存 kernel
            if (_settings.computeShader && kBuildHiZFirst < 0)
            {
                kBuildHiZFirst = _settings.computeShader.FindKernel("BuildHiZFirst");
                kBuildHiZDown = _settings.computeShader.FindKernel("BuildHiZDown");
                kFrustumOcclusionCull = _settings.computeShader.FindKernel("FrustumOcclusionCull");
            }
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Hierarchical Z Pass");

            // 1. Generate Hi-Z depth mip map
            
            // tips: prepass 之后必须使用 Global Texture 作为 Compute Shader 的输入
            // 不能直接使用 renderer.cameraDepthTargetHandle，因为 URP 会在 depth prepass 之后进行 Clear 操作
            var depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
            BuildHiZPyramid(cmd, depthTexture);
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
        private void BuildHiZPyramid(CommandBuffer cmd, Texture cameraDepth)
        {
            int width = _hiZPassOutput.HiZPyramid.rt.width;
            int height = _hiZPassOutput.HiZPyramid.rt.height;
            int mipCount = _hiZPassOutput.MipCount;

            // 用 Compute 生成 mip0 (摄像机深度 -> R32F)，避免 CopyTexture 跨格式报错
            if (_settings.computeShader && kBuildHiZFirst >= 0)
            {
                int gx0 = (width + 7) / 8;
                int gy0 = (height + 7) / 8;
                cmd.SetComputeIntParam(_settings.computeShader, kHiZWidthId, width);
                cmd.SetComputeIntParam(_settings.computeShader, kHiZHeightId, height);
                cmd.SetComputeIntParam(_settings.computeShader, kHiZMipCountId, mipCount);
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZFirst, kCameraDepthTextureId, cameraDepth); // 深度输入
                cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZFirst, kDstMipTextureId, _hiZPassOutput.HiZPyramid, 0);  // 写入 mip0
                cmd.DispatchCompute(_settings.computeShader, kBuildHiZFirst, gx0, gy0, 1);
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
                    cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZDown, kSrcMipTextureId, _hiZPassOutput.HiZPyramid, i - 1);
                    cmd.SetComputeTextureParam(_settings.computeShader, kBuildHiZDown, kDstMipTextureId, _hiZPassOutput.HiZPyramid, i);
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

    class HiZDebugPass : ScriptableRenderPass
    {
        private readonly HierarchicalZRenderSettings _settings;
        private readonly HierarchicalZPassOutput _hiZPassOutput;
        private RTHandle _tempColorTexture;

        public HiZDebugPass(HierarchicalZRenderSettings settings, HierarchicalZPassOutput hiZPassOutput)
        {
            _settings = settings;
            _hiZPassOutput = hiZPassOutput;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var colorTextureDisc = renderingData.cameraData.cameraTargetDescriptor;
            var width = colorTextureDisc.width;
            var height = colorTextureDisc.height;
            colorTextureDisc.depthBufferBits = 0; // 不需要深度
            RenderingUtils.ReAllocateIfNeeded(ref _tempColorTexture, colorTextureDisc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "HiZ_Debug_TempColor");
            _hiZPassOutput.ReAllocateIfNeeded(width, height, () =>
            {
                var mipCount = Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height), 2f)) + 1;
                var desc = new RenderTextureDescriptor(width, height)
                {
                    enableRandomWrite = true,
                    dimension = TextureDimension.Tex2D,
                    useMipMap = true,
                    autoGenerateMips = false,
                    graphicsFormat = GraphicsFormat.R32_SFloat,
                    mipCount = mipCount,
                    msaaSamples = 1
                };
                return RTHandles.Alloc(desc, name: "_HiZPyramid");
            });
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("HiZ Debug Pass");
            DebugHiZTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
        
        private void DebugHiZTexture(CommandBuffer cmd, RTHandle colorTarget)
        {
            cmd.Blit(colorTarget.rt, _tempColorTexture);
            cmd.SetGlobalTexture("_DebugColorInput", _tempColorTexture);
            cmd.SetGlobalTexture("_DebugHiZTexture", _hiZPassOutput.HiZPyramid);
            cmd.SetGlobalVector("_DebugParams", new Vector4(_settings.debugMipLevel, _settings.debugHeightRatio, 0, 0));
            cmd.SetRenderTarget(colorTarget);
            cmd.DrawProcedural(Matrix4x4.identity, _settings.debugHiZTextureMaterial, 0, MeshTopology.Triangles, 3, 1);
        }
    }

    HiZRenderPass m_HiZPass;
    HiZDebugPass m_HiZDebugPass;

    /// <inheritdoc/>
    public override void Create()
    {
        hierarchicalZPassOutput ??= new HierarchicalZPassOutput();
        m_HiZPass = new HiZRenderPass(settings, hierarchicalZPassOutput)
        {
            // 需要在 Depth Prepass 之后，Opaque 之前
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
        };
        if (settings.debug)
        {
            var hiZDebugPass = new HiZDebugPass(settings, hierarchicalZPassOutput)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };
            m_HiZDebugPass = hiZDebugPass;
        }
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_HiZPass);
        if (settings.debug)
            renderer.EnqueuePass(m_HiZDebugPass);
    }
}
