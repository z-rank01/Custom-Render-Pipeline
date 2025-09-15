using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CustomRenderPipeline.HiZ
{
    /// <summary>
    /// 基于 Hierarchical-Z 的 GPU 遮挡剔除与间接绘制示例实现。
    /// 说明：
    /// 1. 利用 URP 已生成的 _CameraDepthTexture（无需再 CopyDepth）。
    /// 2. 生成 Hi-Z Mip 链（最大深度）。
    /// 3. 进行视锥 + 粗略遮挡剔除，将可见实例索引 Append 进列表。
    /// 4. 利用 DrawMeshInstancedIndirect 进行最终绘制。
    /// （此实现为演示/教学用，未做完整的生产级优化与数据缓存）。
    /// </summary>
    public class HierarchicalZOcclusionRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class HierarchicalZSettings
        {
            [Tooltip("最大实例数量(剔除/绘制缓冲预留大小)")]
            public int maxInstances = 4096;

            [Tooltip("生成 HiZ 与剔除的 Compute Shader（包含 BuildHiZ / Downsample / FrustumOcclusionCull 内核）")]
            public ComputeShader hiZCompute;

            [Tooltip("剔除后用于绘制的材质（需支持 GPU Instancing）")]
            public Material indirectMaterial;

            [Tooltip("默认测试使用的 Mesh (如果场景内 Renderer.mesh 不全相同，可扩展为多批次)")]
            public Mesh mesh;

            [Tooltip("是否在 FrameDebugger 中显示 Profile Sample")]
            public bool enableProfiling = true;

            [Tooltip("调试：是否强制关闭遮挡，只做视锥过滤")]
            public bool debugDisableOcclusion = false;
        }

        public HierarchicalZSettings settings = new();
        private HierarchicalZOcclusionResources _occlusionResources;
        private HiZOcclusionRenderPass _pass;

        class HiZOcclusionRenderPass : ScriptableRenderPass
        {
            private readonly HierarchicalZSettings _settings;
            private readonly HierarchicalZOcclusionResources _occlusionResources;
            private readonly List<MeshRenderer> _sceneRenderers = new();

            // Property IDs
            private static readonly int ID_HiZTex = Shader.PropertyToID("_HiZTexture");
            private static readonly int ID_VP = Shader.PropertyToID("_VP");
            private static readonly int ID_View = Shader.PropertyToID("_View");
            private static readonly int ID_Proj = Shader.PropertyToID("_Proj");
            private static readonly int ID_ScreenSize = Shader.PropertyToID("_ScreenSize");
            private static readonly int ID_InstanceCount = Shader.PropertyToID("_InstanceCount");
            private static readonly int ID_DebugDisableOcclusion = Shader.PropertyToID("_DebugDisableOcclusion");

            // Kernels
            private int kBuildHiZFirst = -1;
            private int kBuildHiZDown = -1;
            private int kFrustumOcclusionCull = -1;

            public HiZOcclusionRenderPass(HierarchicalZSettings settings, HierarchicalZOcclusionResources occlusionResources)
            {
                _settings = settings;
                _occlusionResources = occlusionResources;
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses; // 确保 _CameraDepthTexture 可用
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var camDesc = renderingData.cameraData.cameraTargetDescriptor;
                _occlusionResources.ReallocateHiZTextureIfNeeded(cmd, camDesc.width, camDesc.height);

                if (_settings.hiZCompute && kBuildHiZFirst < 0)
                {
                    kBuildHiZFirst = _settings.hiZCompute.FindKernel("BuildHiZFirst");
                    kBuildHiZDown = _settings.hiZCompute.FindKernel("BuildHiZDown");
                    kFrustumOcclusionCull = _settings.hiZCompute.FindKernel("FrustumOcclusionCull");
                }
            }

            private void CollectSceneInstances()
            {
                _sceneRenderers.Clear();
                // 简化：每帧收集（可扩展为脏标记 / Editor 回调）
                var all = Object.FindObjectsOfType<MeshRenderer>();
                foreach (var r in all)
                {
                    if (!r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
                        continue;
                    if (!r.TryGetComponent<MeshFilter>(out var mf))
                        continue;
                    if (mf.sharedMesh == null)
                        continue;
                    _sceneRenderers.Add(r);
                }
            }

            private void UploadBoundsData()
            {
                int count = Mathf.Min(_sceneRenderers.Count, _settings.maxInstances);
                _occlusionResources.ReallocateInstanceBufferIfNeeded(count);

                var centers = _occlusionResources.centersCache;
                var extents = _occlusionResources.extentsCache;
                for (int i = 0; i < count; i++)
                {
                    var b = _sceneRenderers[i].bounds;
                    centers[i] = b.center;
                    extents[i] = b.extents;
                }
                _occlusionResources.boundsCenterBuffer.SetData(centers, 0, 0, count);
                _occlusionResources.boundsExtentBuffer.SetData(extents, 0, 0, count);
            }

            private void BuildHiZ(CommandBuffer cmd, ref RenderingData rd)
            {
                if (_settings.hiZCompute == null) return;
                var hiz = _occlusionResources.hiZTexture;
                int width = hiz.width;
                int height = hiz.height;
                int mipCount = hiz.mipmapCount;
#if UNITY_EDITOR
                Debug.Log($"Build HiZ Texture: {width}x{height}, MipCount: {mipCount}");
#endif
                // 第一层：_CameraDepthTexture -> _HiZMip0 (RWTexture2D)
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZWidth", width);
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZHeight", height);
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZMipCount", mipCount);
                cmd.SetComputeTextureParam(_settings.hiZCompute, kBuildHiZFirst, "_CameraDepthTexture", BuiltinRenderTextureType.Depth);
                cmd.SetComputeTextureParam(_settings.hiZCompute, kBuildHiZFirst, "_HiZMip0", hiz, 0);
                int gx = (width + 7) / 8;
                int gy = (height + 7) / 8;
                cmd.DispatchCompute(_settings.hiZCompute, kBuildHiZFirst, gx, gy, 1);

                // 逐级下采样：读 _SourceHiZ(mip-1) 写 _DestHiZ(mip)
                for (int currMipLevel = 1; currMipLevel < mipCount; currMipLevel++)
                {
                    int prevMipLevel = currMipLevel - 1;
                    int mw = Mathf.Max(1, width >> currMipLevel);
                    int mh = Mathf.Max(1, height >> currMipLevel);
                    gx = (mw + 7) / 8;
                    gy = (mh + 7) / 8;
                    cmd.SetComputeIntParam(_settings.hiZCompute, "_SrcMip", prevMipLevel);
                    cmd.SetComputeIntParam(_settings.hiZCompute, "_DstMip", currMipLevel);
                    cmd.SetComputeTextureParam(_settings.hiZCompute, kBuildHiZDown, "_SourceHiZ", hiz, prevMipLevel);
                    cmd.SetComputeTextureParam(_settings.hiZCompute, kBuildHiZDown, "_DestHiZ", hiz, currMipLevel);
                    cmd.DispatchCompute(_settings.hiZCompute, kBuildHiZDown, gx, gy, 1);
                }
            }

            private void FrustumAndOcclusionCulling(CommandBuffer cmd, ref RenderingData rd)
            {
                if (_settings.hiZCompute == null) return;
                int instanceCount = Mathf.Min(_sceneRenderers.Count, _settings.maxInstances);
                if (instanceCount == 0) return;

                // Reset append counter + args.instanceCount = 0
                _occlusionResources.visibleInstancesBuffer.SetCounterValue(0);
                _occlusionResources.PrepareArgsBuffer(_settings.mesh);

                var camData = rd.cameraData;
                Matrix4x4 VP = camData.GetGPUProjectionMatrix() * camData.GetViewMatrix();
                Matrix4x4 V = camData.GetViewMatrix();
                Matrix4x4 P = camData.GetGPUProjectionMatrix();
                var hiz = _occlusionResources.hiZTexture;

                cmd.SetComputeBufferParam(_settings.hiZCompute, kFrustumOcclusionCull, "_BoundsCenter", _occlusionResources.boundsCenterBuffer);
                cmd.SetComputeBufferParam(_settings.hiZCompute, kFrustumOcclusionCull, "_BoundsExtent", _occlusionResources.boundsExtentBuffer);
                cmd.SetComputeBufferParam(_settings.hiZCompute, kFrustumOcclusionCull, "_VisibleIndices", _occlusionResources.visibleInstancesBuffer);
                cmd.SetComputeBufferParam(_settings.hiZCompute, kFrustumOcclusionCull, "_Args", _occlusionResources.argsBuffer);
                cmd.SetComputeTextureParam(_settings.hiZCompute, kFrustumOcclusionCull, "_HiZSampleTex", hiz);
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZWidth", hiz.width);
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZHeight", hiz.height);
                cmd.SetComputeIntParam(_settings.hiZCompute, "_HiZMipCount", hiz.mipmapCount);
                cmd.SetComputeMatrixParam(_settings.hiZCompute, ID_VP, VP);
                cmd.SetComputeMatrixParam(_settings.hiZCompute, ID_View, V);
                cmd.SetComputeMatrixParam(_settings.hiZCompute, ID_Proj, P);
                cmd.SetComputeVectorParam(_settings.hiZCompute, ID_ScreenSize, new Vector4(hiz.width, hiz.height, 1f / hiz.width, 1f / hiz.height));
                cmd.SetComputeIntParam(_settings.hiZCompute, ID_InstanceCount, instanceCount);
                cmd.SetComputeIntParam(_settings.hiZCompute, ID_DebugDisableOcclusion, _settings.debugDisableOcclusion ? 1 : 0);

                int gx = (instanceCount + 63) / 64; // 64 线程组
                cmd.DispatchCompute(_settings.hiZCompute, kFrustumOcclusionCull, gx, 1, 1);

                // 将 append 计数写回 args[1]
                ComputeBuffer.CopyCount(_occlusionResources.visibleInstancesBuffer, _occlusionResources.argsBuffer, sizeof(uint)); // offset 4 -> args[1]
            }

            private void DrawVisibleInstances(CommandBuffer cmd)
            {
                if (_settings.mesh == null || _settings.indirectMaterial == null) return;
                // 提供可见索引列表供材质/着色器采样（可用于自定义 Instance 数据）
                cmd.SetGlobalBuffer("_VisibleInstanceIndices", _occlusionResources.visibleInstancesBuffer);
                cmd.SetGlobalTexture(ID_HiZTex, _occlusionResources.hiZTexture);
                cmd.DrawMeshInstancedIndirect(_settings.mesh, 0, _settings.indirectMaterial, 0, _occlusionResources.argsBuffer);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_settings.hiZCompute == null || _settings.mesh == null || _settings.indirectMaterial == null)
                    return;

                var cmd = CommandBufferPool.Get("HiZ Occlusion");
                using (new ProfilingScope(cmd, new ProfilingSampler(_settings.enableProfiling ? "HiZ Occlusion" : "")))
                {
                    CollectSceneInstances();
                    UploadBoundsData();
                    BuildHiZ(cmd, ref renderingData);
                    FrustumAndOcclusionCulling(cmd, ref renderingData);
                    DrawVisibleInstances(cmd);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // hiZ Texture 为跨帧复用，可在 Feature Dispose 时统一释放；若需要每帧重建，可在此释放。
            }
        }

        public override void Create()
        {
            if (_occlusionResources == null) _occlusionResources = new HierarchicalZOcclusionResources(settings.maxInstances);
            _pass = new HiZOcclusionRenderPass(settings, _occlusionResources);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.hiZCompute && settings.mesh && settings.indirectMaterial)
                renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _occlusionResources?.Dispose();
        }
    }
}


