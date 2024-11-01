Shader "Custom/Outline3D"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineScale ("Outline Scale", Float) = 1.03
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
        }

        // First Pass: Basic Texture Rendering
        Pass
        {
            Cull Front
            
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
            float _OutlineScale;
            float4 _OutlineColor;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex = v.vertex * _OutlineScale;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

    }
    FallBack "Diffuse"
}