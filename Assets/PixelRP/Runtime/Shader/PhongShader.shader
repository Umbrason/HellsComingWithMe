Shader "Lit/Phong"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainColor ("Color", Color) = (.9, .8, .2, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Back
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag
            
            #include <"Assets/PixelRP/Shaderlibrary/Lighting.hlsl">

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            cbuffer UnityPerMaterial {
                float4 _MainColor;
            };

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

            fixed4 frag(Varyings i) : SV_Target
            {
                Surface surface;
                surface.normal = i.normalWS;
                Light light = CalcLight(surface);

                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;
                fixed3 stylizedLight = smoothstep(.59, .6, light.color);
                return float4(max(col * stylizedLight, .15), 1);
            }
            ENDCG
        }
    }
}
