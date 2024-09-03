#ifndef CUSTOM_UNLIT_INCLUDED
#define CUSTOM_UNLIT_INCLUDED

#include "UnityCG.cginc"


#ifdef UNITY_INSTANCING_ENABLED

// support GPU Instancing
struct appdata
{
    float4 vertexOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 vertexCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

sampler2D _MainTex;

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    
    o.vertexCS = UnityObjectToClipPos(v.vertexOS);
    float4 mainTexST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST);
    o.uv = v.uv * mainTexST.xy + mainTexST.zw;
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    fixed4 col = tex2D(_MainTex, i.uv);
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return baseColor *= col == fixed4(0, 0, 0, 0) ? 1 : col;
}

#else

// support SPR Batcher
struct appdata
{
    half4 vertexOS : POSITION;
    fixed2 uv : TEXCOORD0;
};

struct v2f
{
    half4 vertexCS : SV_POSITION;
    fixed2 uv : TEXCOORD0;
};

// To reduce "setPass call", 
// use constant buffer per material or draw macros
// to cache the properties in GPU for reuse.

// CBUFFER_START()
// ...
// CBUFFER_END
CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
half4 _MainTex_ST;
fixed4 _BaseColor;
CBUFFER_END

v2f vert(appdata v)
{
    v2f o;
    o.vertexCS = UnityObjectToClipPos(v.vertexOS);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    fixed4 col = tex2D(_MainTex, i.uv);
    return _BaseColor;
}

#endif

#endif