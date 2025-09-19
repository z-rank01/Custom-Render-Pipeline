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
            _hiZPassOutput.AllocateOrResizeBuffers(_hiZPassResources.ObjectCount);
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

            // 3. 缓存 kernel 索引
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
            DispatchOcclusionTest(cmd, renderingData);  // 添加这一行调用遮挡剔除
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // 3. DrawIndirect
            // 如果需要在此处绘制，可以添加DrawVisibleIndirect(cmd)的调用

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
            var objects = FindObjectsOfType<Renderer>();
            if (_settings.renderObjects == null)
            {
                _settings.renderObjects = new List<Renderer>(objects);
            }
            else
            {
                _settings.renderObjects.Clear();
                _settings.renderObjects.AddRange(objects);
            }
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
            Debug.Log($"Build HiZ Texture (Compute Path:{_settings.computeShader is not null}): {width}x{height}, MipCount: {mipCount}");
#endif
        }

        // 遮挡测试占位接口
        private void DispatchOcclusionTest(CommandBuffer cmd, RenderingData renderingData)
        {
            if (_settings.computeShader is null || kFrustumOcclusionCull < 0 || _hiZPassResources.ObjectCount <= 0)
                return;
            
            // 获取相机矩阵
            var camera = renderingData.cameraData.camera;
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 vpMatrix = projMatrix * viewMatrix;
            
            // 重置Append缓冲计数
            _hiZPassOutput.ResetAppendBufferData();
            
            // 设置计算着色器参数
            var shader = _settings.computeShader;
            
            // 绑定矩阵和屏幕参数
            cmd.SetComputeMatrixParam(shader, "_VP", vpMatrix);
            cmd.SetComputeMatrixParam(shader, "_View", viewMatrix);
            cmd.SetComputeMatrixParam(shader, "_Proj", projMatrix);
            cmd.SetComputeVectorParam(shader, "_ScreenParams", new Vector4(
                Screen.width, Screen.height, 1.0f / Screen.width, 1.0f / Screen.height));
            
            // 绑定物体数据
            cmd.SetComputeIntParam(shader, "_InstanceCount", _hiZPassResources.ObjectCount);
            cmd.SetComputeIntParam(shader, "_HiZMipCount", _hiZPassOutput.MipCount);
            cmd.SetComputeBufferParam(shader, kFrustumOcclusionCull, "_BoundsCenter", _hiZPassResources.AabbCenterBuffer);
            cmd.SetComputeBufferParam(shader, kFrustumOcclusionCull, "_BoundsExtent", _hiZPassResources.AabbExtentBuffer);
            cmd.SetComputeBufferParam(shader, kFrustumOcclusionCull, "_VisibleIndices", _hiZPassOutput.AppendBuffer);
            
            // 绑定HiZ贴图
            cmd.SetComputeTextureParam(shader, kFrustumOcclusionCull, "_HiZSampleTex", _hiZPassOutput.HiZPyramid);
            
            // 如果使用间接绘制，还需要绑定绘制参数缓冲区
            // cmd.SetComputeBufferParam(shader, kFrustumOcclusionCull, "_Args", _hiZPassResources.ArgsBuffer);
            
            // 计算线程组数量并调度
            int threadGroupsX = (_hiZPassResources.ObjectCount + 63) / 64; // 每组64个线程
            cmd.DispatchCompute(shader, kFrustumOcclusionCull, threadGroupsX, 1, 1);
            
            // 可选：拷贝AppendBuffer内容到可见性结果缓冲区，用于后续处理
            // 注意：如果需要知道有多少物体可见，需要获取AppendBuffer的计数器值
            cmd.CopyCounterValue(_hiZPassOutput.AppendBuffer, _hiZPassOutput.VisibleResultBuffer, 0);
            
            // 调试输出
#if UNITY_EDITOR
            if (_settings.debug)
            {
                cmd.SetGlobalBuffer("_DebugVisibleIndices", _hiZPassOutput.VisibleResultBuffer);
            }
#endif
        }

        // 间接绘制可见物体
        private void DrawVisibleIndirect(CommandBuffer cmd)
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
            
            // 1. debug Hi-Z Texture
            DebugHiZTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 2. debug Hi-Z Cull Results
            DebugHiZCullResults(cmd);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
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

        private void DebugHiZCullResults(CommandBuffer cmd)
        {
            // get visible buffer result
            var result = new uint[_hiZPassOutput.AppendBuffer.count];
            _hiZPassOutput.AppendBuffer.GetData(result);
            foreach (var index in result)
            {
                var obj = _settings.renderObjects[(int)index];
                Debug.Log($"Visible Object: {obj.name} (Index: {index})");
            }
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
