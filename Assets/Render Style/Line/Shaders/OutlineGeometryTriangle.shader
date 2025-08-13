Shader "LSQ/Render Style/Outline/Geometry/Triangle"
{
    Properties
    {
        [Header(Outline)][Space]
        [KeywordEnum(VertexColor,Tangent,UV1,UV2,UV3,UV4)]_OutlineSource ("Source", int) = 0
        [Toggle(_INTBN)]_InTBN ("Store In TBN Space", float) = 0
        [KeywordEnum(Object,World,View,Clip)]_OutlineSpace ("Space", int) = 0
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
            #pragma geometry geom
            #pragma fragment frag

            #include "OutlineHelper.cginc"

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD;

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
            };

            struct v2g
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalSmoothOS : TEXCOORD1;
            };

            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float expand : TEXCOORD1;
            };

            v2g vert(appdata IN)
            {
                v2g OUT;

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
                
                OUT.positionOS = IN.positionOS;
                OUT.uv = IN.uv;
                OUT.normalSmoothOS = smoothNormalOS;
                return OUT;
            }

            g2f Expand(v2g input)
            {
                g2f OUT;
                OUT.positionCS = ExpandAlongNormal(input.positionOS, input.normalSmoothOS);
                OUT.uv = input.uv;
                OUT.expand = 1;
                return OUT;
            }

            [maxvertexcount(21)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> output) 
            {             
                g2f origion[3];
                [unroll]
                for(int i = 0; i < 3; i++)
                {
                    origion[i].positionCS = UnityObjectToClipPos(input[i].positionOS);
                    origion[i].uv = input[i].uv;
                    origion[i].expand = 0;
                    output.Append(origion[i]);
                }
                output.RestartStrip();

                g2f extrusionVertex0 = Expand(input[0]);
                g2f extrusionVertex1 = Expand(input[1]);
                g2f extrusionVertex2 = Expand(input[2]);

                // Line : 0-1
                output.Append(origion[0]);
                output.Append(extrusionVertex0);
                output.Append(origion[1]);
                output.RestartStrip();

                output.Append(extrusionVertex0);
                output.Append(extrusionVertex1);
                output.Append(origion[1]);
                output.RestartStrip();

                // Line : 1-2
                output.Append(origion[1]);
                output.Append(extrusionVertex1);
                output.Append(extrusionVertex2);
                output.RestartStrip();

                output.Append(origion[1]);
                output.Append(extrusionVertex2);
                output.Append(origion[2]);
                output.RestartStrip();

                // Line : 2-0
                output.Append(origion[2]);
                output.Append(extrusionVertex2);
                output.Append(origion[0]);
                output.RestartStrip();

                output.Append(extrusionVertex2);
                output.Append(extrusionVertex0);
                output.Append(origion[0]);
                output.RestartStrip();
            }

            sampler2D _MainTex;

            half4 frag (g2f i) : SV_Target
            {
                if (i.expand)
                    return 1;
                half4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
