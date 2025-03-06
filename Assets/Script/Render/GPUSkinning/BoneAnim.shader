// 骨骼动画蒙皮的写法
Shader "GPU Skinning/BoneAnim"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_AnimTex("Animation", 2D) = "black" {}
		_BoneCount("Bone Count", int) = 50
		_FrameCount("Frame Count", int) = 50
		_Interval("Interval", Range(0.001, 1)) = 0.03333
		_BoneWeightCount("Frame Count", int) = 2
		_Random("Random", Range(0, 1)) = 0
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
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
				float4 _AnimTex_TexelSize;//纹理的1/宽像素数、1/高像素数、宽像素数、高像素数、
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

				float4 readInBoneTex(float total)
				{
					float2 newUv = uvConvert(total);
					float2 animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
					float r = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0)));
					newUv = uvConvert(total + 1);
					animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
					float g = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0)));
					newUv = uvConvert(total + 2);
					animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
					float b = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0)));
					newUv = uvConvert(total + 3);
					animUv = float2((newUv.x + 0.5) * _AnimTex_TexelSize.x, (newUv.y + 0.5) * _AnimTex_TexelSize.y);
					float a = DecodeFloatRGBA(tex2Dlod(_AnimTex, float4(animUv, 0, 0)));
					return float4(r, g, b, a) * 100 - 50;
				}

				v2f vert(appdata v)
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					
					float y = _Time.y / _Interval + UNITY_ACCESS_INSTANCED_PROP(Props, _Random) * _FrameCount;
					y = floor(y - floor(y / _FrameCount) * _FrameCount);  // y为当前帧序

					// 拿回索引和权重
                    float4 temp = 0;

					//组装两个骨骼的最终变换矩阵
					float total = (y * _BoneCount + (int)(v.uv1.x)) * 12;
					float4 line0 = readInBoneTex(total);
					float4 line1 = readInBoneTex(total + 4);
					float4 line2 = readInBoneTex(total + 8);
					float4x4 mat1 = float4x4(line0, line1, line2, float4(0, 0, 0, 1));
                    temp += mul(mat1, v.vertex) * v.uv1.y;

                    if (v.uv2.y > 0){
                        total = (y * _BoneCount + (int)(v.uv2.x)) * 12;
                        line0 = readInBoneTex(total);
                        line1 = readInBoneTex(total + 4);
                        line2 = readInBoneTex(total + 8);
                        mat1 = float4x4(line0, line1, line2, float4(0, 0, 0, 1));
                        temp += mul(mat1, v.vertex) * v.uv2.y;
                    }
					

					// 模型空间 -> 骨骼空间 -> 模型空间
					float4 pos = temp;
					o.vertex = UnityObjectToClipPos(pos);
					// 法线也如此操作
					// o.worldNormal = UnityObjectToWorldNormal(mul(mat, float4(v.normal, 0)).xyz);


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
