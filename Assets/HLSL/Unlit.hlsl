#ifndef CUSTOM_UNLIT_INCLUDED
#define CUSTOM_UNLIT_INCLUDED

struct appdata
{
    float4 vertexOS : POSITION;
};

struct v2f
{
    float4 vertexCS : SV_POSITION;
};

// To reduce "setPass call", 
// use constant buffer per material or draw macros
// to cache the properties in GPU for reuse.

// CBUFFER_START()
// ...
// CBUFFER_END
CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _BaseColor;
CBUFFER_END

v2f vert(appdata v)
{
    v2f o;
    o.vertexCS = UnityObjectToClipPos(v.vertexOS);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    return _BaseColor;
}

#endif