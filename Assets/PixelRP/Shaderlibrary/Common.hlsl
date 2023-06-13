struct Attributes
{
    float3 posOS    : POSITION;
    float3 normalOS : NORMAL;
    float3 uv       : TEXCOORD0;
};

struct Varyings
{
    float4 posCS    : POSITION;
    float3 normalWS : NORMAL;
    float3 uv       : TEXCOORD0;
};

Varyings vert(Attributes attributes)
{
    Varyings varyings;
    varyings.posCS = UnityObjectToClipPos(attributes.posOS);
    varyings.normalWS = normalize(mul(unity_ObjectToWorld, float4(attributes.normalOS, 0)));
    varyings.uv = attributes.uv;
    return varyings;
}