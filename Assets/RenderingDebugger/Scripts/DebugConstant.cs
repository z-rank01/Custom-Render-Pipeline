using UnityEngine;

namespace RenderingDebugger.Scripts
{
    public static class DebugConstant
    {
        // Debug Depth 着色器属性ID
        public static readonly int DebugColorInputId = Shader.PropertyToID("_DebugColorInput");
        public static readonly int DebugDisplayHeightRatioId = Shader.PropertyToID("_DebugDisplayHeightRatio");
        public static readonly int DebugSaturationThresholdId = Shader.PropertyToID("_DebugSaturationThreshold");

        // Debug Overdraw Detection 着色器属性ID
        public static readonly int OverdrawCountBufferId = Shader.PropertyToID("_OverdrawCounters");
        public static readonly int OverdrawEnableId = Shader.PropertyToID("_EnableOverdrawDetection");
        public const string OverdrawEnableKeyword = "ENABLE_OVERDRAW_DETECTION";

        // Overdraw 可视化纹理相关
        public static readonly int OverdrawVisualizationTextureRId = Shader.PropertyToID("_OverdrawVisualizationTexture_R");
        public static readonly int OverdrawVisualizationTextureGId = Shader.PropertyToID("_OverdrawVisualizationTexture_G");
        public static readonly int OverdrawVisualizationTextureBId = Shader.PropertyToID("_OverdrawVisualizationTexture_B");
        public static readonly int OverdrawVisualizationTextureAId = Shader.PropertyToID("_OverdrawVisualizationTexture_A");
        public static readonly int OverdrawVisualizationScreenWidthId = Shader.PropertyToID("_OverdrawScreenWidth");
        public static readonly int OverdrawVisualizationScreenHeightId = Shader.PropertyToID("_OverdrawScreenHeight");

        // Overdraw Compute Shader 相关
        public static readonly int OverdrawComputeScreenWidthId = Shader.PropertyToID("_ComputeScreenWidth");
        public static readonly int OverdrawComputeScreenHeightId = Shader.PropertyToID("_ComputeScreenHeight");
        public static readonly int OverdrawComputeThresholdId = Shader.PropertyToID("_MaxOverdrawThreshold");
        public static readonly int OverdrawComputeMinColorId = Shader.PropertyToID("_MinOverdrawColor");
        public static readonly int OverdrawComputeMaxColorId = Shader.PropertyToID("_MaxOverdrawColor");

        // Overdraw Blend - 纹理模式
        public static readonly int OverdrawBlendTextureRId = Shader.PropertyToID("_OverdrawTexture_R");
        public static readonly int OverdrawBlendTextureGId = Shader.PropertyToID("_OverdrawTexture_G");
        public static readonly int OverdrawBlendTextureBId = Shader.PropertyToID("_OverdrawTexture_B");
        public static readonly int OverdrawBlendTextureAId = Shader.PropertyToID("_OverdrawTexture_A");

        // Overdraw Blend - 直接读取 Buffer 模式
        public static readonly int OverdrawBlendDirectBufferReadId = Shader.PropertyToID("_OverdrawDirectBufferRead");
        public static readonly int OverdrawBlendScreenWidthId = Shader.PropertyToID("_OverdrawBlendScreenWidth");
        public static readonly int OverdrawBlendScreenHeightId = Shader.PropertyToID("_OverdrawBlendScreenHeight");
        public static readonly int OverdrawBlendThresholdId = Shader.PropertyToID("_OverdrawBlendThreshold");
        public static readonly int OverdrawBlendMinColorId = Shader.PropertyToID("_OverdrawBlendMinColor");
        public static readonly int OverdrawBlendMaxColorId = Shader.PropertyToID("_OverdrawBlendMaxColor");
        
        // Overdraw Blend - 通用
        public static readonly int OverdrawBlendOriginalTextureId = Shader.PropertyToID("_OriginalTexture");
        public static readonly int OverdrawBlendOverdrawIntensityId = Shader.PropertyToID("_OverdrawIntensity");
        public static readonly int OverdrawBlendDisplayHeightRatioId = Shader.PropertyToID("_OverdrawDisplayHeightRatio");
        
        // Overdraw 模式关键字
        public const string OverdrawDirectBufferReadKeyword = "OVERDRAW_DIRECT_BUFFER_READ";
    }
}
