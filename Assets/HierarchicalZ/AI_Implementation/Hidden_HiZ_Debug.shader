Shader "Hidden/HiZ/Debug"
{
    Properties{}
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_HiZTexture); SAMPLER(sampler_HiZTexture);
            float4 _DebugParams; // x: mip

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v){ v2f o; o.pos = TransformObjectToHClip(v.vertex.xyz); o.uv = v.uv; return o; }
            float4 frag(v2f i):SV_Target
            {
                uint2 size; uint mipCount;
                _HiZTexture.GetDimensions(0, size.x, size.y, mipCount);
                int mip = clamp((int)_DebugParams.x, 0, (int)mipCount-1);
                float d = SAMPLE_TEXTURE2D_LOD(_HiZTexture, sampler_HiZTexture, i.uv, mip).r;
                return float4(d.xxx,1);
            }
            ENDHLSL
        }
    }
}
