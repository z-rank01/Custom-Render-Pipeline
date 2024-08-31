// #include "UnityCG.cginc"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#include <Library/PackageCache/com.unity.render-pipelines.core@14.0.8/ShaderLibrary/SpaceTransforms.hlsl>
#include <Library/PackageCache/com.unity.render-pipelines.core@14.0.8/ShaderLibrary/Common.hlsl>

struct appdata
{
    float4 vertexOS : POSITION;
};

struct v2f
{
    float4 vertexCS : SV_POSITION;
};

// To reduce "setpass call", 
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
    o.vertexCS = mul(unity_MatrixVP, mul(unity_ObjectToWorld, v.vertexOS));
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    return _BaseColor;
}