using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Overdraw Debug Output")]
[Tooltip("The debug output for overdraw in the rendering pipeline.")]
public class DebugOverdraw : ScriptableRendererFeature
{
    [SerializeField] private Material debugOverdrawMaterial;

    class DebugOverdrawPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "Overdraw Debug Output";
        private readonly ProfilingSampler _profilingSampler = new(ProfilerTag);
        private RTHandle _tempColorTarget;
        private Material _debugOverdrawMaterial;

        public DebugOverdrawPass(Material debugOverdrawMaterial)
        {
            _debugOverdrawMaterial = debugOverdrawMaterial;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTargetDescriptor.depthBufferBits = 24; // 需要深度缓冲区
            RenderingUtils.ReAllocateIfNeeded(ref _tempColorTarget, cameraTargetDescriptor,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempOverdrawColor");

            ConfigureTarget(_tempColorTarget);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_debugOverdrawMaterial == null)
                return;

            var cmd = CommandBufferPool.Get(ProfilerTag);
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                // Set overdraw base color
                cmd.SetGlobalColor("_OverdrawColor", Color.gray);
                // cmd.SetRenderTarget(_tempColorTarget);

                // Create drawing settings with sorting and filtering
                var sortingSettings = CreateSortingSettings(ref renderingData);
                var drawingSettings = CreateDrawingSettings(ref renderingData, sortingSettings);
                var filteringSettings = CreateFilteringSettings(ref renderingData);
                var renderStateBlock = CreateRenderStateBlock();

                // Draw renderers with the debug overdraw material
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
                // context.ExecuteCommandBuffer(cmd);
                // cmd.Clear();

                // // Blit the temporary color target to the camera color target
                // var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                // cmd.SetRenderTarget(cameraColorTarget);
                // cmd.Blit(_tempColorTarget, cameraColorTarget);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
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
            };
            var renderStateBlock = new RenderStateBlock()
            {
                // cull off to detect overdraw in all directions
                rasterState = new RasterState(cullingMode: CullMode.Off, offsetUnits: 0, offsetFactor: 0),
                blendState = new BlendState { blendState0 = additiveColorBlendState },
                depthState = new DepthState(false, CompareFunction.LessEqual),
                mask = RenderStateMask.Raster | RenderStateMask.Blend | RenderStateMask.Depth
            };
            return renderStateBlock;
        }
    }

    DebugOverdrawPass _debugOverdrawPass;

    /// <inheritdoc/>
    public override void Create()
    {
        _debugOverdrawPass = new(debugOverdrawMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
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


