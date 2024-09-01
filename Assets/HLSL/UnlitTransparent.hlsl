#ifndef CUSTOM_UNLIT_TRANSPARENT_INCLUDED
#define CUSTOM_UNLIT_TRANSPARENT_INCLUDED

#include "UnityCG.cginc"

#ifdef UNITY_INSTANCING_ENABLED
// Vertex Shader data structure
struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Fragment Shader data structure
struct v2f
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Material Properties
sampler2D _MainTex;
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


// Vertex Shader function
v2f vert (appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v,o);
    
    o.vertex = UnityObjectToClipPos(v.vertex);
    float4 mainTexST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST);
    o.uv = v.uv * mainTexST.xy + mainTexST.zw;
    return o;
}

// Fragment Shader function
fixed4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    fixed4 col = tex2D(_MainTex, i.uv);
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    baseColor *= col == fixed4(0, 0, 0, 0) ? 1 : col;
    #if defined(_CLIPPING)
    clip(baseColor.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
    return baseColor;
}

#else

// Vertex Shader data structure
struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

// Fragment Shader data structure
struct v2f
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
};

// Material Properties
CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _BaseColor;
float _Cutoff;
CBUFFER_END

// Vertex Shader function
v2f vert (appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    return o;
}

// Fragment Shader function
fixed4 frag (v2f i) : SV_Target
{
    fixed4 col = tex2D(_MainTex, i.uv);
    float4 baseColor = _BaseColor * (col == fixed4(0, 0, 0, 0) ? 1 : col);
    #if defined(_CLIPPING)
    clip(baseColor.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
    return baseColor;
}

#endif

#endif