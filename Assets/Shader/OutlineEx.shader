Shader "Custom/UI/OutlineEx"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "OUTLINE"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;       // 用于使用图集的情景。用于补偿采样结果，保证 UI 显示和单独贴图一致。
            float4 _MainTex_TexelSize;

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float2 uvOriginXY : TEXCOORD1;
                float2 uvOriginZW : TEXCOORD2;
                fixed4 color : COLOR;
            };

            v2f vert(appdata IN)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.texcoord = IN.texcoord;
                o.uvOriginXY = IN.texcoord1;
                o.uvOriginZW = IN.texcoord2;
                o.color = IN.color * _Color;

                return o;
            }

            // 是否在矩形内
            // step(a, b) = float2(
            //     b.x >= a.x ? 1 : 0,
            //     b.y >= a.y ? 1 : 0
            // )
            fixed IsInRect(float2 pPos, float2 pClipRectXY, float2 pClipRectZW)
            {
                pPos = step(pClipRectXY, pPos) * step(pPos, pClipRectZW);
                return pPos.x * pPos.y;
            }

            // 采样偏移的像素点的Alpha值
            //
            fixed SampleAlpha(int pIndex, v2f IN)
            {
                const fixed sinArray[12] = { 0, 0.5, 0.866, 1, 0.866, 0.5, 0, -0.5, -0.866, -1, -0.866, -0.5 };
                const fixed cosArray[12] = { 1, 0.866, 0.5, 0, -0.5, -0.866, -1, -0.866, -0.5, 0, 0.5, 0.866 };
                float2 pos = IN.texcoord + _MainTex_TexelSize.xy * float2(cosArray[pIndex], sinArray[pIndex]) * _OutlineWidth;
                return IsInRect(pos, IN.uvOriginXY, IN.uvOriginZW) * (tex2D(_MainTex, pos) + _TextureSampleAdd).w * _OutlineColor.w* _Color.w;
            }

            
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                if (_OutlineWidth > 0)
                {
                    // color.a *= IsInRect(IN.texcoord, IN.uvOriginXY, IN.uvOriginZW);
                    // 计算此像素点是否为描边，特征就是采样后w值>0
                    half4 val = half4(_OutlineColor.x, _OutlineColor.y, _OutlineColor.z, 0);

                    val.w += SampleAlpha(0, IN);
                    val.w += SampleAlpha(1, IN);
                    val.w += SampleAlpha(2, IN);
                    val.w += SampleAlpha(3, IN);
                    val.w += SampleAlpha(4, IN);
                    val.w += SampleAlpha(5, IN);
                    val.w += SampleAlpha(6, IN);
                    val.w += SampleAlpha(7, IN);
                    val.w += SampleAlpha(8, IN);
                    val.w += SampleAlpha(9, IN);
                    val.w += SampleAlpha(10, IN);
                    val.w += SampleAlpha(11, IN);

                    val.w = clamp(val.w, 0, 1);
                    // 这里，中间会有透明空洞。因为color的a乘了两次。(1.0 - color.a) + color.a * color.a < 1
                    // color = (val * (1.0 - color.a)) + (color * color.a);
                    fixed4 scale = fixed4(color.a, color.a, color.a, 1);
                    color = (val * (1.0 - color.a)) + (color * scale);

                    // todo 
                    // * _Color.a 还不行，奇怪的bug。描边还会100%不透明。
                    // 研究明白了，原来没从Image那里传过去到这里。
                    // Image.Color 传到了 IN.color里了
                    color.a = color.a * IN.color.a;

                }
                return color;
            }
            ENDCG
        }
    }
}