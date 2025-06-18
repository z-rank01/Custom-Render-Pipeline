// OverdrawOverlappedVisualization.shader
Shader "RenderingDebugger/OverdrawOverlappedVisualization"
{
    Properties
    {
//        _OverdrawOverlappedOriginalTexture ("Original Texture", 2D) = "white" {}
//        _OverdrawCounters ("Overdraw Count Texture", 2D) = "black" {}
//        _OverdrawOverlappedIntensity ("Overdraw Intensity", Range(0, 1)) = 0.7
//        _OverdrawOverlappedDisplayHeightRatio ("Display Height Ratio", Range(0, 1)) = 0.5
//        _OverdrawOverlappedThreshold ("Max Overdraw Threshold", Float) = 20.0
//        _OverdrawOverlappedMinColor ("Min Overdraw Color", Color) = (0, 0, 0, 1)
//        _OverdrawOverlappedMaxColor ("Max Overdraw Color", Color) = (1, 0, 0, 1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "OverdrawVisualization"
            
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_OverdrawOverlappedOriginalColoTexture);
            SAMPLER(sampler_OverdrawOverlappedOriginalColoTexture);
            
            TEXTURE2D(_OverdrawOverlappedCounters);
            SAMPLER(sampler_OverdrawOverlappedCounters);

            float _OverdrawOverlappedIntensity;
            float _OverdrawOverlappedDisplayHeightRatio;
            float _OverdrawOverlappedThreshold;
            float4 _OverdrawOverlappedMinColor;
            float4 _OverdrawOverlappedMaxColor;
            
            // 生成全屏三角形的顶点着色器
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 生成全屏三角形
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.uv = uv;
                
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
                #endif
                
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float4 originalColor = SAMPLE_TEXTURE2D(_OverdrawOverlappedOriginalColoTexture, sampler_OverdrawOverlappedOriginalColoTexture, input.uv);
                float overdrawCounts = SAMPLE_TEXTURE2D(_OverdrawOverlappedCounters, sampler_OverdrawOverlappedCounters, input.uv).r;
                
                // 将计数值从 [0,1] 范围转换回实际计数
                // 假设每次 overdraw 在 shader 中写入 0.05，那么实际计数 = overdrawCount / 0.05
                float actualOverdrawCount = overdrawCounts / 0.05;
                float normalizedCount = saturate(actualOverdrawCount / _OverdrawOverlappedThreshold);
                float4 rangeMapColor = lerp(_OverdrawOverlappedMinColor, _OverdrawOverlappedMaxColor, normalizedCount);
                float4 finalColor;
                return rangeMapColor;
                if (input.uv.y > (1.0 - _OverdrawOverlappedDisplayHeightRatio) && actualOverdrawCount > 0.001)
                {
                    finalColor = lerp(originalColor, rangeMapColor, _OverdrawOverlappedIntensity);
                }
                else
                {
                    finalColor = originalColor;
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}