Shader "CustomRenderPipeline/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "LightMode" = "CustomLit"
        }
        
        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/HLSL/Lit.hlsl"
            ENDCG
        }
    }
}
