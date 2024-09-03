Shader "CustomRenderPipeline/SimpleLit"
{
    Properties
    {
        _AlbedoMap ("Albedo Map", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "white" {}
        
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _AmbientColor ("Ambient Color", Color) = (1, 1, 1, 1)
        _Shininess ("Specular Intensity", Float) = 0.3
    }
    SubShader
    {
        Tags
        {
            "LightMode" = "SimpleLit"
        }
        
        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma target 3.5
            
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/HLSL/SimpleLit.hlsl"
            ENDCG
        }
    }
}
