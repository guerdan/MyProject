Shader "LSQ/Render Style/Outline/ZOffset"
{
    Properties
    {
        [Header(Outline)][Space]
        [KeywordEnum(VertexColor,Tangent,UV1,UV2,UV3,UV4)]_OutlineSource ("Source", int) = 0
        [Toggle(_INTBN)]_InTBN ("Store In TBN Space", float) = 0
        [KeywordEnum(Object,World,View,Clip)]_OutlineSpace ("Space", int) = 0
        _OutlineWidth ("Width", Range(0, 0.1)) = 0.02
        _OutlineZOffset ("Z Offset", Range(0, 1)) = 0.0001

        [Header(Render)][Space]
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Pass
        {
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "OutlineHelper.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;

                #ifdef _OUTLINESOURCE_VERTEXCOLOR
                float4 color : Color;
                #elif _OUTLINESOURCE_UV1
                float3 uv1 : TEXCOORD1;
                #elif _OUTLINESOURCE_UV2
                float3 uv2 : TEXCOORD2;
                #elif _OUTLINESOURCE_UV3
                float3 uv3 : TEXCOORD3;
                #elif _OUTLINESOURCE_UV4
                float3 uv4 : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float _OutlineZOffset;

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;

                float3 bitangentOS = cross(IN.normalOS, IN.tangentOS.xyz) * IN.tangentOS.w;
                float3x3 OtoT = float3x3(IN.tangentOS.xyz, bitangentOS, IN.normalOS);
                float3 smoothNormalOS = IN.normalOS;
                #ifdef _OUTLINESOURCE_VERTEXCOLOR
                smoothNormalOS = GetSmoothNormalOS(IN.color, OtoT);
                #elif _OUTLINESOURCE_TANGENT
                smoothNormalOS = GetSmoothNormalOS(IN.tangentOS, OtoT);
                #elif _OUTLINESOURCE_UV1
                smoothNormalOS = GetSmoothNormalOS(IN.uv1, OtoT);
                #elif _OUTLINESOURCE_UV2
                smoothNormalOS = GetSmoothNormalOS(IN.uv2, OtoT);
                #elif _OUTLINESOURCE_UV3
                smoothNormalOS = GetSmoothNormalOS(IN.uv3, OtoT);
                #elif _OUTLINESOURCE_UV4
                smoothNormalOS = GetSmoothNormalOS(IN.uv4, OtoT);
                #endif

                OUT.positionCS = ExpandAlongNormal(IN.positionOS, smoothNormalOS);
                OUT.positionCS = GetNewClipPosWithZOffset(OUT.positionCS, _OutlineZOffset);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                return 1;
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #pragma multi_compile_instancing

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.uv = v.uv, _MainTex;;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}