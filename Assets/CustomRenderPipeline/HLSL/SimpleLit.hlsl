#ifndef CUSTOM_SIMPLE_LIT_INCLUDED
#define CUSTOM_SIMPLE_LIT_INCLUDED

#include "UnityCG.cginc"
#include "Lighting.hlsl"
#include "BRDF.hlsl"

struct appdata
{
    half4 vertexOS : POSITION;
    half3 normalOS : NORMAL;
    fixed2 uv : TEXCOORD0;
};

struct v2f
{
    half4 vertexCS : SV_POSITION;
    half4 vertexWS : VAR_POSITION;
    half3 normalWS : NORMAL;
    fixed2 uv : TEXCOORD0;
};

CBUFFER_START(UnityPerMaterial)
sampler2D _AlbedoMap;
sampler2D _NormalMap;
fixed4 _AlbedoMap_ST;
fixed4 _NormalMap_ST;

fixed4 _BaseColor;
fixed4 _SpecularColor;
half _Shininess;
fixed4 _AmbientColor;
CBUFFER_END

v2f vert(appdata v)
{
    v2f o;
    o.vertexCS = UnityObjectToClipPos(v.vertexOS);
    o.vertexWS = mul(unity_ObjectToWorld, v.vertexOS);
    o.normalWS = UnityObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _AlbedoMap);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    // setup bsaic parameters
    fixed4 col = tex2D(_AlbedoMap, i.uv);
    half3 normal = normalize(i.normalWS);
    half3 viewDirection = normalize(_WorldSpaceCameraPos - i.vertexWS);

    // setup BlinnPhong brdf
    SimpleBRDF brdf;
    brdf.diffuseColor = _BaseColor.rgb;
    brdf.specularColor = _SpecularColor.rgb;
    brdf.specularIntensity = _Shininess;

    // BlinnPhong shading with all directional lights
    fixed3 color = 0.0;
    for(int j = 0; j < GetDirectionalLightCount(); j++)
    {
        Light light = GetDirectionalLight(j);
        // diffuse
        color += LambertianBRDF(brdf, normal, light.direction) * light.color;
        // specular
        color += BlinnPhongBRDF(brdf, normal, light.direction, viewDirection) * light.color;
    }
    // ambient
    color += _AmbientColor.rgb;
    return fixed4(color, 1.0);
}

#endif