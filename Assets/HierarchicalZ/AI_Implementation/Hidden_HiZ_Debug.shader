Shader "Hidden/HiZ/Debug"
{
    Properties{}
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always 
            ZWrite Off
            Cull Off 
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D(_DebugHiZTexture); SAMPLER(sampler_DebugHiZTexture);
            TEXTURE2D(_DebugColorInput); SAMPLER(sampler_DebugColorInput);
            float4 _DebugParams; // x: mip

            struct appdata { uint vertexID : SV_VertexID; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half debugRegionSize = _DebugParams.y;
                half2 debugRegionStart = half2(1.0 - debugRegionSize, 0.0);
                half2 debugRegionEnd = half2(1.0, debugRegionSize);
                half4 debugColor = SAMPLE_TEXTURE2D(_DebugColorInput, sampler_DebugColorInput, i.uv);

                if (i.uv.x >= debugRegionStart.x && i.uv.x <= debugRegionEnd.x &&
                    i.uv.y >= debugRegionStart.y && i.uv.y <= debugRegionEnd.y)
                {
                    uint2 size;
                    uint mipCount;
                    _DebugHiZTexture.GetDimensions(0, size.x, size.y, mipCount);
                    int mip = clamp((int)_DebugParams.x, 0, (int)mipCount-1);
                    half d = SAMPLE_TEXTURE2D_LOD(_DebugHiZTexture, sampler_DebugHiZTexture, i.uv, mip).r;
                    return half4(d.xxx,1);
                }
                else
                {
                    return debugColor;
                }
            }
            ENDHLSL
        }
    }
}
