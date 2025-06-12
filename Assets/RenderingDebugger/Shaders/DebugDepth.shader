Shader "RenderingDebugger/DebugDepth"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    
    TEXTURE2D_X(_DebugColorInput);
    SAMPLER(sampler_DebugColorInput);
    float _DebugDisplayHeightRatio;
    int _DebugSaturationThreshold;

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
        // sample the original color from the debug input texture
        float4 originalColor = SAMPLE_TEXTURE2D_X(_DebugColorInput, sampler_DebugColorInput, input.uv);
        
        // calculate the debug region size and position
        float debugRegionSize = _DebugDisplayHeightRatio;
        float2 debugRegionStart = float2(1.0 - debugRegionSize, 0.0);
        float2 debugRegionEnd = float2(1.0, debugRegionSize);
        
        // check if the current pixel is within the debug region
        if (input.uv.x >= debugRegionStart.x && input.uv.x <= debugRegionEnd.x &&
            input.uv.y >= debugRegionStart.y && input.uv.y <= debugRegionEnd.y)
        {
            float depth = SampleSceneDepth(input.uv);
            float linearDepth = LinearEyeDepth(depth, _ZBufferParams);
            linearDepth = saturate(linearDepth / _DebugSaturationThreshold);
            
            float3 depthColor;
            depthColor.r = smoothstep(0.5, 1.0, linearDepth);
            depthColor.g = smoothstep(0.0, 0.5, linearDepth) * (1.0 - smoothstep(0.5, 1.0, linearDepth));
            depthColor.b = 1.0 - smoothstep(0.0, 0.5, linearDepth);
            
            return float4(depthColor, 1.0);
        }
        // if not in the debug region, return the original color
        else
        {
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
