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
            float _OverdrawDisplayHeightRatio;
            
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
                // calculate the debug region size and position
                float debugRegionSize = _OverdrawDisplayHeightRatio;
                float2 debugRegionStart = float2(1.0 - debugRegionSize, 0.0);
                float2 debugRegionEnd = float2(1.0, debugRegionSize);

                // check if the current pixel is within the debug region
                if (input.uv.x >= debugRegionStart.x && input.uv.x <= debugRegionEnd.x &&
                    input.uv.y >= debugRegionStart.y && input.uv.y <= debugRegionEnd.y)
                {
                    finalColor = lerp(originalColor, overdrawColor, _OverdrawIntensity); // return the overdraw color if in the debug region
                }
                else
                {
                    finalColor =  originalColor; // outside the debug region, return the original color
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
