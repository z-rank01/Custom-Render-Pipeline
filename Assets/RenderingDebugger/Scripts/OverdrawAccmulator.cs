using System;
using UnityEngine;
using UnityEngine.Rendering;

// OverdrawManager.cs
namespace RenderingDebugger.Scripts
{
    public class OverdrawAccumulator
    {
        private static OverdrawAccumulator _instance;
        public static OverdrawAccumulator Instance
        {
            get
            {
                _instance ??= new OverdrawAccumulator();
                return _instance;
            }
        }

        private ComputeBuffer _overdrawCountBuffer;
        private int _screenWidth, _screenHeight;
        private ComputeShader _overdrawVisualizationCs;
        private bool isOverdrawEnabled { get; set; } = false;

        private OverdrawAccumulator() { }

        public RenderTexture overdrawVisualizationTexture { get; private set; }

        // 检查分辨率是否变化，如果变化则重新初始化
        public bool CheckResolution(int screenWidth, int screenHeight)
        {
            return !isOverdrawEnabled || _screenWidth != screenWidth || _screenHeight != screenHeight;
        }

        private void UpdateComputeBufferAndVariables(int screenWidth, int screenHeight)
        {
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            RecreateBuffersAndTextures();
            UpdateShaderGlobals();
        }

        private void RecreateBuffersAndTextures()
        {
            // 释放旧的缓冲区
            _overdrawCountBuffer?.Release();

            // 创建新的计数缓冲区
            _overdrawCountBuffer = new ComputeBuffer(
                _screenWidth * _screenHeight,
                sizeof(uint),
                ComputeBufferType.Default,
                ComputeBufferMode.Immutable);

            // 释放旧的可视化纹理
            if (overdrawVisualizationTexture != null)
                overdrawVisualizationTexture.Release();

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

        public void EnableOverdrawDetection(int screenWidth, int screenHeight, ComputeShader visualizationCS)
        {
            isOverdrawEnabled = true;
            _overdrawVisualizationCs = visualizationCS;

            // 创建或重新分配计数缓冲区
            UpdateComputeBufferAndVariables(screenWidth, screenHeight);
            LogShaderKeywordStatus();
        }

        public void SetupUAVBinding(CommandBuffer cmd)
        {
            if (_overdrawCountBuffer == null) return;
            cmd.SetRandomWriteTarget(1, _overdrawCountBuffer);
            // Debug.Log("Set UAV binding for overdraw counter buffer");
        }

        // 添加清零计数器的单独方法
        public void ClearData(CommandBuffer cmd)
        {
            if (_overdrawCountBuffer == null) return;

            // 清零计数器 - 在渲染开始前调用
            uint[] zeros = new uint[_overdrawCountBuffer.count];
            _overdrawCountBuffer.SetData(zeros);

            // Debug.Log($"Cleared overdraw counters: {_overdrawCountBuffer.count} elements");
        }

        public void GenerateVisualization(CommandBuffer cmd, uint maxOverdrawThreshold = 20, Color minColor = default, Color maxColor = default)
        {
            if (!isOverdrawEnabled || _overdrawVisualizationCs == null)
                return;

            // 如果没有提供颜色，使用默认值
            if (minColor == default) minColor = Color.black;
            if (maxColor == default) maxColor = Color.red;

            // Debug.Log($"Buffer valid: {_overdrawCountBuffer != null && _overdrawCountBuffer.IsValid()}");
            // Debug.Log($"Buffer count: {_overdrawCountBuffer?.count}");

            int kernelIndex = _overdrawVisualizationCs.FindKernel("VisualizeOverdraw");

            cmd.SetComputeBufferParam(_overdrawVisualizationCs, kernelIndex, DebugConstant.OverdrawCountBufferId, _overdrawCountBuffer);
            cmd.SetComputeTextureParam(_overdrawVisualizationCs, kernelIndex, DebugConstant.OverdrawVisualizationTextureId, overdrawVisualizationTexture);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenWidthId, _screenWidth);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeScreenHeightId, _screenHeight);
            cmd.SetComputeIntParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeThresholdId, (int)maxOverdrawThreshold);
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMinColorId, new Vector4(minColor.r, minColor.g, minColor.b, minColor.a));
            cmd.SetComputeVectorParam(_overdrawVisualizationCs, DebugConstant.OverdrawComputeMaxColorId, new Vector4(maxColor.r, maxColor.g, maxColor.b, maxColor.a));

            int threadGroupsX = Mathf.CeilToInt(_screenWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_screenHeight / 8.0f);
            cmd.DispatchCompute(_overdrawVisualizationCs, kernelIndex, threadGroupsX, threadGroupsY, 1);

            // Debug.Log($"Dispatched overdraw visualization: {threadGroupsX}x{threadGroupsY} thread groups");
        }

        public void DisableOverdrawDetection()
        {
            isOverdrawEnabled = false;
            Shader.SetGlobalInt(DebugConstant.OverdrawEnableId, 0);
            Shader.DisableKeyword(DebugConstant.OverdrawEnableKeyword);
            Debug.Log("Overdraw detection disabled");
        }

        private void LogShaderKeywordStatus()
        {
            Debug.Log($"_EnableOverdrawDetection: {Shader.GetGlobalInt(DebugConstant.OverdrawEnableId)} " +
                      $"\n _OverdrawScreenWidth: {Shader.GetGlobalInt(DebugConstant.OverdrawVisualizationScreenWidthId)} " +
                      $"\n _OverdrawScreenHeight: {Shader.GetGlobalInt(DebugConstant.OverdrawVisualizationScreenHeightId)}");
        }

        public ComputeBuffer GetOverdrawCountBuffer() => _overdrawCountBuffer;

        public uint[] GetOverdrawData()
        {
            if (_overdrawCountBuffer == null) return null;

            uint[] data = new uint[_overdrawCountBuffer.count];
            _overdrawCountBuffer.GetData(data);

            // 调试：检查数据
            int nonZeroCount = 0;
            uint maxValue = 0;
            foreach (var t in data)
            {
                if (t <= 0) continue;
                nonZeroCount++;
                maxValue = (uint)Mathf.Max(maxValue, t);
            }
            Debug.Log($"Overdraw data: {nonZeroCount} non-zero pixels, max value: {maxValue}");

            return data;
        }

        public void Cleanup()
        {
            _overdrawCountBuffer?.Release();
            _overdrawCountBuffer = null;

            if (overdrawVisualizationTexture)
            {
                overdrawVisualizationTexture.Release();
                overdrawVisualizationTexture = null;
            }

            isOverdrawEnabled = false;
            Debug.Log("Overdraw accumulator cleaned up");
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

            Debug.Log($"Buffer Stats: Total pixels: {data.Length}, Non-zero pixels: {nonZeroCount}, Max value: {maxValue}, Total sum: {totalSum}");

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
    }
}
