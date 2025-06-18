using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    public class DebugOverdrawFragmentDetection : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OverdrawDetectionSettings
        {
            [Header("Detection Settings")]
            public bool enableOverdrawDetection = true;
            public ComputeShader overdrawVisualizationCS;

            [Header("Visualization Settings")]
            public Material overdrawDisplayMaterial;
            [Range(0f, 1f)] public float overdrawDisplayHeightRatio = 0.5f;
            [Range(0f, 1f)] public float overdrawIntensity = 0.7f;
            [Range(1, 50)] public uint maxOverdrawThreshold = 20;

            [Header("Range map Colors")]
            [ColorUsage(false)] public Color minOverdrawColor = Color.gray;
            [ColorUsage(false)] public Color maxOverdrawColor = Color.white;

            [Header("Performance")]
            public bool updateEveryFrame = true;
        }

        public OverdrawDetectionSettings settings = new();
        private DebugOverdrawFragmentDetectionPass _debugOverdrawFragmentDetectionPass;

        public override void Create()
        {
            _debugOverdrawFragmentDetectionPass = new DebugOverdrawFragmentDetectionPass(settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.enableOverdrawDetection && settings.overdrawVisualizationCS != null && settings.overdrawDisplayMaterial != null)
            {
                renderer.EnqueuePass(_debugOverdrawFragmentDetectionPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debugOverdrawFragmentDetectionPass.Dispose();
            }
        }


        private class DebugOverdrawFragmentDetectionPass : ScriptableRenderPass
        {
            private readonly OverdrawDetectionSettings _settings;
            private RTHandle _tempColorTarget;
            private const string ProfilerTag = "Fragment Overdraw Detection";
            private bool _isInitialized = false;

            public DebugOverdrawFragmentDetectionPass(OverdrawDetectionSettings settings)
            {
                _settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var cameraData = renderingData.cameraData;
                int currentWidth = cameraData.camera.pixelWidth;
                int currentHeight = cameraData.camera.pixelHeight;

                // 启用 overdraw 检测
                if (!_isInitialized || DebugOverdrawFragmentAccumulator.Instance.CheckResolution(currentWidth, currentHeight))
                {
                    DebugOverdrawFragmentAccumulator.Instance.EnableOverdrawDetection(
                        currentWidth,
                        currentHeight,
                        _settings.overdrawVisualizationCS
                    );
                    DebugOverdrawFragmentAccumulator.Instance.SetupUavBinding(cmd);
                    _isInitialized = true;
                }
                DebugOverdrawFragmentAccumulator.Instance.ClearData(cmd);

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
                    DebugOverdrawFragmentAccumulator.Instance.GenerateVisualization(cmd, _settings.maxOverdrawThreshold, _settings.minOverdrawColor, _settings.maxOverdrawColor);

                    // 3. 应用 overdraw 可视化到相机目标
                    var overdrawTexture = DebugOverdrawFragmentAccumulator.Instance.overdrawVisualizationTexture;
                    if (overdrawTexture != null)
                    {
                        // 设置材质参数
                        var material = _settings.overdrawDisplayMaterial;
                        material.SetTexture(DebugConstant.OverdrawBlendOverdrawTextureId, overdrawTexture);
                        material.SetTexture(DebugConstant.OverdrawBlendOriginalTextureId, _tempColorTarget);
                        material.SetFloat(DebugConstant.OverdrawBlendOverdrawIntensityId, _settings.overdrawIntensity);
                        material.SetFloat(DebugConstant.OverdrawBlendDisplayHeightRatioId,  _settings.overdrawDisplayHeightRatio);

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
                DebugOverdrawFragmentAccumulator.Instance.DisableOverdrawDetection();
                _isInitialized = false;
            }
        }
    }
    
    public class DebugOverdrawFragmentAccumulator
    {
        private static DebugOverdrawFragmentAccumulator _instance;
        public static DebugOverdrawFragmentAccumulator Instance
        {
            get
            {
                _instance ??= new DebugOverdrawFragmentAccumulator();
                return _instance;
            }
        }

        private ComputeBuffer _overdrawCountBuffer;
        private int _screenWidth, _screenHeight;
        private ComputeShader _overdrawVisualizationCs;
        private bool isOverdrawEnabled { get; set; } = false;
        public RenderTexture overdrawVisualizationTexture { get; private set; }


        #region Public Interface

        /// <summary>
        /// Check if the current resolution is different from the stored one.
        /// </summary>
        /// <param name="screenWidth">Width of current camera view</param>
        /// <param name="screenHeight">Height of current camera view</param>
        /// <returns>True if resolution changed, False otherwise</returns>
        public bool CheckResolution(int screenWidth, int screenHeight)
        {
            return !isOverdrawEnabled || _screenWidth != screenWidth || _screenHeight != screenHeight;
        }

        /// <summary>
        /// Enable overdraw detection and set up the necessary compute shader and buffers.
        /// </summary>
        /// <param name="screenWidth">Width of current camera view</param>
        /// <param name="screenHeight">Height of current camera view</param>
        /// <param name="visualizationCS">Compute Shader for visualization calculation</param>
        public void EnableOverdrawDetection(int screenWidth, int screenHeight, ComputeShader visualizationCS)
        {
            isOverdrawEnabled = true;
            _overdrawVisualizationCs = visualizationCS;

            // 创建或重新分配计数缓冲区
            UpdateComputeBufferAndVariables(screenWidth, screenHeight);
            LogShaderKeywordStatus();
        }

        /// <summary>
        /// Set up the UAV binding for the overdraw count buffer.
        /// </summary>
        /// <param name="cmd">Command buffer currently used</param>
        public void SetupUavBinding(CommandBuffer cmd)
        {
            if (_overdrawCountBuffer == null) return;
            cmd.SetRandomWriteTarget(1, _overdrawCountBuffer);
            // Debug.Log("Set UAV binding for overdraw counter buffer");
        }

        /// <summary>
        /// Clear the overdraw count buffer data.
        /// </summary>
        /// <param name="cmd">Command buffer currently used</param>
        public void ClearData(CommandBuffer cmd)
        {
            if (_overdrawCountBuffer == null) return;

            // 清零计数器 - 在渲染开始前调用
            uint[] zeros = new uint[_overdrawCountBuffer.count];
            _overdrawCountBuffer.SetData(zeros);

            // Debug.Log($"Cleared overdraw counters: {_overdrawCountBuffer.count} elements");
        }

        /// <summary>
        /// Generate the overdraw visualization texture using the provided compute shader.
        /// </summary>
        /// <param name="cmd">Command buffer currently used</param>
        /// <param name="maxOverdrawThreshold">Threshold of overdraw counts, will affect the visualization of overdraws</param>
        /// <param name="minColor">Color of minimum overdraw pixels</param>
        /// <param name="maxColor">Color of maximum overdraw pixels</param>
        public void GenerateVisualization(CommandBuffer cmd, uint maxOverdrawThreshold = 20, Color minColor = default,
            Color maxColor = default)
        {
            if (!isOverdrawEnabled || _overdrawVisualizationCs == null)
                return;

            // 如果没有提供颜色，使用默认值
            if (minColor == default) minColor = Color.black;
            if (maxColor == default) maxColor = Color.red;

            // Debug.Log($"Buffer valid: {_overdrawCountBuffer != null && _overdrawCountBuffer.IsValid()}");
            // Debug.Log($"Buffer count: {_overdrawCountBuffer?.count}");

            int kernelIndex = _overdrawVisualizationCs.FindKernel("VisualizeOverdraw");

            cmd.SetComputeBufferParam(_overdrawVisualizationCs, kernelIndex, DebugConstant.OverdrawCountBufferId,
                _overdrawCountBuffer);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex,
                DebugConstant.OverdrawVisualizationTextureId, overdrawVisualizationTexture);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenWidthId, _screenWidth);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenHeightId,
                _screenHeight);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeThresholdId,
                (int)maxOverdrawThreshold);
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMinColorId,
                new Vector4(minColor.r, minColor.g, minColor.b, minColor.a));
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMaxColorId,
                new Vector4(maxColor.r, maxColor.g, maxColor.b, maxColor.a));

            int threadGroupsX = Mathf.CeilToInt(_screenWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_screenHeight / 8.0f);
            cmd.DispatchCompute(_overdrawVisualizationCs, kernelIndex, threadGroupsX, threadGroupsY, 1);

            // Debug.Log($"Dispatched overdraw visualization: {threadGroupsX}x{threadGroupsY} thread groups");
        }

        public void DisableOverdrawDetection()
        {
            isOverdrawEnabled = false;
            Cleanup();
            Shader.SetGlobalInt(DebugConstant.OverdrawEnableId, 0);
            Shader.DisableKeyword(DebugConstant.OverdrawEnableKeyword);
            Debug.Log("Overdraw detection disabled");
        }

        #endregion

        #region Private Method

        private void UpdateComputeBufferAndVariables(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            RecreateBuffersAndTextures();
            UpdateShaderGlobals();
        }

        private void RecreateBuffersAndTextures()
        {
            // 释放旧的缓冲区和纹理
            Cleanup();

            // 创建新的计数缓冲区
            _overdrawCountBuffer = new ComputeBuffer(
                _screenWidth * _screenHeight,
                sizeof(uint),
                ComputeBufferType.Default,
                ComputeBufferMode.Immutable);

            // 创建新的可视化纹理
            overdrawVisualizationTexture = new RenderTexture(_screenWidth, _screenHeight, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization"
            };
            overdrawVisualizationTexture.Create();
        }

        private void UpdateShaderGlobals()
        {
            Shader.SetGlobalInt(DebugConstant.OverdrawVisualizationScreenWidthId, _screenWidth);
            Shader.SetGlobalInt(DebugConstant.OverdrawVisualizationScreenHeightId, _screenHeight);
            Shader.SetGlobalInt(DebugConstant.OverdrawEnableId, 1);
            Shader.EnableKeyword(DebugConstant.OverdrawEnableKeyword);
        }

        private void Cleanup()
        {
            _overdrawCountBuffer?.Release();
            _overdrawCountBuffer = null;

            if (overdrawVisualizationTexture)
            {
                overdrawVisualizationTexture.Release();
                overdrawVisualizationTexture = null;
            }

            Debug.Log("Overdraw accumulator cleaned up");
        }

        #endregion

        #region Debug

        private void LogShaderKeywordStatus()
        {
            Debug.Log($"_EnableOverdrawDetection: {Shader.GetGlobalInt(DebugConstant.OverdrawEnableId)} " +
                      $"\n _OverdrawScreenWidth: {Shader.GetGlobalInt(DebugConstant.OverdrawVisualizationScreenWidthId)} " +
                      $"\n _OverdrawScreenHeight: {Shader.GetGlobalInt(DebugConstant.OverdrawVisualizationScreenHeightId)}");
        }

        public void DebugBufferContents()
        {
            if (_overdrawCountBuffer == null)
            {
                Debug.Log("Overdraw buffer is null!");
                return;
            }

            // 读取缓冲区数据
            uint[] data = new uint[_overdrawCountBuffer.count];
            _overdrawCountBuffer.GetData(data);

            // 统计非零数据
            int nonZeroCount = 0;
            uint maxValue = 0;
            uint totalSum = 0;

            foreach (var t in data)
            {
                if (t <= 0) continue;
                nonZeroCount++;
                maxValue = Math.Max(maxValue, t);
                totalSum += t;
            }

            Debug.Log(
                $"Buffer Stats: Total pixels: {data.Length}, Non-zero pixels: {nonZeroCount}, Max value: {maxValue}, Total sum: {totalSum}");

            // 显示前几个非零值的位置
            if (nonZeroCount <= 0) return;
            Debug.Log("First few non-zero values:");
            int count = 0;
            for (int i = 0; i < data.Length && count < 10; i++)
            {
                if (data[i] <= 0) continue;
                int x = i % _screenWidth;
                int y = i / _screenWidth;
                Debug.Log($"  Pixel ({x}, {y}): {data[i]}");
                count++;
            }
        }

        #endregion
    }
}



