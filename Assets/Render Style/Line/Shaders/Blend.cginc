//大多数参考的是ShaderGraph的Blend结点，基本上和PS中的效果一致了(*是ShaderGraph的Blend结点多出来的)
//-1-
//正常
float4 Blend_Normal(float4 Base, float4 Blend, float Opacity)
{
    return lerp(Base, Blend, Opacity);
}
//溶解
// #include "LSQ/Noise/WhiteNoise.cginc"
// float4 Blend_Dissolve(float4 Base, float4 Blend, float Opacity)
// {
//     float randomValue = rand3dTo1d(Blend.rgb);
//     float alpha = step(randomValue, Blend.a * Opacity);
//     return lerp(Base, Blend, alpha);
// }

//-2-
//变暗
float4 Blend_Darken(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = min(Blend, Base);
    return lerp(Base, Out, Opacity);
}
//正片叠底
float4 Blend_Multiply(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base * Blend;
    return lerp(Base, Out, Opacity);
}
//颜色加深
float4 Blend_ColorBurn(float4 Base, float4 Blend, float Opacity)
{
    float4 Out =  1.0 - (1.0 - Base) / Blend;
    return lerp(Base, Out, Opacity);
}
//线性加深
float4 Blend_LinearBurn(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base + Blend - 1.0;
    return lerp(Base, Out, Opacity);
}
//深色
float4 Blend_DarkerColor(float4 Base, float4 Blend, float Opacity)
{
    if((Base.r + Base.g + Base.b) < (Blend.r + Blend.g + Blend.b))
    {
        return Base;
    }
    else
    {
        return Blend;
    }
}

//-3-
//变亮
float4 Blend_Lighten(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = max(Blend, Base);
    return lerp(Base, Out, Opacity);
}
//滤色
float4 Blend_Screen(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = 1.0 - (1.0 - Blend) * (1.0 - Base);
    return lerp(Base, Out, Opacity);
}
//颜色减淡
float4 Blend_ColorDodge(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base / (1.0 - Blend);
    return lerp(Base, Out, Opacity);
}
//线性减淡
float4 Blend_LinearDodge(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base + Blend;
    return lerp(Base, Out, Opacity);
}
//浅色
float4 Blend_LighterColor(float4 Base, float4 Blend, float Opacity)
{
     if((Base.r + Base.g + Base.b) < (Blend.r + Blend.g + Blend.b))
    {
        return Blend;
    }
    else
    {
        return Base;
    }
}

//-4-
//叠加
float4 Blend_Overlay(float4 Base, float4 Blend, float Opacity)
{
    float4 result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
    float4 result2 = 2.0 * Base * Blend;
    float4 zeroOrOne = step(Base, 0.5);
    float4 Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    return lerp(Base, Out, Opacity);
}
//柔光
float4 Blend_SoftLight(float4 Base, float4 Blend, float Opacity)
{
    float4 result1 = 2.0 * Base * Blend + Base * Base * (1.0 - 2.0 * Blend);
    float4 result2 = sqrt(Base) * (2.0 * Blend - 1.0) + 2.0 * Base * (1.0 - Blend);
    float4 zeroOrOne = step(0.5, Blend);
    float4 Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    return lerp(Base, Out, Opacity);
}
//强光
float4 Blend_HardLight(float4 Base, float4 Blend, float Opacity)
{
    float4 result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
    float4 result2 = 2.0 * Base * Blend;
    float4 zeroOrOne = step(Blend, 0.5);
    float4 Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    return lerp(Base, Out, Opacity);
}
//亮光
float4 Blend_VividLight(float4 Base, float4 Blend, float Opacity)
{
    float4 result1 = 1.0 - (1.0 - Blend) / (2.0 * Base);
    float4 result2 = Blend / (2.0 * (1.0 - Base));
    float4 zeroOrOne = step(0.5, Base);
    float4 Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    return lerp(Base, Out, Opacity);
}
//线性光
float4 Blend_LinearLightAddSub(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Blend + 2.0 * Base - 1.0;
    return lerp(Base, Out, Opacity);
}
//限制在0-1的线性光 *
float4 Blend_LinearLight(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Blend < 0.5 ? max(Base + (2 * Blend) - 1, 0) : min(Base + 2 * (Blend - 0.5), 1);
    return lerp(Base, Out, Opacity);
}
//点光
float4 Blend_PinLight(float4 Base, float4 Blend, float Opacity)
{
    float4 check = step (0.5, Blend);
    float4 result1 = check * max(2.0 * (Base - 0.5), Blend);
    float4 Out = result1 + (1.0 - check) * min(2.0 * Base, Blend);
    return lerp(Base, Out, Opacity);
}
//实色混合
float4 Blend_HardMix(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = step(1 - Base, Blend);
    return lerp(Base, Out, Opacity);
}

//-5-
//差值
float4 Blend_Difference(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = abs(Blend - Base);
    return lerp(Base, Out, Opacity);
}
//排除
float4 Blend_Exclusion(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Blend + Base - (2.0 * Blend * Base);
    return lerp(Base, Out, Opacity);
}
//划分
float4 Blend_Divide(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base / (Blend + 0.000000000001);
    return lerp(Base, Out, Opacity);
}
//减去
float4 Blend_Subtract(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = Base - Blend;
    return lerp(Base, Out, Opacity);
}
//反相 *
float4 Blend_Negation(float4 Base, float4 Blend, float Opacity)
{
    float4 Out = 1.0 - abs(1.0 - Blend - Base);
    return lerp(Base, Out, Opacity);
}

//-6-
//  #include "LSQ/ColorSpaceConversion.cginc"
// //色相
// float4 Blend_Hue(float4 Base, float4 Blend, float Opacity)
// {
//     float3 Base_HSV = rgb2hsv(Base.rgb);
//     float3 Blend_HSV = rgb2hsv(Blend.rgb);
//     float3 Out_HSV = float3(Base_HSV.x, Blend_HSV.y, Blend_HSV.z);
//     float4 Out = float4(hsv2rgb(Out_HSV), 1);
//     return lerp(Base, Out, Opacity);
// }
// //饱和度
// float4 Blend_Saturation(float4 Base, float4 Blend, float Opacity)
// {
//     float3 Base_HSV = rgb2hsv(Base.rgb);
//     float3 Blend_HSV = rgb2hsv(Blend.rgb);
//     float3 Out_HSV = float3(Blend_HSV.x, Base_HSV.y, Blend_HSV.z);
//     float4 Out = float4(hsv2rgb(Out_HSV), 1);
//     return lerp(Base, Out, Opacity);
// }
// //颜色
// float4 Blend_Color(float4 Base, float4 Blend, float Opacity)
// {
//     float3 Base_HSV = rgb2hsv(Base.rgb);
//     float3 Blend_HSV = rgb2hsv(Blend.rgb);
//     float3 Out_HSV = float3(Base_HSV.x, Base_HSV.y, Blend_HSV.z);
//     float4 Out = float4(hsv2rgb(Out_HSV), 1);
//     return lerp(Base, Out, Opacity);
// }
// //明度
// float4 Blend_Luminosity(float4 Base, float4 Blend, float Opacity)
// {
//     float3 Base_HSV = rgb2hsv(Base.rgb);
//     float3 Blend_HSV = rgb2hsv(Blend.rgb);
//     float3 Out_HSV = float3(Blend_HSV.x, Blend_HSV.y, Base_HSV.z);
//     float4 Out = float4(hsv2rgb(Out_HSV), 1);
//     return lerp(Base, Out, Opacity);
// }