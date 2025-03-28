Shader "Custom/Outline3D"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Float) = 1.03
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
            // Cull Front

            CGPROGRAM
            #include "UnityCG.cginc"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct a2v {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 smoothNormal : TEXCOORD3; // 平滑的法线, 对相同顶点的所有法线取平均值
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct v2f {
                float4 pos : SV_POSITION;
            };

            sampler2D _MainTex;
            float _OutlineWidth;
            float4 _OutlineColor;
            float4 _MainTex_ST;
            float _StartTime;

            v2f vert(a2v v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 normal = any(v.smoothNormal) ? v.smoothNormal : v.normal; // 光滑的法线
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, normal)); // 观察坐标系下的法线向量
                float3 viewPos = UnityObjectToViewPos(v.vertex); // 观察坐标系下的顶点坐标
                // 裁剪坐标系下的顶点坐标, 将顶点坐标沿着法线方向向外延伸, 延伸的部分就是描边部分
                // 乘以(-viewPos.z)是为了抵消透视变换造成的描边宽度近大远小效果, 使得物体无论距离相机多远, 描边宽度都不发生变化
                // 除以1000是为了将描边宽度单位转换到1mm(这里的宽度是世界坐标系中的宽度, 而不是屏幕上的宽度)
                o.pos = UnityViewToClipPos(viewPos + viewNormal * _OutlineWidth * (-viewPos.z) / 1000);
                return o;
            }
 
            fixed4 frag(v2f i) : SV_Target {
                float t1 = sin(_Time.z - _StartTime); // _Time = float4(t/20, t, t*2, t*3)
                float t2 = cos(_Time.z - _StartTime);
                // 描边颜色随时间变化, 描边透明度随时间变化, 视觉上感觉描边在膨胀和收缩
                return _OutlineColor;
            }
            ENDCG
        }

    }
//    FallBack "Diffuse"
}