#include "UnityCG.cginc"

struct appdata
{
    float4 vertexOS : POSITION;
};

struct v2f
{
    float4 vertexCS : SV_POSITION;
};


sampler2D _MainTex;
float4 _MainTex_ST;
float4 _BaseColor;

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