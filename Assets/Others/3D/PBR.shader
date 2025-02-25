//Standard材质
Shader "MyShader/PBR"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [NoScaleOffset]_MetallicTex("Metallic(R) Smoothness(G) AO(B)",2D) = "white" {}
        [Normal]_NormalTex("NormalTex",2D) = "bump" {}
        
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AO("AO",Range(0,1)) = 1.0      // _AO 属性定义了环境光遮蔽的强度，范围为 0 到 1，默认值为 1.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 200

        // ---- forward rendering base pass:
        Pass
        {
            Name "FORWARD"
            Tags
            {
                "LightMode" = "ForwardBase"
            }

            CGPROGRAM
            // compile directives
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Glossiness;
            half _Metallic;
            fixed4 _Color;
            sampler2D _MetallicTex;
            half _AO;
            sampler2D _NormalTex;

#ifdef UNITY_INSTANCING_ENABLED
        UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
#endif
            
            struct appdata
            {
                float4 vertex : POSITION;      // 顶点位置
                float4 tangent : TANGENT;      // 切线
                float3 normal : NORMAL;        // 法线
                float4 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                float4 texcoord3 : TEXCOORD3;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Unity 实例化 ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;       // 裁剪空间位置
                float2 uv : TEXCOORD0;  
                float3 worldNormal : TEXCOORD1; // 世界空间法线
                float3 worldPos : TEXCOORD2;
                #if UNITY_SHOULD_SAMPLE_SH
                    half3 sh : TEXCOORD3; // SH
                #endif
                //切线空间需要使用的矩阵
                float3 tSpace0 : TEXCOORD4;
                float3 tSpace1 : TEXCOORD5;
                float3 tSpace2 : TEXCOORD6;

                UNITY_FOG_COORDS(7)             // 雾坐标
                UNITY_SHADOW_COORDS(8)          // 阴影坐标
                UNITY_VERTEX_INPUT_INSTANCE_ID // Unity 实例化 ID
            };

            // vertex shader
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);

                //世界空间下的切线
                half3 worldTangent = UnityObjectToWorldDir(v.tangent);
                //切线方向
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                //世界空间下的副切线
                half3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;
                //切线矩阵
                o.tSpace0 = float3(worldTangent.x, worldBinormal.x, worldNormal.x);
                o.tSpace1 = float3(worldTangent.y, worldBinormal.y, worldNormal.y);
                o.tSpace2 = float3(worldTangent.z, worldBinormal.z, worldNormal.z);

                o.worldPos.xyz = worldPos;
                o.worldNormal = worldNormal;

                // SH/ambient and vertex lights

                #if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
                    o.sh = 0;
                    // Approximated illumination from non-important point lights
                #ifdef VERTEXLIGHT_ON
                    o.sh += Shade4PointLights (
                    unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                    unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                    unity_4LightAtten0, worldPos, worldNormal);
                #endif
                    o.sh = ShadeSHPerVertex (worldNormal, o.sh);
                #endif


                UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);

                UNITY_TRANSFER_FOG(o, o.pos); // pass fog coordinates to pixel shader

                return o;
            }

            // fragment shader
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_EXTRACT_FOG(i);
                
                float3 worldPos = i.worldPos.xyz;
                
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));

                SurfaceOutputStandard o;
                UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandard, o);

                fixed4 mainTex = tex2D(_MainTex, i.uv);

                #ifdef UNITY_INSTANCING_ENABLED
                    _Color = UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, _Color);
                #endif
                o.Albedo = mainTex.rgb * _Color;

                o.Emission = 0.0;

                fixed4 metallicTex = tex2D(_MetallicTex, i.uv);
                o.Metallic = metallicTex.r * _Metallic;
                o.Smoothness = metallicTex.g * _Glossiness;
                o.Occlusion = metallicTex.b * _AO;
                o.Alpha = 1;


                half3 normalTex = UnpackNormal(tex2D(_NormalTex,i.uv));
                half3 worldNormal = half3(dot(i.tSpace0,normalTex),dot(i.tSpace1,normalTex),dot(i.tSpace2,normalTex));
                o.Normal = worldNormal;


                // compute lighting & shadowing factor
                UNITY_LIGHT_ATTENUATION(atten, i, worldPos)

                // 设置光照环境 ，UnityGI 结构体用于存储全局光照计算的输出数据。
                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.indirect.diffuse = 0;
                gi.indirect.specular = 0;
                gi.light.color = _LightColor0.rgb;
                gi.light.dir = _WorldSpaceLightPos0.xyz;
                // 调用 GI (光照贴图/SH/反射) 光照函数，UnityGIInput 结构体用于存储输入到全局光照计算中的数据。
                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.light = gi.light;
                giInput.worldPos = worldPos;
                giInput.worldViewDir = worldViewDir;
                giInput.atten = atten;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    giInput.lightmapUV = IN.lmap;
                #else
                giInput.lightmapUV = 0.0;
                #endif
                #if UNITY_SHOULD_SAMPLE_SH && !UNITY_SAMPLE_FULL_SH_PER_PIXEL
                    giInput.ambient = i.sh;
                #else
                giInput.ambient.rgb = 0.0;
                #endif
                giInput.probeHDR[0] = unity_SpecCube0_HDR;
                giInput.probeHDR[1] = unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
                    giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                    giInput.boxMax[0] = unity_SpecCube0_BoxMax;
                    giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
                    giInput.boxMax[1] = unity_SpecCube1_BoxMax;
                    giInput.boxMin[1] = unity_SpecCube1_BoxMin;
                    giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                LightingStandard_GI(o, giInput, gi);

                // PBS的核心计算
                fixed4 c = LightingStandard(o, worldViewDir, gi);
                UNITY_APPLY_FOG(_unity_fogCoord, c); // apply fog
                UNITY_OPAQUE_ALPHA(c.a); //把c的Alpha置1
                return c;
            }
            ENDCG

        }
    }

}
