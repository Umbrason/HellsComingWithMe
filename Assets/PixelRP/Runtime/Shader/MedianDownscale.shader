Shader "Hidden/MedianDownscale"
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

            sampler2D _BlitSrcColor;
            float4 _BlitSrcColor_TexelSize;

            sampler2D _BlitSrcDepth;
            float4 _BlitSrcDepth_TexelSize;
            
            #define medianRadius 1
            #define kernelSize 9

            PixelData frag (v2f i)
            {
                float2 uvOffset = _BlitSrcColor_TexelSize.xy / 2.0;
                i.uv -= uvOffset / 2.0;

                fixed4 colors[kernelSize];
                fixed  depths[kernelSize];
                fixed4 avgColor;
                fixed2 medianSourceUV;
                
                for(int x = -medianRadius; x <= medianRadius; x++)
                    for(int y = -medianRadius; y <= medianRadius; y++)
                    {
                        float2 sampleUV = i.uv + uvOffset * float2(x, y);
                        sampleUV = clamp(sampleUV, 0, 1);
                        int i = (medianRadius * 2 + 1) * (x + medianRadius) + y + medianRadius; 
                        colors[i] = tex2D(_BlitSrcColor, sampleUV);
                        depths[i] = tex2D(_BlitSrcDepth, sampleUV);
                        avgColor += colors[i] / (float)kernelSize;
                    }
                
                fixed4 medianColor = 0;
                fixed  medianDepth = 0;
                float md = 1000000.0;
                for(int i = 0; i < kernelSize; i++)
                {
                    float d = length(avgColor - colors[i]);
                    float smaller = (d <= md);
                    md = md * (1 - smaller) + smaller * d;
                    medianColor *= 1 - smaller;
                    medianDepth *= 1 - smaller;
                    medianColor += smaller * colors[i];
                    medianDepth += smaller * depths[i];
                }

                /* fixed4 col1 = tex2D(_BlitSrcColor, i.uv + uvOffset);
                fixed4 col2 = tex2D(_BlitSrcColor, i.uv + float2(0, uvOffset.y) );
                fixed4 col3 = tex2D(_BlitSrcColor, i.uv + float2(uvOffset.x, 0) );
                fixed4 col4 = tex2D(_BlitSrcColor, i.uv);
                
                fixed4 depth1 = tex2D(_BlitSrcDepth, i.uv + _BlitSrcColor_TexelSize.xy);
                fixed4 depth2 = tex2D(_BlitSrcDepth, i.uv + float2(0,_BlitSrcColor_TexelSize.y));
                fixed4 depth3 = tex2D(_BlitSrcDepth, i.uv + float2(_BlitSrcColor_TexelSize.x, 0));
                fixed4 depth4 = tex2D(_BlitSrcDepth, i.uv);

                fixed4 avgCol = (col1 + col2 + col3 + col4) / 4.0;
                fixed4 d = float4(length(col1 - avgCol), length(col2 - avgCol), length(col3 - avgCol), length(col4 - avgCol));
                d = float4(d.x <= d.y & d.x <= d.z & d.x <= d.w,
                           d.y < d.x & d.y < d.z & d.y < d.w,
                           d.z < d.x & d.z < d.y & d.z < d.w,
                           d.w < d.x & d.w < d.y & d.w < d.z);
                if(length(d) == 0) d.x = 1;

                fixed4 medianColor = d.x * col1 +
                                     d.y * col2 +
                                     d.z * col3 +
                                     d.w * col4;

                fixed4 medianDepth = d.x * depth1 +
                                     d.y * depth2 +
                                     d.z * depth3 +
                                     d.w * depth4;
 */
                PixelData pd;
                pd.color = medianColor;
                pd.depth = medianDepth;
                return pd;
            }

            ENDCG
        }
    }
}
