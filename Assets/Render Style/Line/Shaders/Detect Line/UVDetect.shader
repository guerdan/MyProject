Shader "LSQ/Render Style/Line/UVDetect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            int _LineWidth;

            half4 frag (v2f i) : SV_Target
            {  
                float2 unit = _MainTex_TexelSize.xy * _LineWidth;
                half col[9];
                col[0] = tex2D(_MainTex, i.uv + float2(0,0) * unit).r;
                col[1] = tex2D(_MainTex, i.uv + float2(0,-1)* unit).r;
                col[2] = tex2D(_MainTex, i.uv + float2(0,1) * unit).r;
                col[3] = tex2D(_MainTex, i.uv + float2(1,1) * unit).r;
                col[4] = tex2D(_MainTex, i.uv + float2(1,0) * unit).r;
                col[5] = tex2D(_MainTex, i.uv + float2(1,-1) * unit).r;
                col[6] = tex2D(_MainTex, i.uv + float2(-1,0) * unit).r;
                col[7] = tex2D(_MainTex, i.uv + float2(-1,1) * unit).r;
                col[8] = tex2D(_MainTex, i.uv + float2(-1,-1) * unit).r;

                half sum = 0;
                for(int i = 0; i < 9; i++)
                    sum += col[i];
                sum /= 9;

                if (sum < 1) //if (col[0] > 0 && sum < 1)
                    return 1;
                else
                    return 0;
            }
            ENDCG
        }
    }
}
