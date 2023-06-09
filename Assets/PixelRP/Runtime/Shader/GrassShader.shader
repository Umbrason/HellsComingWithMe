Shader "Custom/SimplestInstancedShader"
{
    Properties
    {
        _ColorTip ("Tip", Color) = (1, 1, 1, 1)
        _ColorBase ("Base", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objPos : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // use this to access instanced properties in the fragment shader.
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorTip)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorBase)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.objPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }            

            float smoothstep(float x, float edge0, float edge1)
            {
                float t = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
                return t * t * (3.0 - 2.0 * t);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float t = i.objPos.y / .15;
                t = smoothstep(t, .15,.6);

                return UNITY_ACCESS_INSTANCED_PROP(Props, _ColorTip) * t +
                       UNITY_ACCESS_INSTANCED_PROP(Props, _ColorBase) * (1 - t);
                
            }
            ENDCG
        }
    }
}