Shader "RenderingDebugger/DepthDepth"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    
    TEXTURE2D_X(_DebugColorInput);
    SAMPLER(sampler_DebugColorInput);

    float _DebugDisplayHeightRatio;
    int _DebugScreenWidth;
    int _DebugScreenHeight;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings vert(Attributes input)
    {
        Varyings output;
        
        // 使用类似 URP Blitter 的方式创建全屏三角形
        // 顶点ID: 0->(-1,-1), 1->(3,-1), 2->(-1,3)
        float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
        output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
        
        // 处理不同平台的UV坐标差异
        #if UNITY_UV_STARTS_AT_TOP
        output.uv = float2(uv.x, 1.0 - uv.y);
        #else
        output.uv = uv;
        #endif
        
        return output;
    }

    float4 frag(Varyings input) : SV_Target
    {
        // 采样原始颜色
        float4 originalColor = SAMPLE_TEXTURE2D_X(_DebugColorInput, sampler_DebugColorInput, input.uv);
        
        // 计算右上角区域的边界
        float debugRegionSize = _DebugDisplayHeightRatio;
        float2 debugRegionStart = float2(1.0 - debugRegionSize, 0.0);
        float2 debugRegionEnd = float2(1.0, debugRegionSize);
        
        // 检查当前像素是否在右上角的调试区域内
        if (input.uv.x >= debugRegionStart.x && input.uv.x <= debugRegionEnd.x &&
            input.uv.y >= debugRegionStart.y && input.uv.y <= debugRegionEnd.y)
        {
            // 在调试区域内，显示深度信息
            float depth = SampleSceneDepth(input.uv);
            
            // 将深度值线性化以便更好地可视化
            float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
            linearDepth = saturate(linearDepth / 20.0); // 调整这个值来改变深度范围
            
            // 创建热度图颜色映射
            float3 depthColor;
            depthColor.r = smoothstep(0.5, 1.0, linearDepth);
            depthColor.g = smoothstep(0.0, 0.5, linearDepth) * (1.0 - smoothstep(0.5, 1.0, linearDepth));
            depthColor.b = 1.0 - smoothstep(0.0, 0.5, linearDepth);
            
            return float4(depthColor, 1.0);
        }
        else
        {
            // 在调试区域外，显示原始颜色
            return originalColor;
        }
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Depth Debug"
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
