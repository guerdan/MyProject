﻿// 骨骼动画蒙皮的写法
Shader "GPU Skinning/BoneAnim_Improved"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
		_AnimTex("Animation", 2D) = "black" {}
		_BoneCount("Bone Count", int) = 50
		_FrameCount("Frame Count", int) = 50
		_Interval("Interval", Range(0.001, 1)) = 0.03333
		// 这个写法能够控制 Shader编译的Keyword 。映射关系为 字段名 拼接枚举名。可在组件debug模式下观察keyword变化
		[KeywordEnum(UV2, UV3, UV4, UV5, UV6, UV7, UV8)]_IndexUV("Bone Index Channel", float) = 0
		[KeywordEnum(UV2, UV3, UV4, UV5, UV6, UV7, UV8)]_WeightUV("Bone Weight Channel", float) = 1

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
				// 搞这么多变体当然不好，只是测试用
				#pragma multi_compile _WEIGHTUV_UV2 _WEIGHTUV_UV3 _WEIGHTUV_UV4 _WEIGHTUV_UV5 _WEIGHTUV_UV6 _WEIGHTUV_UV7 _WEIGHTUV_UV8
				#pragma multi_compile _INDEXUV_UV2 _INDEXUV_UV3 _INDEXUV_UV4 _INDEXUV_UV5 _INDEXUV_UV6 _INDEXUV_UV7 _INDEXUV_UV8

				#undef BONE_INPUT_WEIGHT_UV
				#if _WEIGHTUV_UV2
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD1;
				#elif _WEIGHTUV_UV3
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD2;
				#elif _WEIGHTUV_UV4
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD3;
				#elif _WEIGHTUV_UV5
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD4;
				#elif _WEIGHTUV_UV6
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD5;
				#elif _WEIGHTUV_UV7
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD6;
				#else
					#define BONE_INPUT_WEIGHT_UV float2 wuv : TEXCOORD7;
				#endif


				#undef BONE_INPUT_INDEX_UV
				#if _INDEXUV_UV2
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD1;
				#elif _INDEXUV_UV3
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD2;
				#elif _INDEXUV_UV4
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD3;
				#elif _INDEXUV_UV5
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD4;
				#elif _INDEXUV_UV6
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD5;
				#elif _INDEXUV_UV7
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD6;
				#else
					#define BONE_INPUT_INDEX_UV float2 iuv : TEXCOORD7;
				#endif

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					float2 uv1 : TEXCOORD1;
					float2 uv2 : TEXCOORD2;
					float2 uv3 : TEXCOORD3;
					float2 uv4 : TEXCOORD4;
					BONE_INPUT_WEIGHT_UV
					BONE_INPUT_INDEX_UV
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
					float4 line1 : TEXCOORD1;
					float4 line2 : TEXCOORD2;
					float4 line3 : TEXCOORD3;
					float4 line4 : TEXCOORD4;
					float4 uv_n_uv :TEXCOORD5;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				UNITY_INSTANCING_BUFFER_START(Props)
					UNITY_DEFINE_INSTANCED_PROP(float, _Random)
				UNITY_INSTANCING_BUFFER_END(Props)

				sampler2D _MainTex;
				float4 _MainTex_ST;
				sampler2D _AnimTex;
				float4 _AnimTex_TexelSize;
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
					// 计算y轴坐标（时间）
					float y = _Time.y / _Interval + UNITY_ACCESS_INSTANCED_PROP(Props, _Random) * _FrameCount;;
					y = floor(y - floor(y / _FrameCount) * _FrameCount);

					// 拿回索引和权重
					float2 index = v.iuv;
					float2 weight = v.wuv;

					//组装两个骨骼的最终变换矩阵
					float total = (y * _BoneCount + (int)(index.x)) * 12;
					float4 line0 = readInBoneTex(total);
					float4 line1 = readInBoneTex(total + 4);
					float4 line2 = readInBoneTex(total + 8);
					float4x4 mat1 = float4x4(line0, line1, line2, float4(0, 0, 0, 1));
					total = (y * _BoneCount + (int)(index.y)) * 12;
					line0 = readInBoneTex(total);
					line1 = readInBoneTex(total + 4);
					line2 = readInBoneTex(total + 8);
					float4x4 mat2 = float4x4(line0, line1, line2, float4(0, 0, 0, 1));
					// 模型空间 -> 骨骼空间 -> 模型空间
					float4 pos = mul(mat1, v.vertex) * weight.x + mul(mat2, v.vertex) * (1 - weight.x);
					o.vertex = UnityObjectToClipPos(pos);
					// 法线也如此操作
					// o.worldNormal = UnityObjectToWorldNormal(mul(mat, float4(v.normal, 0)).xyz);

					o.line1 = line0;
					o.line2 = line1;
					o.line3 = line2;
					o.line4 = float4(0, 0, 0, 1);
					o.uv_n_uv = tex2Dlod(_AnimTex, float4(0, 0, 0, 0));
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 col = tex2D(_MainTex, i.uv);
					return col;
				}
				ENDCG
			}
		}
}
