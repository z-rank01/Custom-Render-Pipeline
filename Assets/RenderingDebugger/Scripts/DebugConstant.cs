using UnityEngine;

namespace RenderingDebugger.Scripts
{
    public static class DebugConstant
    {
        // Debug Depth 着色器属性ID
        public static readonly int DebugColorInputId = Shader.PropertyToID("_DebugColorInput");
        public static readonly int DebugDisplayHeightRatioId = Shader.PropertyToID("_DebugDisplayHeightRatio");
        public static readonly int DebugSaturationThresholdId = Shader.PropertyToID("_DebugSaturationThreshold");

        // Debug Overdraw Simulation 着色器属性ID
        public static readonly int DebugOverdrawColorId = Shader.PropertyToID("_DebugOverdrawColor");
        public static readonly int DebugOverdrawResultId = Shader.PropertyToID("_DebugOverdrawResult");

        // Debug Overdraw Detection 着色器属性ID
        public static readonly int OverdrawCountBufferId = Shader.PropertyToID("_OverdrawCounters");
        public static readonly int OverdrawEnableId = Shader.PropertyToID("_EnableOverdrawDetection");
        public const string OverdrawEnableKeyword = "ENABLE_OVERDRAW_DETECTION";

        public static readonly int OverdrawVisualizationTextureId = Shader.PropertyToID("_OverdrawVisualizationTexture");
        public static readonly int OverdrawVisualizationScreenWidthId = Shader.PropertyToID("_OverdrawScreenWidth");
        public static readonly int OverdrawVisualizationScreenHeightId = Shader.PropertyToID("_OverdrawScreenHeight");

        public static readonly int OverdrawComputeScreenWidthId = Shader.PropertyToID("_ComputeScreenWidth");
        public static readonly int OverdrawComputeScreenHeightId = Shader.PropertyToID("_ComputeScreenHeight");
        public static readonly int OverdrawComputeThresholdId = Shader.PropertyToID("_MaxOverdrawThreshold");
        public static readonly int OverdrawComputeMinColorId = Shader.PropertyToID("_MinOverdrawColor");
        public static readonly int OverdrawComputeMaxColorId = Shader.PropertyToID("_MaxOverdrawColor");

        public static readonly int OverdrawBlendOverdrawTextureId = Shader.PropertyToID("_OverdrawTexture");
        public static readonly int OverdrawBlendOriginalTextureId = Shader.PropertyToID("_OriginalTexture");
        public static readonly int OverdrawBlendOverdrawIntensityId = Shader.PropertyToID("_OverdrawIntensity");
        public static readonly int OverdrawBlendDisplayHeightRatioId = Shader.PropertyToID("_OverdrawDisplayHeightRatio");
    }
}
