#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#include "UnityCG.cginc"

struct SimpleBRDF
{
    fixed3 diffuseColor;
    fixed3 specularColor;
    half specularIntensity;
};

struct CookTorranceBRDF
{
    fixed4 albedo;
    half metallic;
    half roughness;
};


// traditional lighting model

// lambertian diffuse
fixed3 LambertianBRDF(SimpleBRDF brdf, half3 normal, half3 lightDirection)
{
    return brdf.diffuseColor * max(0, dot(normal, lightDirection));
}

// blinn phong specular
fixed3 BlinnPhongBRDF(SimpleBRDF brdf, half3 normal, half3 lightDirection, half3 viewDirection)
{
    half3 halfDir = normalize(lightDirection + viewDirection);
    return brdf.specularColor * pow(max(0, dot(halfDir, normal)), brdf.specularIntensity);
}

// Physically based lighting model

#endif