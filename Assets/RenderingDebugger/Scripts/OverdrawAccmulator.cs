using System;
using UnityEngine;
using UnityEngine.Rendering;

// OverdrawManager.cs
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
    private RenderTexture _overdrawVisualizationTexture;
    private bool _isOverdrawEnabled = false;
    private int _screenWidth, _screenHeight;
    private ComputeShader _overdrawVisualizationCS;

    private OverdrawAccumulator() { }

    public bool IsOverdrawEnabled => _isOverdrawEnabled;
    public RenderTexture OverdrawVisualizationTexture => _overdrawVisualizationTexture;

    // 检查分辨率是否变化，如果变化则重新初始化
    public bool CheckResolution(int screenWidth, int screenHeight)
    {
        return !_isOverdrawEnabled || _screenWidth != screenWidth || _screenHeight != screenHeight;
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
        if (_overdrawVisualizationTexture != null)
            _overdrawVisualizationTexture.Release();

        // 创建新的可视化纹理
        _overdrawVisualizationTexture = new RenderTexture(_screenWidth, _screenHeight, 0, RenderTextureFormat.ARGB32)
        {
            enableRandomWrite = true,
            name = "OverdrawVisualization"
        };
        _overdrawVisualizationTexture.Create();
    }

    private void UpdateShaderGlobals()
    {
        Shader.SetGlobalBuffer("_OverdrawCounters", _overdrawCountBuffer);
        Shader.SetGlobalInt("_OverdrawScreenWidth", _screenWidth);
        Shader.SetGlobalInt("_OverdrawScreenHeight", _screenHeight);
        Shader.SetGlobalInt("_EnableOverdrawDetection", 1);
        Shader.EnableKeyword("ENABLE_OVERDRAW_DETECTION");
    }

    public void EnableOverdrawDetection(int screenWidth, int screenHeight, ComputeShader visualizationCS)
    {
        _isOverdrawEnabled = true;
        _overdrawVisualizationCS = visualizationCS;

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
        if (!_isOverdrawEnabled || _overdrawVisualizationCS == null)
            return;

        // 如果没有提供颜色，使用默认值
        if (minColor == default) minColor = Color.black;
        if (maxColor == default) maxColor = Color.red;

        Debug.Log($"Buffer valid: {_overdrawCountBuffer != null && _overdrawCountBuffer.IsValid()}");
        Debug.Log($"Buffer count: {_overdrawCountBuffer?.count}");

        int kernelIndex = _overdrawVisualizationCS.FindKernel("VisualizeOverdraw");

        _overdrawVisualizationCS.SetBuffer(kernelIndex, "_OverdrawCounters", _overdrawCountBuffer);
        cmd.SetComputeBufferParam(_overdrawVisualizationCS, kernelIndex, "_OverdrawCounters", _overdrawCountBuffer);
        cmd.SetComputeTextureParam(_overdrawVisualizationCS, kernelIndex, "_OverdrawVisualizationTexture", _overdrawVisualizationTexture);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_ScreenWidth", _screenWidth);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_ScreenHeight", _screenHeight);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_MaxOverdrawThreshold", (int)maxOverdrawThreshold);
        cmd.SetComputeVectorParam(_overdrawVisualizationCS, "_MinOverdrawColor", new Vector4(minColor.r, minColor.g, minColor.b, minColor.a));
        cmd.SetComputeVectorParam(_overdrawVisualizationCS, "_MaxOverdrawColor", new Vector4(maxColor.r, maxColor.g, maxColor.b, maxColor.a));

        int threadGroupsX = Mathf.CeilToInt(_screenWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(_screenHeight / 8.0f);
        cmd.DispatchCompute(_overdrawVisualizationCS, kernelIndex, threadGroupsX, threadGroupsY, 1);

        // Debug.Log($"Dispatched overdraw visualization: {threadGroupsX}x{threadGroupsY} thread groups");
    }

    public void DisableOverdrawDetection()
    {
        _isOverdrawEnabled = false;
        Shader.SetGlobalInt("_EnableOverdrawDetection", 0);
        Shader.DisableKeyword("ENABLE_OVERDRAW_DETECTION");
        Debug.Log("Overdraw detection disabled");
    }

    public void LogShaderKeywordStatus()
    {
        bool keywordEnabled = Shader.IsKeywordEnabled("ENABLE_OVERDRAW_DETECTION");
        Debug.Log($"ENABLE_OVERDRAW_DETECTION keyword enabled: {keywordEnabled}");

        // 检查全局参数
        Debug.Log($"_EnableOverdrawDetection: {Shader.GetGlobalInt("_EnableOverdrawDetection")}");
        Debug.Log($"_OverdrawScreenWidth: {Shader.GetGlobalInt("_OverdrawScreenWidth")}");
        Debug.Log($"_OverdrawScreenHeight: {Shader.GetGlobalInt("_OverdrawScreenHeight")}");
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
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 0)
            {
                nonZeroCount++;
                maxValue = (uint)Mathf.Max(maxValue, data[i]);
            }
        }
        Debug.Log($"Overdraw data: {nonZeroCount} non-zero pixels, max value: {maxValue}");

        return data;
    }

    public void Cleanup()
    {
        _overdrawCountBuffer?.Release();
        _overdrawCountBuffer = null;

        if (_overdrawVisualizationTexture != null)
        {
            _overdrawVisualizationTexture.Release();
            _overdrawVisualizationTexture = null;
        }

        _isOverdrawEnabled = false;
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

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 0)
            {
                nonZeroCount++;
                maxValue = Math.Max(maxValue, data[i]);
                totalSum += data[i];
            }
        }

        Debug.Log($"Buffer Stats: Total pixels: {data.Length}, Non-zero pixels: {nonZeroCount}, Max value: {maxValue}, Total sum: {totalSum}");

        // 显示前几个非零值的位置
        if (nonZeroCount > 0)
        {
            Debug.Log("First few non-zero values:");
            int count = 0;
            for (int i = 0; i < data.Length && count < 10; i++)
            {
                if (data[i] > 0)
                {
                    int x = i % _screenWidth;
                    int y = i / _screenWidth;
                    Debug.Log($"  Pixel ({x}, {y}): {data[i]}");
                    count++;
                }
            }
        }
    }
}
