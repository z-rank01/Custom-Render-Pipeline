Shader "RenderingDebugger/DebugOverdrawFragmentBlend"
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
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ OVERDRAW_DIRECT_BUFFER_READ
            
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
            
            #ifdef OVERDRAW_DIRECT_BUFFER_READ
                // 直接读取 Buffer 模式
                #if defined(SHADER_API_D3D11) || defined(SHADER_API_D3D12) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
                    #define STRUCTURED_BUFFER_SUPPORTED
                    RWStructuredBuffer<int> _OverdrawDirectBufferRead : register(u1);
                #endif
                
                int _OverdrawBlendScreenWidth;
                int _OverdrawBlendScreenHeight;
                int _OverdrawBlendThreshold;
                float4 _OverdrawBlendMinColor;
                float4 _OverdrawBlendMaxColor;
                
                float3 GetLinearHeatmapColor(int overdrawCount, int maxThreshold)
                {
                    if (overdrawCount <= 1)
                        return _OverdrawBlendMinColor.rgb;
                    
                    // 计算归一化的overdraw值 (0到1之间)
                    float normalizedCount = saturate((float)overdrawCount / (float)maxThreshold);
                    
                    // 在最小颜色和最大颜色之间进行线性插值
                    float3 color = lerp(_OverdrawBlendMinColor.rgb, _OverdrawBlendMaxColor.rgb, normalizedCount);
                    return color;
                }
            #else
                // 纹理模式
                TEXTURE2D(_OverdrawTexture_R);
                SAMPLER(sampler_OverdrawTexture_R);
                TEXTURE2D(_OverdrawTexture_G);
                SAMPLER(sampler_OverdrawTexture_G);
                TEXTURE2D(_OverdrawTexture_B);
                SAMPLER(sampler_OverdrawTexture_B);
                TEXTURE2D(_OverdrawTexture_A);
                SAMPLER(sampler_OverdrawTexture_A);
            #endif
            
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
                float4 overdrawColor = float4(0, 0, 0, 1);
                
                #ifdef OVERDRAW_DIRECT_BUFFER_READ

                    #ifdef STRUCTURED_BUFFER_SUPPORTED
                        float2 screenPos = input.uv;
                        int2 pixelCoord = int2(floor(screenPos.x * _OverdrawBlendScreenWidth), 
                                              floor(screenPos.y * _OverdrawBlendScreenHeight));
                        
                        int index = pixelCoord.y * _OverdrawBlendScreenWidth + pixelCoord.x;
                        int bufferSize = _OverdrawBlendScreenWidth * _OverdrawBlendScreenHeight;
                        if (index >= 0 && index < bufferSize)
                        {
                            int overdrawCount = _OverdrawDirectBufferRead[index];
                            float3 heatmapColor = GetLinearHeatmapColor(overdrawCount, _OverdrawBlendThreshold);
                            overdrawColor = float4(heatmapColor, 1.0);
                        }
                    #else
                        // 不支持 StructuredBuffer 的平台，显示警告颜色
                        overdrawColor = float4(1, 0, 1, 1); // 紫色表示不支持
                    #endif
                #else
                    // 纹理模式
                    // 从四张单通道纹理采样并组合成四通道颜色
                    float r = SAMPLE_TEXTURE2D(_OverdrawTexture_R, sampler_OverdrawTexture_R, input.uv).r;
                    float g = SAMPLE_TEXTURE2D(_OverdrawTexture_G, sampler_OverdrawTexture_G, input.uv).r;
                    float b = SAMPLE_TEXTURE2D(_OverdrawTexture_B, sampler_OverdrawTexture_B, input.uv).r;
                    float a = SAMPLE_TEXTURE2D(_OverdrawTexture_A, sampler_OverdrawTexture_A, input.uv).r;
                    overdrawColor = float4(r, g, b, a);
                #endif
                
                // 计算调试区域
                float debugRegionSize = _OverdrawDisplayHeightRatio;
                float2 debugRegionStart = float2(1.0 - debugRegionSize, 0.0);
                float2 debugRegionEnd = float2(1.0, debugRegionSize);

                // 检查当前像素是否在调试区域内
                if (input.uv.x >= debugRegionStart.x && input.uv.x <= debugRegionEnd.x &&
                    input.uv.y >= debugRegionStart.y && input.uv.y <= debugRegionEnd.y)
                {
                    return lerp(originalColor, overdrawColor, _OverdrawIntensity);
                }
                else
                {
                    return originalColor;
                }
            }
            ENDHLSL
        }
    }
}
