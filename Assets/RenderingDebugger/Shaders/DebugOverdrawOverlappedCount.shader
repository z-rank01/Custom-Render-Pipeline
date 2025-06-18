Shader "RenderingDebugger/DebugOverdrawOverlappedCount"
{
    // SubShader
    // {
    //     Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
    //     Pass
    //     {
    //         Name "OverdrawCount"
            
    //         HLSLPROGRAM
    //         #pragma vertex vert
    //         #pragma fragment frag
            
    //         #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    //         #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
    //         struct Attributes
    //         {
    //             float4 positionOS : POSITION;
    //         };
            
    //         struct Varyings
    //         {
    //             float4 positionCS : SV_POSITION;
    //             float4 screenPos : TEXCOORD0;
    //         };
            
    //         TEXTURE2D(_OverdrawOverlappedOriginalDepthTexture);
    //         SAMPLER(sampler_OverdrawOverlappedOriginalDepthTexture);
            
    //         Varyings vert(Attributes input)
    //         {
    //             Varyings output;
    //             output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    //             output.screenPos = ComputeScreenPos(output.positionCS);
    //             return output;
    //         }
            
    //         float frag(Varyings input) : SV_Target
    //         {
    //             float2 screenUV = input.screenPos.xy / input.screenPos.w;
    //             float currentDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
    //             float originalDepth = LinearEyeDepth(SAMPLE_TEXTURE2D(
    //                 _OverdrawOverlappedOriginalDepthTexture, 
    //                 sampler_OverdrawOverlappedOriginalDepthTexture, screenUV).r, _ZBufferParams);
    
    //             float overdrawMask = currentDepth > originalDepth ? 1.0f : 0.0f;
    //             return float4(0.1, 0.1, 0.1, 1.0) * overdrawMask;
    //         }
    //         ENDHLSL
    //     }
    // }

   SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
    
       Pass
       {
           Name "OverdrawCount"
        
        //    ZTest Greater  // 只渲染深度测试失败的像素
        //    ZWrite Off
        //    Blend One One  // 累加混合
        
           HLSLPROGRAM
           #pragma vertex vert
           #pragma fragment frag
        
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
           struct Attributes
           {
               float4 positionOS : POSITION;
           };
        
           struct Varyings
           {
               float4 positionCS : SV_POSITION;
           };
        
           Varyings vert(Attributes input)
           {
               Varyings output;
               output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
               return output;
           }
        
           float4 frag(Varyings input) : SV_Target
           {
               return float4(0.1, 0.1, 0.1, 1.0);
           }
           ENDHLSL
       }
   }
}
