using UnityEngine;

namespace RenderingDebugger.Scripts
{
    public static class DebugConstant
    {
        // Debug Depth 着色器属性ID
        public static readonly int DebugColorInputId = Shader.PropertyToID("_DebugColorInput");
        public static readonly int DebugDisplayHeightRatioId = Shader.PropertyToID("_DebugDisplayHeightRatio");
        public static readonly int DebugScreenWidthId = Shader.PropertyToID("_DebugScreenWidth");
        public static readonly int DebugScreenHeightId = Shader.PropertyToID("_DebugScreenHeight");
        public static readonly int DebugSaturationThresholdId = Shader.PropertyToID("_DebugSaturationThreshold");

        // Debug Overdraw 着色器属性ID
        public static readonly int DebugOverdrawColorId = Shader.PropertyToID("_OverdrawColor");
    }
}
