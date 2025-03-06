// 骨骼动画蒙皮的写法
Shader "GPU Skinning/2DBoneAnim"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _AnimTex("Animation", 2D) = "black" {}
        _BoneCount("Bone Count", int) = 50
        _FrameCount("Frame Count", int) = 50
        _Interval("Interval", Range(0.001, 1)) = 0.03333
        _BoneWeightCount("BoneWeightCount", int) = 4
        _Random("Random", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
                float2 uv4 : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float, _Random)
            UNITY_INSTANCING_BUFFER_END(Props)

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _AnimTex;
            float4 _AnimTex_TexelSize; //纹理的 1/宽像素数、1/高像素数、宽像素数、高像素数、
            int _BoneCount, _FrameCount;
            float _Interval;

            // 转换成图片空间下的uv
            float2 uvConvert(float total)
            {
                float new_y = total * _AnimTex_TexelSize.x;
                float new_x = floor(fmod(new_y, 1.0) * _AnimTex_TexelSize.z);
                new_y = floor(new_y);
                return float2(new_x, new_y);
            }

            float4x4 readInBoneTex(float total)
            {
                int width = _AnimTex_TexelSize.z;
                float new_y = total * _AnimTex_TexelSize.x;
                float new_x = floor(fmod(new_y, 1.0) * _AnimTex_TexelSize.z);
                new_y = floor(new_y);
                float2 newUv = float2(new_x, new_y);

                float varX = (newUv.x + 0.5) * _AnimTex_TexelSize.x;
                float varY = (newUv.y + 0.5) * _AnimTex_TexelSize.y;
                float2 animUv = float2(varX, varY);
                float m00 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;

                float2 result[5];
                for (int i = 0; i < 5; i++)
                {
                    if (new_x + 1 < width)
                        new_x++;
                    else
                    {
                        new_x = 0;
                        varY += _AnimTex_TexelSize.y;
                    }
                    float varX = (new_x + 0.5) * _AnimTex_TexelSize.x;
                    result[i] = float2(varX, varY);
                }

                animUv = result[0];
                float m01 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;
                animUv = result[1];
                float m03 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;
                animUv = result[2];
                float m10 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;
                animUv = result[3];
                float m11 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;
                animUv = result[4];
                float m13 = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0))) * 100 - 50;

                return float4x4(
                    m00, m01, 0, m03,
                    m10, m11, 0, m13,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );;
            }


            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float y = _Time.y / _Interval + UNITY_ACCESS_INSTANCED_PROP(Props, _Random) * _FrameCount;
                y = floor(y - floor(y / _FrameCount) * _FrameCount); // y为当前帧序

                // 拿回索引和权重
                float4 temp = 0;

                float remain = 1 - v.uv1.y - v.uv2.y - v.uv3.y - v.uv4.y;
                float var1 = y * _BoneCount;

                //组装每个骨骼的最终变换矩阵
                float total = (var1 + (int)(v.uv1.x)) * 6;
                float4x4 mat1 = readInBoneTex(total);
                temp += mul(mat1, v.vertex) * v.uv1.y;

                if (v.uv2.y > 0)
                {
                    total = (var1 + (int)(v.uv2.x)) * 6;
                    mat1 = readInBoneTex(total);
                    temp += mul(mat1, v.vertex) * v.uv2.y;
                }
                if (v.uv3.y > 0)
                {
                    total = (var1 + (int)(v.uv3.x)) * 6;
                    mat1 = readInBoneTex(total);
                    temp += mul(mat1, v.vertex) * v.uv3.y;
                }
                if (v.uv4.y > 0)
                {
                    total = (var1 + (int)(v.uv4.x)) * 6;
                    mat1 = readInBoneTex(total);
                    temp += mul(mat1, v.vertex) * (v.uv4.y + remain);
                }


                // 模型空间 -> 骨骼空间 -> 模型空间
                float4 pos = temp;
                o.vertex = UnityObjectToClipPos(pos);
                // 法线也如此操作
                // o.worldNormal = UnityObjectToWorldNormal(mul(mat, float4(v.normal, 0)).xyz);

                // o.vertex = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                clip(col.a - 0.1);
                return col;
            }
            ENDCG
        }
    }
}