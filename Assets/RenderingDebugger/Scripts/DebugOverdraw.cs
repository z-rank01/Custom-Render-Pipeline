using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Overdraw Debug Output")]
[Tooltip("The debug output for overdraw in the rendering pipeline.")]
public class DebugOverdraw : ScriptableRendererFeature
{
    [SerializeField] private Material debugOverdrawMaterial;

    [Tooltip(@"Overdraw detection threshold. 
Note: This value determines how many times a pixel can be drawn until it cannot be accmulated (completely white). For example, if set to 10, a pixel can be drawn up to 10 times and it will not be counted for rest of drawcalls."
    )]
    [SerializeField] private int overdrawDetectionThreshold = 20; // Threshold for overdraw detection

    internal readonly struct DebugOverdrawSettings
    {
        public readonly bool EnableDebugOverdraw;
        public readonly int OverdrawDetectionThreshold;
        public readonly Color debugOverdrawColor;
        public DebugOverdrawSettings(bool enableDebugOverdraw, int overdrawDetectionThreshold)
        {
            EnableDebugOverdraw = enableDebugOverdraw;
            OverdrawDetectionThreshold = overdrawDetectionThreshold;
            debugOverdrawColor = new Color(1f / overdrawDetectionThreshold, 1f / overdrawDetectionThreshold, 1f / overdrawDetectionThreshold, 1f);
        }
    }

    class DebugOverdrawPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "Debug Overdraw";
        private RTHandle _tempRenderTarget;
        private readonly Material _debugOverdrawMaterial;
        private readonly DebugOverdrawSettings _settings;

        public DebugOverdrawPass(Material debugOverdrawMaterial, DebugOverdrawSettings settings)
        {
            _debugOverdrawMaterial = debugOverdrawMaterial;
            _settings = settings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Check if the debug overdraw material is assigned
            if (_debugOverdrawMaterial == null)
            {
                Debug.LogWarning("Debug Overdraw is disabled or material is not assigned.");
                return;
            }

            // allocate a temporary render target for the debug overdraw pass
            var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTargetDescriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRenderTarget, cameraTargetDescriptor,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugOverdrawTarget");

            // configure the render pass to use the temporary render target
            ConfigureTarget(_tempRenderTarget);
            ConfigureClear(ClearFlag.All, Color.black);

            // set up the render pass event
            base.profilingSampler = new ProfilingSampler(ProfilerTag);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Check if the debug overdraw material is assigned
            if (_debugOverdrawMaterial == null)
                return;

            var cmd = CommandBufferPool.Get(ProfilerTag);

            // set up the debug overdraw material
            using (new ProfilingScope(cmd, new ProfilingSampler("Setup Overdraw Parameters")))
            {
                cmd.SetGlobalColor("_OverdrawColor", _settings.debugOverdrawColor);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

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
                cmd.Blit(_tempRenderTarget, cameraColorTarget);
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
                overrideMaterial = _debugOverdrawMaterial,
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

    DebugOverdrawPass _debugOverdrawPass;

    /// <inheritdoc/>
    public override void Create()
    {
        var settings = new DebugOverdrawSettings(true, overdrawDetectionThreshold);
        _debugOverdrawPass = new(debugOverdrawMaterial, settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_debugOverdrawPass);
    }

    protected override void Dispose(bool disposing)
    {
        _debugOverdrawPass?.Dispose();
    }
}


