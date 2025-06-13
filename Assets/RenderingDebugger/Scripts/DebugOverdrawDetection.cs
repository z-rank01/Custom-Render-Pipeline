using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    public class DebugOverdrawDetection : ScriptableRendererFeature
    {
        [System.Serializable]
        public enum OverdrawDisplayMode
        {
            Overlay,      // 叠加模式
            Replace,      // 替换模式
            SplitScreen,  // 分屏模式
            Additive     // 加法模式
        }

        [System.Serializable]
        public class OverdrawDetectionSettings
        {
            [Header("Detection Settings")]
            public bool enableOverdrawDetection = true;
            public ComputeShader overdrawVisualizationCS;

            [Header("Visualization Settings")]
            public Material overdrawDisplayMaterial;
            public OverdrawDisplayMode displayMode = OverdrawDisplayMode.Overlay;
            [Range(0f, 1f)] public float overdrawIntensity = 0.7f;
            [Range(1, 50)] public uint maxOverdrawThreshold = 20;

            [Header("Heatmap Colors")]
            [ColorUsage(false)] public Color minOverdrawColor = Color.gray;
            [ColorUsage(false)] public Color maxOverdrawColor = Color.white;

            [Header("Performance")]
            public bool updateEveryFrame = true;
        }

        public OverdrawDetectionSettings settings = new OverdrawDetectionSettings();
        private DebugOverdrawDetectionPass _debugOverdrawDetectionPass;

        public override void Create()
        {
            _debugOverdrawDetectionPass = new DebugOverdrawDetectionPass(settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.enableOverdrawDetection && settings.overdrawVisualizationCS != null && settings.overdrawDisplayMaterial != null)
            {
                renderer.EnqueuePass(_debugOverdrawDetectionPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debugOverdrawDetectionPass.Dispose();
            }
        }


        private class DebugOverdrawDetectionPass : ScriptableRenderPass
        {
            private readonly OverdrawDetectionSettings _settings;
            private RTHandle _tempColorTarget;
            private const string ProfilerTag = "Overdraw Detection";
            private bool _isInitialized = false;

            public DebugOverdrawDetectionPass(OverdrawDetectionSettings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                int currentWidth = cameraData.camera.pixelWidth;
                int currentHeight = cameraData.camera.pixelHeight;

                // 启用 overdraw 检测
                if (!_isInitialized || OverdrawAccumulator.Instance.CheckResolution(currentWidth, currentHeight))
                {
                    OverdrawAccumulator.Instance.EnableOverdrawDetection(
                        currentWidth,
                        currentHeight,
                        _settings.overdrawVisualizationCS
                    );
                    OverdrawAccumulator.Instance.SetupUAVBinding(cmd);
                    _isInitialized = true;
                }
                OverdrawAccumulator.Instance.ClearData(cmd);

                // 创建临时颜色目标用于保存原始图像
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref _tempColorTarget, descriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempOverdrawColor");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(ProfilerTag);
                var cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

                using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTag)))
                {
                    // 1. 保存当前相机颜色目标
                    cmd.Blit(cameraColorTarget.rt, _tempColorTarget);

                    // 2. 生成 overdraw 可视化
                    // if (_settings.updateEveryFrame || Time.frameCount % 30 == 0) // 可选：降低更新频率
                    // {
                    // OverdrawAccumulator.Instance.SetupBuffer(cmd);
                    // OverdrawAccumulator.Instance.SetupUAVBinding(cmd);
                    OverdrawAccumulator.Instance.GenerateVisualization(cmd, _settings.maxOverdrawThreshold, _settings.minOverdrawColor, _settings.maxOverdrawColor);
                    // }

                    // 3. 应用 overdraw 可视化到相机目标
                    var overdrawTexture = OverdrawAccumulator.Instance.overdrawVisualizationTexture;
                    if (overdrawTexture != null)
                    {
                        // 设置材质参数
                        var material = _settings.overdrawDisplayMaterial;
                        material.SetTexture("_OverdrawTexture", overdrawTexture);
                        material.SetTexture("_OriginalTexture", _tempColorTarget);
                        material.SetFloat("_OverdrawIntensity", _settings.overdrawIntensity);
                        material.SetInt("_BlendMode", (int)_settings.displayMode);

                        // 绘制全屏 quad
                        cmd.SetRenderTarget(cameraColorTarget);
                        cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
                    }
                    else
                    {
                        Debug.LogWarning("Overdraw visualization texture is null!");
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                // 调试输出 overdraw 缓冲区内容 - 移到 ProfilingScope 外部
                // if (_settings.updateEveryFrame || Time.frameCount % 30 == 0)
                // {
                //     OverdrawAccumulator.Instance.DebugBufferContents();
                // }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }

            public void Dispose()
            {
                _tempColorTarget?.Release();
                OverdrawAccumulator.Instance.DisableOverdrawDetection();
                OverdrawAccumulator.Instance.Cleanup();
                _isInitialized = false;
            }
        }
    }
}



