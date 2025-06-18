using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderingDebugger.Scripts
{
    [DisallowMultipleRendererFeature("Debug Fragment Overdraw")]
    [Tooltip("Render feature for debugging fragment overdraw information.")]
    public class DebugOverdrawFragmentDetection : ScriptableRendererFeature
    {
        public OverdrawDetectionSettings settings = new();
        private DebugOverdrawFragmentDetectionPass _debugOverdrawFragmentDetectionPass;
        
        [System.Serializable]
        public class OverdrawDetectionSettings
        {
            [Header("Detection Settings")]
            public bool enableOverdrawDetection = true;
            public ComputeShader overdrawVisualizationCS;
            
            [Header("Rendering Mode")]
            [Tooltip("Use direct buffer read in shader (better performance) or texture-based approach (better compatibility)")]
            public bool useDirectBufferRead = true;

            [Header("Visualization Settings")]
            public Material overdrawDisplayMaterial;
            [Range(0f, 1f)] public float overdrawDisplayHeightRatio = 0.5f;
            [Range(0f, 1f)] public float overdrawIntensity = 0.7f;
            [Range(1, 50)] public uint maxOverdrawThreshold = 20;

            [Header("Range map Colors")]
            [ColorUsage(false)] public Color minOverdrawColor = Color.gray;
            [ColorUsage(false)] public Color maxOverdrawColor = Color.white;
        }
        
        #region Renderer Feature Implementation
        
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
        
        #endregion

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
                    _isInitialized = true;
                }
                DebugOverdrawFragmentAccumulator.Instance.SetupUavBinding(cmd);
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
                    // 2. 启用 overdraw 检测
                    

                    var material = _settings.overdrawDisplayMaterial;
                    if (_settings.useDirectBufferRead)
                    {
                        // 2a. 直接读取 Buffer 模式
                        var overdrawCountBuffer = DebugOverdrawFragmentAccumulator.Instance.OverdrawCountBuffer;
                        
                        if (overdrawCountBuffer != null && overdrawCountBuffer.IsValid())
                        {
                            // 启用直接读取 Buffer 关键字
                            // DebugOverdrawFragmentAccumulator.Instance.SetupUavBinding(cmd);
                            material.EnableKeyword(DebugConstant.OverdrawDirectBufferReadKeyword);
                            material.SetBuffer(DebugConstant.OverdrawBlendDirectBufferReadId, overdrawCountBuffer);
                            material.SetInt(DebugConstant.OverdrawBlendScreenWidthId, DebugOverdrawFragmentAccumulator.Instance.ScreenWidth);
                            material.SetInt(DebugConstant.OverdrawBlendScreenHeightId, DebugOverdrawFragmentAccumulator.Instance.ScreenHeight);
                            material.SetInt(DebugConstant.OverdrawBlendThresholdId, (int)_settings.maxOverdrawThreshold);
                            material.SetVector(DebugConstant.OverdrawBlendMinColorId, _settings.minOverdrawColor);
                            material.SetVector(DebugConstant.OverdrawBlendMaxColorId, _settings.maxOverdrawColor);
                        }
                        else
                        {
                            Debug.LogWarning("Overdraw count buffer is null or invalid!");
                        }
                    }
                    else
                    {
                        // 2b. 纹理模式
                        material.DisableKeyword(DebugConstant.OverdrawDirectBufferReadKeyword);
                        
                        // 生成 overdraw 可视化纹理
                        DebugOverdrawFragmentAccumulator.Instance.GenerateVisualization(cmd, _settings.maxOverdrawThreshold, _settings.minOverdrawColor, _settings.maxOverdrawColor);

                        // 应用 overdraw 可视化到相机目标
                        var overdrawTextureR = DebugOverdrawFragmentAccumulator.Instance.OverdrawVisualizationTextureR;
                        var overdrawTextureG = DebugOverdrawFragmentAccumulator.Instance.OverdrawVisualizationTextureG;
                        var overdrawTextureB = DebugOverdrawFragmentAccumulator.Instance.OverdrawVisualizationTextureB;
                        var overdrawTextureA = DebugOverdrawFragmentAccumulator.Instance.OverdrawVisualizationTextureA;

                        if (overdrawTextureR != null && overdrawTextureG != null && overdrawTextureB != null && overdrawTextureA != null)
                        {
                            // 设置材质参数
                            material.SetTexture(DebugConstant.OverdrawBlendTextureRId, overdrawTextureR);
                            material.SetTexture(DebugConstant.OverdrawBlendTextureGId, overdrawTextureG);
                            material.SetTexture(DebugConstant.OverdrawBlendTextureBId, overdrawTextureB);
                            material.SetTexture(DebugConstant.OverdrawBlendTextureAId, overdrawTextureA);
                        }
                        else
                        {
                            Debug.LogWarning("Overdraw visualization textures are null!");
                        }
                    }

                    // 3. 设置通用材质参数并绘制
                    material.SetTexture(DebugConstant.OverdrawBlendOriginalTextureId, _tempColorTarget);
                    material.SetFloat(DebugConstant.OverdrawBlendOverdrawIntensityId, _settings.overdrawIntensity);
                    material.SetFloat(DebugConstant.OverdrawBlendDisplayHeightRatioId, _settings.overdrawDisplayHeightRatio);

                    // 绘制全屏 quad
                    cmd.SetRenderTarget(cameraColorTarget);
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
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
        private ComputeShader _overdrawVisualizationCs;
        private bool _isOverdrawEnabled = false;

        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        public ComputeBuffer OverdrawCountBuffer { get; private set; }
        public RenderTexture OverdrawVisualizationTextureR { get; private set; }
        public RenderTexture OverdrawVisualizationTextureG { get; private set; }
        public RenderTexture OverdrawVisualizationTextureB { get; private set; }
        public RenderTexture OverdrawVisualizationTextureA { get; private set; }



        #region Public Interface

        /// <summary>
        /// Check if the current resolution is different from the stored one.
        /// </summary>
        /// <param name="screenWidth">Width of current camera view</param>
        /// <param name="screenHeight">Height of current camera view</param>
        /// <returns>True if resolution changed, False otherwise</returns>
        public bool CheckResolution(int screenWidth, int screenHeight)
        {
            return !_isOverdrawEnabled || ScreenWidth != screenWidth || ScreenHeight != screenHeight;
        }

        /// <summary>
        /// Enable overdraw detection and set up the necessary compute shader and buffers.
        /// </summary>
        /// <param name="screenWidth">Width of current camera view</param>
        /// <param name="screenHeight">Height of current camera view</param>
        /// <param name="visualizationCS">Compute Shader for visualization calculation</param>
        public void EnableOverdrawDetection(int screenWidth, int screenHeight, ComputeShader visualizationCS)
        {
            _isOverdrawEnabled = true;
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
            if (OverdrawCountBuffer == null) return;
            cmd.SetRandomWriteTarget(1, OverdrawCountBuffer);
            // Debug.Log("Set UAV binding for overdraw counter buffer");
        }

        /// <summary>
        /// Clear the overdraw count buffer data.
        /// </summary>
        /// <param name="cmd">Command buffer currently used</param>
        public void ClearData(CommandBuffer cmd)
        {
            if (OverdrawCountBuffer == null) return;

            // 清零计数器 - 在渲染开始前调用
            int[] zeros = new int[OverdrawCountBuffer.count];
            OverdrawCountBuffer.SetData(zeros);

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
            if (!_isOverdrawEnabled || _overdrawVisualizationCs == null)
                return;

            // 如果没有提供颜色，使用默认值
            if (minColor == default) minColor = Color.black;
            if (maxColor == default) maxColor = Color.red;

            // Debug.Log($"Buffer valid: {_overdrawCountBuffer != null && _overdrawCountBuffer.IsValid()}");
            // Debug.Log($"Buffer count: {_overdrawCountBuffer?.count}");

            int kernelIndex = _overdrawVisualizationCs.FindKernel("VisualizeOverdraw");

            cmd.SetComputeBufferParam(_overdrawVisualizationCs, kernelIndex, DebugConstant.OverdrawCountBufferId,
                OverdrawCountBuffer);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex,
                DebugConstant.OverdrawVisualizationTextureRId, OverdrawVisualizationTextureR);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex,
                DebugConstant.OverdrawVisualizationTextureGId, OverdrawVisualizationTextureG);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex,
                DebugConstant.OverdrawVisualizationTextureBId, OverdrawVisualizationTextureB);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex,
                DebugConstant.OverdrawVisualizationTextureAId, OverdrawVisualizationTextureA);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenWidthId, ScreenWidth);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenHeightId,
                ScreenHeight);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeThresholdId,
                (int)maxOverdrawThreshold);
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMinColorId,
                new Vector4(minColor.r, minColor.g, minColor.b, minColor.a));
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMaxColorId,
                new Vector4(maxColor.r, maxColor.g, maxColor.b, maxColor.a));

            int threadGroupsX = Mathf.CeilToInt(ScreenWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(ScreenHeight / 8.0f);
            cmd.DispatchCompute(_overdrawVisualizationCs, kernelIndex, threadGroupsX, threadGroupsY, 1);

            // Debug.Log($"Dispatched overdraw visualization: {threadGroupsX}x{threadGroupsY} thread groups");
        }

        public void DisableOverdrawDetection()
        {
            _isOverdrawEnabled = false;
            Cleanup();
            Shader.SetGlobalInt(DebugConstant.OverdrawEnableId, 0);
            Shader.DisableKeyword(DebugConstant.OverdrawEnableKeyword);
            Debug.Log("Overdraw detection disabled");
        }

        #endregion

        #region Private Method

        private void UpdateComputeBufferAndVariables(int screenWidth, int screenHeight)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            RecreateBuffersAndTextures();
            UpdateShaderGlobals();
        }

        private void RecreateBuffersAndTextures()
        {
            // 释放旧的缓冲区和纹理
            Cleanup();

            // 创建新的计数缓冲区
            OverdrawCountBuffer = new ComputeBuffer(
                ScreenWidth * ScreenHeight,
                sizeof(int),
                ComputeBufferType.Default,
                ComputeBufferMode.Immutable);

            // 创建四张单通道可视化纹理（纹理模式需要）
            CreateVisualizationTextures();
        }
        
        private void CreateVisualizationTextures()
        {
            OverdrawVisualizationTextureR = new RenderTexture(ScreenWidth, ScreenHeight, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization_R"
            };
            OverdrawVisualizationTextureR.Create();

            OverdrawVisualizationTextureG = new RenderTexture(ScreenWidth, ScreenHeight, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization_G"
            };
            OverdrawVisualizationTextureG.Create();

            OverdrawVisualizationTextureB = new RenderTexture(ScreenWidth, ScreenHeight, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization_B"
            };
            OverdrawVisualizationTextureB.Create();

            OverdrawVisualizationTextureA = new RenderTexture(ScreenWidth, ScreenHeight, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization_A"
            };
            OverdrawVisualizationTextureA.Create();
        }

        private void UpdateShaderGlobals()
        {
            Shader.SetGlobalInt(DebugConstant.OverdrawVisualizationScreenWidthId, ScreenWidth);
            Shader.SetGlobalInt(DebugConstant.OverdrawVisualizationScreenHeightId, ScreenHeight);
            Shader.SetGlobalInt(DebugConstant.OverdrawEnableId, 1);
            Shader.EnableKeyword(DebugConstant.OverdrawEnableKeyword);
        }

        private void Cleanup()
        {
            OverdrawCountBuffer?.Release();
            OverdrawCountBuffer = null;

            if (OverdrawVisualizationTextureR)
            {
                OverdrawVisualizationTextureR.Release();
                OverdrawVisualizationTextureR = null;
            }

            if (OverdrawVisualizationTextureG)
            {
                OverdrawVisualizationTextureG.Release();
                OverdrawVisualizationTextureG = null;
            }

            if (OverdrawVisualizationTextureB)
            {
                OverdrawVisualizationTextureB.Release();
                OverdrawVisualizationTextureB = null;
            }

            if (OverdrawVisualizationTextureA)
            {
                OverdrawVisualizationTextureA.Release();
                OverdrawVisualizationTextureA = null;
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
            if (OverdrawCountBuffer == null)
            {
                Debug.Log("Overdraw buffer is null!");
                return;
            }

            // 读取缓冲区数据
            int[] data = new int[OverdrawCountBuffer.count];
            OverdrawCountBuffer.GetData(data);

            // 统计非零数据
            int nonZeroCount = 0;
            int maxValue = 0;
            int totalSum = 0;

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
                int x = i % ScreenWidth;
                int y = i / ScreenWidth;
                Debug.Log($"  Pixel ({x}, {y}): {data[i]}");
                count++;
            }
        }

        #endregion
    }
}
