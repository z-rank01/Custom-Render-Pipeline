Shader "RenderingDebugger/DebugOverdrawOverlapped"
{
    Properties
    {
        _OverdrawColor ("Overdraw Color", Color) = (0.1, 0.1, 0.1, 1)
    }
   SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
    
       Pass
       {
           Name "OverdrawCount"
        
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

           float4 _OverdrawColor;
        
           Varyings vert(Attributes input)
           {
               Varyings output;
               output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
               return output;
           }
        
           float4 frag(Varyings input) : SV_Target
           {
               return _OverdrawColor;
           }
           ENDHLSL
       }
   }
}
