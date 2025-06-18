using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    public class DebugOverdrawOverlapped : ScriptableRendererFeature
    {
        public OverlappedSettings settings = new();
        private DebugOverdrawOverlappedPass _debugOverdrawOverlappedPass;


        [System.Serializable]
        public class OverlappedSettings
        {
            [Header("Overdraw Settings")]
            public bool enableOverdrawDetection = true;
            public Material overdrawVisualizationMaterial;
            public Material overdrawCountMaterial;

            [Header("Visualization Settings")]
            [Range(0f, 1f)] public float overdrawDisplayHeightRatio = 0.5f;
            [Range(0f, 1f)] public float overdrawIntensity = 0.7f;
            [Range(1, 50)] public uint maxOverdrawThreshold = 20;
            [ColorUsage(false)] public Color minOverdrawColor = Color.gray;
            [ColorUsage(false)] public Color maxOverdrawColor = Color.white;
        }


        class DebugOverdrawOverlappedPass : ScriptableRenderPass
        {
            private readonly OverlappedSettings _settings;
            private RTHandle _overdrawCountTexture;
            private RTHandle _overdrawDepthTexture;
            private RTHandle _tempColorTarget;
            private RTHandle _tempDepthTarget;

            public DebugOverdrawOverlappedPass(OverlappedSettings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var countDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                countDescriptor.colorFormat = RenderTextureFormat.RFloat;
                countDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _overdrawCountTexture, countDescriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_OverdrawOverlappedCountTexture");

                var depthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = 24;
                RenderingUtils.ReAllocateIfNeeded(ref _overdrawDepthTexture, depthDescriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_OverdrawOverlappedDepthTexture");

                var sourceColorDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                sourceColorDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                sourceColorDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempColorTarget, sourceColorDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_OverdrawOverlappedSourceColorTexture");

                var sourceDepthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                sourceDepthDescriptor.colorFormat = RenderTextureFormat.RFloat;
                sourceDepthDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempDepthTarget, sourceDepthDescriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: "_OverdrawOverlappedSourceDepthTexture");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get("Smart Overdraw Detection");
                var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cameraDepthTarget = renderingData.cameraData.renderer.cameraDepthTargetHandle;

                using (new ProfilingScope(cmd, new ProfilingSampler("Blit Source Color and Clear Overdraw Count")))
                {
                    // 1. 保存当前颜色缓冲区
                    cmd.Blit(cameraColorTarget.rt, _tempColorTarget);

                    // 2. 保存当前深度缓冲区
                    cmd.Blit(cameraDepthTarget.rt, _tempDepthTarget);

                    // 3. 清零 overdraw 计数纹理
                    cmd.SetRenderTarget(cameraColorTarget, cameraDepthTarget);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 4.  生成 overdraw 计数
                var sortingSettings = CreateSortingSettings(ref renderingData, false);
                var drawingSettings = CreateDrawingSettings(ref renderingData, sortingSettings);
                var filteringSettings = CreateFilteringSettings(ref renderingData);
                var renderStateBlock = CreateRenderStateBlock(ref renderingData, false);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                // // 5. 生成 overdraw 可视化
                // using (new ProfilingScope(cmd, new ProfilingSampler("Generate Overdraw Visualization")))
                // {
                //     // 设置着色器参数
                //     cmd.SetGlobalTexture(DebugConstant.OverdrawOverlappedOriginalColorTextureId, _tempColorTarget);
                //     cmd.SetGlobalTexture(DebugConstant.OverdrawOverlappedCountBufferId, _overdrawCountTexture);
                //     cmd.SetGlobalFloat(DebugConstant.OverdrawOverlappedDisplayHeightRatioId, _settings.overdrawDisplayHeightRatio);
                //     cmd.SetGlobalFloat(DebugConstant.OverdrawOverlappedIntensityId, _settings.overdrawIntensity);
                //     cmd.SetGlobalFloat(DebugConstant.OverdrawOverlappedThresholdId, _settings.maxOverdrawThreshold);
                //     cmd.SetGlobalColor(DebugConstant.OverdrawOverlappedMinColorId, _settings.minOverdrawColor);
                //     cmd.SetGlobalColor(DebugConstant.OverdrawOverlappedMaxColorId, _settings.maxOverdrawColor);

                //     cmd.SetRenderTarget(cameraColorTarget);
                //     cmd.DrawProcedural(Matrix4x4.identity, _settings.overdrawVisualizationMaterial, 0, MeshTopology.Triangles, 3, 1);
                // }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }

            public void Dispose()
            {
                _overdrawCountTexture?.Release();
                _tempColorTarget?.Release();
            }

            private DrawingSettings CreateDrawingSettings(ref RenderingData renderingData, SortingSettings sortingSettings)
            {
                // TODO: Add support for custom shaders if needed
                // Note: These shader tags aim at urp default shaders like Lit.shader, BakedLit.shader.
                //       The shader tags may vary based on the URP version and custom shaders used.
                var shaderTagIds = new ShaderTagId[]
                {
                    new("UniversalForward"),
                    new("UniversalForwardOnly"),
                    new("LightweightForward")
                };
                var drawingSettings = new DrawingSettings(shaderTagIds[0], sortingSettings)
                {
                    // Use the overdraw count material to override the default shader
                    overrideMaterial = _settings.overdrawCountMaterial,
                    overrideMaterialPassIndex = 0,

                    perObjectData = renderingData.perObjectData,
                    mainLightIndex = renderingData.lightData.mainLightIndex,
                    enableDynamicBatching = renderingData.supportsDynamicBatching,
                    enableInstancing = false
                };
                for (int i = 1; i < shaderTagIds.Length; ++i)
                    drawingSettings.SetShaderPassName(i, shaderTagIds[i]);
                return drawingSettings;
            }

            private SortingSettings CreateSortingSettings(ref RenderingData renderingData, bool useDepthPriming)
            {
                var camera = renderingData.cameraData.camera;
                // 复制URP DrawObjectsPass的排序逻辑
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

                // 检查深度预处理条件，与URP保持一致
                if (useDepthPriming &&
                    (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
                {
                    sortFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue |
                               SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
                }
                return new SortingSettings(camera)
                {
                    criteria = sortFlags,
                    // criteria = renderingData.cameraData.defaultOpaqueSortFlags
                    // criteria = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder
                };
            }

            private FilteringSettings CreateFilteringSettings(ref RenderingData renderingData)
            {
                return new FilteringSettings(RenderQueueRange.opaque);
            }

            private RenderStateBlock CreateRenderStateBlock(ref RenderingData renderingData, bool useDepthPriming)
            {
                var additiveColorBlendState = new RenderTargetBlendState
                {
                    sourceColorBlendMode = BlendMode.One,
                    destinationColorBlendMode = BlendMode.One,
                    sourceAlphaBlendMode = BlendMode.One,
                    destinationAlphaBlendMode = BlendMode.Zero,
                    colorBlendOperation = BlendOp.Add,
                    writeMask = ColorWriteMask.All
                };
                var renderStateBlock = new RenderStateBlock()
                {
                    blendState = new BlendState { blendState0 = additiveColorBlendState },
                    mask = RenderStateMask.Blend
                };
                // 添加深度状态处理，与URP保持一致
                if (useDepthPriming &&
                    (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
                {
                    renderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
                    renderStateBlock.mask |= RenderStateMask.Depth;
                }
                else
                {
                    renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
                    renderStateBlock.mask |= RenderStateMask.Depth;
                }
                return renderStateBlock;
            }
        }



        /// <inheritdoc/>
        public override void Create()
        {
            _debugOverdrawOverlappedPass = new DebugOverdrawOverlappedPass(settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_debugOverdrawOverlappedPass);
        }

        protected override void Dispose(bool disposing)
        {
            _debugOverdrawOverlappedPass?.Dispose();
        }
    }
}
