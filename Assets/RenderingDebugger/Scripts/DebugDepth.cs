using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    internal struct DebugOutputSettings
    {
        public bool EnableDebugOutput;
        public float DisplayHeightRatio;
        public int DepthDetectionThreshold;

        public DebugOutputSettings(bool enableDebug, float displayHeightRatio, int depthDetectionThreshold)
        {
            // Initialize the settings with default values
            EnableDebugOutput = enableDebug;
            DisplayHeightRatio = displayHeightRatio;
            DepthDetectionThreshold = depthDetectionThreshold;
        }
    }

    [DisallowMultipleRendererFeature("Depth Debug Output")]
    [Tooltip("The debug output for depth information in the rendering pipeline.")]
    public class DebugDepth : ScriptableRendererFeature
    {
        [SerializeField] private Material debugDepthMaterial;
        [SerializeField, Range(0.1f, 1f)] private float displayHeightRatio = 0.5f; // Ratio of the display height for the debug output
        [SerializeField] private int depthDetectionThreshold = 20; // Threshold for depth detection

        private class DepthOutputRenderPass : ScriptableRenderPass
        {
            private readonly Material _debugSplitMaterial;
            private readonly DebugOutputSettings _settings;
            private const string ProfilerTag = "Debug Depth";
            private readonly ProfilingSampler _profilingSampler = new(ProfilerTag);

            // 临时渲染目标
            private RTHandle _tempRenderTarget;

            public DepthOutputRenderPass(DebugOutputSettings settings, Material debugSplitMaterial)
            {
                _debugSplitMaterial = debugSplitMaterial;
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                cameraTargetDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempRenderTarget, cameraTargetDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DebugDepthTarget");
                ConfigureInput(ScriptableRenderPassInput.Color);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                var colorTarget = cameraData.renderer.cameraColorTargetHandle;

                var cmd = CommandBufferPool.Get(ProfilerTag);
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    cmd.Blit(colorTarget.rt, _tempRenderTarget);

                    cmd.SetGlobalTexture(DebugConstant.DebugColorInputId, _tempRenderTarget);
                    cmd.SetGlobalFloat(DebugConstant.DebugDisplayHeightRatioId, _settings.DisplayHeightRatio);
                    cmd.SetGlobalInt(DebugConstant.DebugSaturationThresholdId, _settings.DepthDetectionThreshold);
                    cmd.SetRenderTarget(colorTarget);
                    cmd.DrawProcedural(Matrix4x4.identity, _debugSplitMaterial, 0, MeshTopology.Triangles, 3, 1);
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
        }

        private DepthOutputRenderPass _depthOutputPass;

        public override void Create()
        {
            _depthOutputPass = new DepthOutputRenderPass(
                new DebugOutputSettings(true, displayHeightRatio, depthDetectionThreshold),
                debugDepthMaterial
            )
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (debugDepthMaterial != null)
            {
                renderer.EnqueuePass(_depthOutputPass);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            _depthOutputPass?.Dispose();
        }
    }
}