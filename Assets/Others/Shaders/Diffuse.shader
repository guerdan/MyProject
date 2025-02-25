Shader "MyShader/Diffuse"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        //漫反射参数，用于调整漫反射效果
        _Diffuse ("Diffuse", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque"
            "LightMode" = "ForwardBase"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work  加入fog变体
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                //法线方向（计算）
                float3 normal : NORMAL;
            };

            struct v2f
            {
                //输出顶点位置
                float4 pos : SV_POSITION;
                //存储顶点着色器输出的颜色
                fixed3 color_in : COLOR;

                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Diffuse;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                //color计算
                //变量准备
                fixed3 worldLight = normalize(_WorldSpaceLightPos0.xyz);
                //法线原本是模型空间下的，需要变换至世界坐标。因为法线本身是垂直得出的，所有矩阵用的unity_WorldToObject
                fixed3 worldNormal = normalize(mul(v.normal, (float3x3)unity_WorldToObject)); 
                //环境光部分
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                //漫反射部分计算
                fixed3 diffuse = _LightColor0.rgb * _Diffuse.rgb * saturate(dot(worldNormal, worldLight));
                //整理输出
                o.color_in = ambient + diffuse;

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                col = col * fixed4(i.color_in, 1.0);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
