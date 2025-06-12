using UnityEngine;
using UnityEngine.Rendering;

// OverdrawManager.cs
public static class OverdrawAccumulator
{
    private static ComputeBuffer _overdrawCountBuffer;
    private static RenderTexture _overdrawVisualizationTexture;
    private static bool _isOverdrawEnabled = false;
    private static int _screenWidth, _screenHeight;
    private static ComputeShader _overdrawVisualizationCS;

    public static bool IsOverdrawEnabled => _isOverdrawEnabled;
    public static RenderTexture OverdrawVisualizationTexture => _overdrawVisualizationTexture;

    public static void EnableOverdrawDetection(int screenWidth, int screenHeight, ComputeShader visualizationCS)
    {
        _isOverdrawEnabled = true;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _overdrawVisualizationCS = visualizationCS;

        // 创建或重新分配计数缓冲区
        if (_overdrawCountBuffer == null || _overdrawCountBuffer.count != screenWidth * screenHeight)
        {
            _overdrawCountBuffer?.Release();
            _overdrawCountBuffer = new ComputeBuffer(screenWidth * screenHeight, sizeof(uint));
        }

        // 创建或重新分配可视化纹理
        if (_overdrawVisualizationTexture == null ||
            _overdrawVisualizationTexture.width != screenWidth ||
            _overdrawVisualizationTexture.height != screenHeight)
        {
            if (_overdrawVisualizationTexture != null)
                _overdrawVisualizationTexture.Release();

            _overdrawVisualizationTexture = new RenderTexture(screenWidth, screenHeight, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                name = "OverdrawVisualization"
            };
            _overdrawVisualizationTexture.Create();
        }

        // 清零计数器
        // uint[] zeros = new uint[screenWidth * screenHeight];
        // _overdrawCountBuffer.SetData(zeros);

        // 设置全局着色器参数
        Shader.SetGlobalBuffer("_OverdrawCounters", _overdrawCountBuffer);
        Shader.SetGlobalInt("_OverdrawScreenWidth", screenWidth);
        Shader.SetGlobalInt("_OverdrawScreenHeight", screenHeight);
        Shader.SetGlobalInt("_EnableOverdrawDetection", 1);
        Shader.EnableKeyword("ENABLE_OVERDRAW_DETECTION");

        Debug.Log($"Overdraw detection enabled: {screenWidth}x{screenHeight}");
        LogShaderKeywordStatus();
    }

    // 添加清零计数器的单独方法
    public static void ClearCounters(CommandBuffer cmd)
    {
        if (_overdrawCountBuffer == null) return;

        // 清零计数器 - 在渲染开始前调用
        uint[] zeros = new uint[_overdrawCountBuffer.count];
        _overdrawCountBuffer.SetData(zeros);

        // Debug.Log($"Cleared overdraw counters: {_overdrawCountBuffer.count} elements");
    }

    public static void GenerateVisualization(CommandBuffer cmd, uint maxOverdrawThreshold = 20)
    {
        if (!_isOverdrawEnabled || _overdrawVisualizationCS == null)
            return;

        int kernelIndex = _overdrawVisualizationCS.FindKernel("VisualizeOverdraw");

        cmd.SetComputeBufferParam(_overdrawVisualizationCS, kernelIndex, "_OverdrawCounters", _overdrawCountBuffer);
        cmd.SetComputeTextureParam(_overdrawVisualizationCS, kernelIndex, "_OverdrawVisualizationTexture", _overdrawVisualizationTexture);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_ScreenWidth", _screenWidth);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_ScreenHeight", _screenHeight);
        cmd.SetComputeIntParam(_overdrawVisualizationCS, "_MaxOverdrawThreshold", (int)maxOverdrawThreshold);

        int threadGroupsX = Mathf.CeilToInt(_screenWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(_screenHeight / 8.0f);
        cmd.DispatchCompute(_overdrawVisualizationCS, kernelIndex, threadGroupsX, threadGroupsY, 1);

        // Debug.Log($"Dispatched overdraw visualization: {threadGroupsX}x{threadGroupsY} thread groups");
    }

    public static void DisableOverdrawDetection()
    {
        _isOverdrawEnabled = false;
        Shader.SetGlobalInt("_EnableOverdrawDetection", 0);
        Shader.DisableKeyword("ENABLE_OVERDRAW_DETECTION");
        Debug.Log("Overdraw detection disabled");
    }

    public static void LogShaderKeywordStatus()
    {
        bool keywordEnabled = Shader.IsKeywordEnabled("ENABLE_OVERDRAW_DETECTION");
        Debug.Log($"ENABLE_OVERDRAW_DETECTION keyword enabled: {keywordEnabled}");

        // 检查全局参数
        Debug.Log($"_EnableOverdrawDetection: {Shader.GetGlobalInt("_EnableOverdrawDetection")}");
        Debug.Log($"_OverdrawScreenWidth: {Shader.GetGlobalInt("_OverdrawScreenWidth")}");
        Debug.Log($"_OverdrawScreenHeight: {Shader.GetGlobalInt("_OverdrawScreenHeight")}");
    }

    public static ComputeBuffer GetOverdrawCountBuffer() => _overdrawCountBuffer;

    public static uint[] GetOverdrawData()
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

    public static void Cleanup()
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
}
