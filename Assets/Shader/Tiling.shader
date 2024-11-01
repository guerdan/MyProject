Shader "Custom/tiling"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // 主纹理
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8 // 模板比较函数
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0 // 模板 ID
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0 // 模板操作
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255 // 模板写掩码
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255 // 模板读掩码
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15 // 颜色掩码，默认值为 15 (RGBA)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent" // 渲染队列设置为透明
            "IgnoreProjector"="True" // 忽略投影
            "RenderType"="Transparent" // 渲染类型为透明
            "PreviewType"="Plane" // 预览类型为平面
            "CanUseSpriteAtlas"="True" // 可以使用精灵图集
        }
        Stencil
        {
            Ref [_Stencil] // 模板参考值
            Comp [_StencilComp] // 模板比较函数
            Pass [_StencilOp] // 模板通过操作
            ReadMask [_StencilReadMask] // 模板读掩码
            WriteMask [_StencilWriteMask] // 模板写掩码
        }
        Cull Off // 关闭背面剔除
        Lighting Off // 关闭光照
        ZWrite Off // 关闭深度写入
        ZTest Always // 深度测试总是通过
        Blend SrcAlpha OneMinusSrcAlpha // 混合模式：源颜色的 alpha 和 1 - 源颜色的 alpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert // 顶点着色器入口
            #pragma fragment frag // 片段着色器入口
            #pragma multi_compile _ UNITY_UI_ALPHACLIP // 多编译选项
            #include "UnityUI.cginc" // 包含 Unity UI 的 CG 库
            #include "UnityCG.cginc" // 包含 Unity 的 CG 库

            sampler2D _MainTex; // 主纹理采样器

            struct a2v
            {
                float4 vertex : POSITION; // 顶点位置
                float4 color : COLOR; // 顶点颜色
                float2 texcoord : TEXCOORD0; // 纹理坐标
            };

            struct v2f
            {
                float4 vertex : SV_POSITION; // 裁剪空间位置
                float4 color : COLOR; // 顶点颜色
                float2 texcoord : TEXCOORD0; // 纹理坐标
            };
float4 _MainTex_ST;
            v2f vert(a2v IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex); // 转换为裁剪空间位置
                OUT.color = IN.color; // 传递颜色
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord,_MainTex); // 传递纹理坐标
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 读取纹理颜色并乘以顶点颜色
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                return color; // 返回颜色
            }
            ENDCG
        }
    }
}