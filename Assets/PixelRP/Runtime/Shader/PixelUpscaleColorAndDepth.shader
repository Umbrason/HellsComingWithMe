Shader "Hidden/PixelUpscaleColorAndDepth"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag            

            #include "Assets/PixelRP/Shaderlibrary/ScreenPass.hlsl"

            sampler2D _EnvironmentColor;
            float4 _EnvironmentColor_TexelSize;

            sampler2D _EnvironmentDepth;
            float4 _EnvironmentDepth_TexelSize;

            float2 _pxPerTex;

            PixelData frag (v2f i)
            {
                float2 tx = i.uv * _EnvironmentColor_TexelSize.zw;
                float2 txOffset = clamp(frac(tx) * _pxPerTex, 0, 0.5) - clamp((1 - frac(tx)) * _pxPerTex, 0, 0.5);
                float2 uv = (floor(tx) + .5 + txOffset) * _EnvironmentColor_TexelSize.xy;
                fixed4 col = tex2D(_EnvironmentColor, uv);
                fixed depth = tex2D(_EnvironmentDepth, uv);

                PixelData pd;
                pd.color = col;
                pd.depth = depth;
                return pd;
            }

            ENDCG
        }
    }
}
