#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#include "UnityCG.cginc"

#define MAX_DIRECTIONAL_LIGHTS 4

CBUFFER_START(_DirectionalLight)
int _DirectionalLightCount;
fixed4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHTS];
fixed4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHTS];
CBUFFER_END

struct Light
{
    fixed3 color;
    fixed3 direction;
};

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

Light GetDirectionalLight(int i)
{
    Light light;
    light.color = _DirectionalLightColors[i].rgb;
    light.direction = normalize(_DirectionalLightDirections[i].xyz);
    return light;
}

#endif