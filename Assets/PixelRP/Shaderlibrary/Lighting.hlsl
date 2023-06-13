#define MAX_DIR_LIGHTS_COUNT 4

CBUFFER_START(_DirectionalLights)
    int _DirectionalLightCount;
	float3 _DirectionalLightColors[MAX_DIR_LIGHTS_COUNT];
	float3 _DirectionalLightDirections[MAX_DIR_LIGHTS_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
};

struct Surface
{
    float3 normal;
};

Light CalcLight(Surface surf)
{
    Light light;
    light.color = 0;
    for(int i = 0; i < _DirectionalLightCount; i++)
    {
        float brightness = dot(surf.normal, _DirectionalLightDirections[i]);
        light.color += max(0, brightness) * _DirectionalLightColors[i];
    }
    return light;
}