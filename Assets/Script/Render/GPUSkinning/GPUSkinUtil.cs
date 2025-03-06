
using UnityEngine;

namespace Script.Render.GPUSkinning
{
    public class GPUSkinUtil
    {
        public static bool UseMsg = true;

        public static void Msg(string info, bool warning = false)
        {
            if (!UseMsg) return;
            if (warning)
                Debug.LogWarning(string.Format("{0}--{1}", "烘焙动画纹理", info));
            else
                Debug.Log(string.Format("{0}--{1}", "烘焙动画纹理", info));
        }


        // 前提：v是0-1之间的浮点数
        // https://blog.csdn.net/zengjunjie59/article/details/111405894
        // 单单看代码实在让人摸不着头脑，展开后可得EncodeFloatRGBA的最终结果为：
        // x是v  
        // y是(v mod 1/255）* 255  
        // z是(v mod 1/255^2）* 255^2  
        // w是(v mod 1/255^3）* 255^3
        // 举个例子，比如现在有个float类型的值h=1.23456…，
        // 我如果把h存在一个int整形的变量里面，那么小数后面的精度肯定丢失了。
        // 如果我想保留，那么可以用四个整形变量a, r, g, b来保存，a = 1, r = 2, g = 3, b = 4。
        // 这样g可以表示为:h = a+0.1r+0.01g+0.001*b。这个原理其实和上面的Encode和Decode是一样的，
        // 只不过这个例子用的是10倍，而U3D中是255倍。
        public static Vector4 EncodeFloatRGBA(float v)
        {
            v = v * 0.01f + 0.5f;
            if (v > 1) Msg("精度丢失！");
            Vector4 kEncodeMul = new Vector4(1.0f, 255.0f, 65025.0f, 160581375.0f);
            float kEncodeBit = 1.0f / 255.0f;
            Vector4 enc = kEncodeMul * v;
            for (int i = 0; i < 4; i++)
                enc[i] = enc[i] - Mathf.Floor(enc[i]); //取余
            enc = enc - new Vector4(enc.y, enc.z, enc.w, enc.w) * kEncodeBit;
            return enc;
        }
    }
}