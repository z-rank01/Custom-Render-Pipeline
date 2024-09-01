Shader "CustomRenderPipeline/UnlitTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        
        [Toggle(_CLIPPING)]
        _Clipping ("Alpha Clip", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend ("Src Blend", Float) = 1
        
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend ("Dst Blend", Float) = 0
        
        [Enum(off, 0, On, 1)]
        _ZWrite ("Z Write", Float) = 1
    }
    SubShader
    {
       Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma shader_feature _CLIPPING
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Assets/HLSL/UnlitTransparent.hlsl"
            ENDCG
        }
    }
}
