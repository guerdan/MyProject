Shader "Custom/MultiPassShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range (0.0, 0.03)) = 0.005
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }

        // First Pass: Basic Texture Rendering
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
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }

        // Second Pass: Outline Effect
//        Pass
//        {
//            Name "OUTLINE"
//            Tags { "LightMode" = "Always" }
//
//            Cull Front
//
//            CGPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//            #include "UnityCG.cginc"
//
//            struct appdata
//            {
//                float4 vertex : POSITION;
//                float2 uv : TEXCOORD0;
//                float3 normal : NORMAL;
//            };
//
//            struct v2f
//            {
//                float4 pos : SV_POSITION;
//                float2 uv : TEXCOORD0;
//            };
//
//            float _OutlineWidth;
//            float4 _OutlineColor;
//
//            v2f vert(appdata v)
//            {
//                // Expand vertices along normals to create outline
//                v.vertex.xyz += v.normal * _OutlineWidth;
//                v2f o;
//                o.pos = UnityObjectToClipPos(v.vertex);
//                o.uv = v.uv;
//                return o;
//            }
//
//            fixed4 frag(v2f i) : SV_Target
//            {
//                return _OutlineColor;
//            }
//            ENDCG
//        }
    }
    FallBack "Diffuse"
}