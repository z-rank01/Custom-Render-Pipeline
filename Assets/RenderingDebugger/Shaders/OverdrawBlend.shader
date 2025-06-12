Shader "RenderingDebugger/OverdrawBlend"
{
    SubShader
    {
        Tags { "RenderType"="Overlay" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            Name "OverdrawDisplay"
            
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
                uint vertexID : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_OverdrawTexture);
            SAMPLER(sampler_OverdrawTexture);
            TEXTURE2D(_OriginalTexture);
            SAMPLER(sampler_OriginalTexture);
            float _OverdrawIntensity;
            int _BlendMode;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
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
                float4 originalColor = SAMPLE_TEXTURE2D(_OriginalTexture, sampler_OriginalTexture, input.uv);
                float4 overdrawColor = SAMPLE_TEXTURE2D(_OverdrawTexture, sampler_OverdrawTexture, input.uv);
                
                // 不同的混合模式
                float4 finalColor;
                
                if (_BlendMode == 0) // Overlay
                {
                    // 叠加模式：保留原图，叠加 overdraw 热度图
                    finalColor = lerp(originalColor, overdrawColor, _OverdrawIntensity * overdrawColor.a);
                    finalColor.a = originalColor.a;
                }
                else if (_BlendMode == 1) // Replace
                {
                    // 替换模式：完全显示 overdraw 热度图
                    finalColor = overdrawColor;
                }
                else if (_BlendMode == 2) // Split Screen
                {
                    // 分屏模式：左半边原图，右半边 overdraw
                    finalColor = input.uv.x < 0.5 ? originalColor : overdrawColor;
                }
                else // Additive
                {
                    // 加法模式：原图 + overdraw 热度图
                    finalColor = originalColor + overdrawColor * _OverdrawIntensity;
                    finalColor.a = originalColor.a;
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
