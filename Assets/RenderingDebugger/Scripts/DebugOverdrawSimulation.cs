using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    [DisallowMultipleRendererFeature("Overdraw Debug Output")]
    [Tooltip("The debug output for overdraw in the rendering pipeline.")]
    public class DebugOverdrawSimulation : ScriptableRendererFeature
    {
        public DebugOverdrawSettings settings = new();
        private DebugOverdrawSimulationPass _debugOverdrawSimulationPass;

        [System.Serializable]
        public class DebugOverdrawSettings
        {
            [Header("Debug Overdraw Materials")]
            public Material DebugOverdrawMaterial;
            public Material DebugSplitMaterial;

            [Header("Overdraw Detection Settings")]
            public int OverdrawDetectionThreshold = 10;
            [Range(0.1f, 1f)] public float DebugDisplayHeightRatio = 0.5f;
            public Color DebugOverdrawColor = new(0.1f, 0.1f, 0.1f, 0.5f);
        }

        private class DebugOverdrawSimulationPass : ScriptableRenderPass
        {
            private const string ProfilerTag = "Debug Overdraw";
            private RTHandle _tempRenderTarget;
            private RTHandle _sourceRenderTarget;
            private readonly DebugOverdrawSettings _settings;

            public DebugOverdrawSimulationPass(DebugOverdrawSettings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // Check if the debug overdraw material is assigned
                if (!_settings.DebugOverdrawMaterial || !_settings.DebugSplitMaterial)
                {
                    Debug.LogWarning("Debug Overdraw materials are not assigned.");
                    return;
                }

                // allocate a temporary render target for the debug overdraw pass
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempRenderTarget, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugOverdrawTarget");
                RenderingUtils.ReAllocateIfNeeded(ref _sourceRenderTarget, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugOverdrawSource");

                // configure the render pass to use the temporary render target
                // ConfigureTarget(_tempRenderTarget);
                // ConfigureClear(ClearFlag.All, Color.black);

                // set up the render pass event
                base.profilingSampler = new ProfilingSampler(ProfilerTag);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Check if the debug overdraw material is assigned
                if (!_settings.DebugOverdrawMaterial || !_settings.DebugSplitMaterial)
                    return;

                var cmd = CommandBufferPool.Get(ProfilerTag);

                // get the camera color target and set it as the source render target
                using (new ProfilingScope(cmd, base.profilingSampler))
                {
                    // set the temporary render target as the active render target
                    cmd.SetRenderTarget(_sourceRenderTarget);
                    cmd.Blit(renderingData.cameraData.renderer.cameraColorTargetHandle, _sourceRenderTarget);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                // set up the debug overdraw material
                using (new ProfilingScope(cmd, new ProfilingSampler("Setup Overdraw Parameters")))
                {
                    cmd.SetGlobalColor(DebugConstant.DebugOverdrawColorId, _settings.DebugOverdrawColor);
                    cmd.SetRenderTarget(_tempRenderTarget);
                    cmd.ClearRenderTarget(true, true, Color.clear);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
                // draw renderers with debug overdraw material
                var sortingSettings = CreateSortingSettings(ref renderingData);
                var drawingSettings = CreateDrawingSettings(ref renderingData, sortingSettings);
                var filteringSettings = CreateFilteringSettings(ref renderingData);
                var renderStateBlock = CreateRenderStateBlock();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

                // blit the result to the camera color target
                using (new ProfilingScope(cmd, new ProfilingSampler("Blit Overdraw Result")))
                {
                    var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    cmd.SetGlobalFloat(DebugConstant.DebugDisplayHeightRatioId, _settings.DebugDisplayHeightRatio);
                    cmd.SetGlobalTexture(DebugConstant.DebugOverdrawResultId, _tempRenderTarget);
                    cmd.SetGlobalTexture(DebugConstant.DebugColorInputId, _sourceRenderTarget);
                    cmd.SetRenderTarget(cameraColorTarget);
                    cmd.DrawProcedural(Matrix4x4.identity, _settings.DebugSplitMaterial, 0, MeshTopology.Triangles, 3, 1);
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
                    // use the debug overdraw material for rendering
                    overrideMaterial = _settings.DebugOverdrawMaterial,
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


            private SortingSettings CreateSortingSettings(ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                return new SortingSettings(camera)
                {
                    criteria = SortingCriteria.CommonOpaque | SortingCriteria.QuantizedFrontToBack
                };
            }

            private FilteringSettings CreateFilteringSettings(ref RenderingData renderingData)
            {
                return new FilteringSettings(RenderQueueRange.opaque);
            }

            private RenderStateBlock CreateRenderStateBlock()
            {
                var additiveColorBlendState = new RenderTargetBlendState
                {
                    sourceColorBlendMode = BlendMode.One,
                    destinationColorBlendMode = BlendMode.One,
                    sourceAlphaBlendMode = BlendMode.One,
                    destinationAlphaBlendMode = BlendMode.Zero,
                    colorBlendOperation = BlendOp.Add,
                    alphaBlendOperation = BlendOp.Add,
                    writeMask = ColorWriteMask.All
                };
                var renderStateBlock = new RenderStateBlock()
                {
                    // cull off to detect overdraw in all directions
                    rasterState = new RasterState(cullingMode: CullMode.Off, offsetUnits: 0, offsetFactor: 0),
                    blendState = new BlendState { blendState0 = additiveColorBlendState },
                    depthState = new DepthState(true, CompareFunction.LessEqual),
                    mask = RenderStateMask.Raster | RenderStateMask.Blend | RenderStateMask.Depth
                };
                return renderStateBlock;
            }
        }

        /// <inheritdoc/>
        public override void Create()
        {
            _debugOverdrawSimulationPass = new DebugOverdrawSimulationPass(settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_debugOverdrawSimulationPass);
        }

        protected override void Dispose(bool disposing)
        {
            _debugOverdrawSimulationPass?.Dispose();
        }
    }
}


