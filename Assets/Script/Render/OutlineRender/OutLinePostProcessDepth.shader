Shader "Custom/OutLinePostProcessDepth"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Scale", Range(0,0.01)) = 0.002
        _OutlineStep ("Outline Step", Range(0,0.01)) = 0.005
    }
    SubShader
    {
        // Tags
        // {
        //     "RenderType"="Transparent"
        // }

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            uniform sampler2D _SrcTex;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineStep;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // offset
                half2 offset[9];
                offset[0] = half2(-1, 1) * _OutlineWidth;
                offset[1] = half2(0, 1) * _OutlineWidth;
                offset[2] = half2(1, 1) * _OutlineWidth;
                offset[3] = half2(-1, 0) * _OutlineWidth;
                offset[4] = half2(0, 0) * _OutlineWidth;
                offset[5] = half2(1, 0) * _OutlineWidth;
                offset[6] = half2(-1, -1) * _OutlineWidth;
                offset[7] = half2(0, -1) * _OutlineWidth;
                offset[8] = half2(1, -1) * _OutlineWidth;

                const half convoX[] = {
                    0, 0, 0,
                    -1, 0, 1,
                    0, 0, 0
                };
                const half convoY[] = {
                    0, -1, 0,
                    0, 0, 0,
                    0, 1, 0
                };

                half gx = 0;
                half gy = 0;
                half mask = 0;
                for (int i = 0; i < 9; i++)
                {
                    mask = tex2D(_SrcTex, IN.uv + offset[i]).r;
                    gx += mask * convoX[i];
                    gy += mask * convoY[i];
                }

                half4 color = tex2D(_MainTex, IN.uv);

                half a = saturate(abs(gx) + abs(gy)); //返回值是 x 限制在0到1之间的结果。
                a = step(_OutlineStep, a);

                color = lerp(color, _OutlineColor, a);
                return color;
            }
            ENDCG
        }

    }
    FallBack "Diffuse"
}