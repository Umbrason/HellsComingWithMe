Shader "Unlit/BillboardSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True"}
        LOD 100
        Cull off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata input)
            {
                float3 worldPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
 
                float3 dist = _WorldSpaceCameraPos - worldPos;
                float angle = atan2(dist.x, dist.z);
 
                float3x3 rotMatrix;
                float cosinus = cos(angle);
                float sinus = sin(angle);
                                       
                rotMatrix[0].xyz = float3(cosinus, 0, sinus  );
                rotMatrix[1].xyz = float3(0,       1, 0      );
                rotMatrix[2].xyz = float3(-sinus,  0, cosinus);
                 
                float4 newPos = float4(mul(rotMatrix, input.vertex), 1);                  
                
                v2f output;
                output.vertex = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, newPos)); 
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                if(col.a <= .001) discard;
                return col;
            }
            ENDCG
        }
    }
}
