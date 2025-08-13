Shader "LSQ/Render Style/Outline/NdotV"
{
    Properties
    {
        [Header(Outline)][Space]
        _OutlineValue("Value", Range(0, 1)) = 0.2
        _OutlineWidth("Width", Range(0, 1)) = 0.2

        [Header(Render)][Space]
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : NORMAL;
            };

            sampler2D _MainTex;

            float _OutlineValue;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.uv = v.uv, _MainTex;
                o.positionWS = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                half3 V = normalize(UnityWorldSpaceViewDir(i.positionWS));
                float NdotV = saturate(dot(N, V));
				float outline = smoothstep(_OutlineValue - _OutlineWidth, _OutlineValue + _OutlineWidth, NdotV);

                half4 col = tex2D(_MainTex, i.uv) * outline;
                return col;
            }
            ENDCG
        }
    }
}
