#ifndef CUSTOM_LIT_INCLUDED
#define CUSTOM_LIT_INCLUDED

#define MAX_DIRECTIONAL_LIGHTS 4

#include "UnityCG.cginc"

struct appdata
{
    float4 vertexOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float4 vertexCS : SV_POSITION;
    float3 normalWS : NORMAL;
    float2 uv : TEXCOORD0;
};

CBUFFER_START(_DirectionalLight)
int _DirectionalLightCount;
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHTS];
float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHTS];
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _BaseColor;
CBUFFER_END

v2f vert(appdata v)
{
    v2f o;
    o.vertexCS = UnityObjectToClipPos(v.vertexOS);
    o.normalWS = UnityObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    fixed4 col = tex2D(_MainTex, i.uv);
    float3 normal = normalize(i.normalWS);
    float3 color = 0.0;
    for(int j = 0; j < _DirectionalLightCount; j++)
    {
        color += _BaseColor.rgb * _DirectionalLightColors[j].rgb * max(dot(normal, _DirectionalLightDirections[j]), 0);
    }
    return float4(color, 1.0);
}

#endif