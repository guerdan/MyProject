
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Script.UI.Test
{
    public class UITest : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                OpenOnePanel();
            }


            if (Input.GetKeyDown(KeyCode.S))
            {
            }

            if (Input.GetKeyDown(KeyCode.D))
            {
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                AssetManager.Inst.ReleaseUnuseAsset();
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
            }


            if (Input.GetKeyDown(KeyCode.L))
            {
                var v1 = EncodeFloatRGBA1(11.1111f);
                var f1 = DecodeFloatRGBA(v1);
                var v2 = EncodeFloatRGBA1(31.1111f);
                var f2 = DecodeFloatRGBA(v2);
                var v3 = EncodeFloatRGBA1(0.1111f);
                var f3 = DecodeFloatRGBA(v3);
                var v4 = EncodeFloatRGBA1(0.123456f);
                var f4 = DecodeFloatRGBA(v4);
                var v5 = EncodeFloatRGBA1(0.78569f);
                var f5 = DecodeFloatRGBA(v5);
                var v6 = EncodeFloatRGBA1(0.3453437f);
                var f6 = DecodeFloatRGBA(v6);
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
            }



            // return;


            // // 标记自定义方法的开始
            // Profiler.BeginSample("CustomMethod");
            // // 调用自定义方法
            // CustomMethod();
            // // 标记自定义方法的结束
            // Profiler.EndSample();



            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            CustomMethod();
            stopwatch.Stop();
            var inter = stopwatch.ElapsedMilliseconds;
            // UnityEngine.Debug.Log($"Execution time for 1,000,000 integer multiplications: {inter} ms");


            if (list.Count >= 30)
            {
                var aver = list.Sum() / list.Count;
                UnityEngine.Debug.Log($"Execution time for 1,000,000 integer multiplications: {aver} ms");
                list.Clear();
            }
            else
            {
                list.Add(inter);
                if (list.Count > 15)
                {
                    var aver = list.Sum() / list.Count;
                    list = list.Where(x => x < aver * 1.2).ToList();
                }
            }
        }
        //前提：v是0-1之间的浮点数
        // x是v  
        // y是(v mod 1/255）* 255  
        // z是(v mod 1/255^2）* 255^2  
        // w是(v mod 1/255^3）* 255^3
        private static Vector4 EncodeFloatRGBA1(float v)
        {
            Vector4 kEncodeMul = new Vector4(1.0f, 255.0f, 65025.0f, 160581375.0f);
            float kEncodeBit = 1.0f / 255.0f;
            Vector4 enc = kEncodeMul * v;
            for (int i = 0; i < 4; i++)
                enc[i] = enc[i] - Mathf.Floor(enc[i]); //取余
            enc = enc - new Vector4(enc.y, enc.z, enc.w, enc.w) * kEncodeBit;
            return enc;
        }
        private static float DecodeFloatRGBA(Vector4 v)
        {
            Vector4 kDecode = new Vector4(1.0f, 1 / 255.0f, 1 / 65025.0f, 1 / 160581375.0f);
            return Vector4.Dot(kDecode, v);
        }

        void OpenOnePanel()
        {
            UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
        }

        List<float> list = new List<float>();

        //测试时，要将变量设置到成员中，避免被当成常数优化掉
        // int a = 2;
        // int b = 3;
        // int c = 4;
        // int d = 5;
        // int e = 6;
        // int g = 7;
        // int h = 8;
        // int b1 = 8;
        // int b2 = 8;
        // int b3 = 8;
        // int b4 = 8;
        // int b5 = 8;
        float a = 1;
        float b = 3.24f;
        float c = 4.24f;
        float d = 5.24f;
        float e = 6.24f;
        float g = 7.24f;
        float h = 8.24f;
        float k = 8.24f;


        void CustomMethod()
        {

            // 自定义方法的代码
            for (int i = 1; i < 10000000; i++)  //2.1   8周期
            {
                // 测试一些工作
                // k = (float)Math.Sqrt(k);  //10ms   60周期
                // h = (float)Math.Round(a + b*k);  //22ms
                // h = (float)Math.Round(a + b*k);  //22ms
                // h = (float)Math.Round(a+ b*k);  //370ms
                // h = (float)Math.Round(b*k);  //370ms
                // h = (float)Math.Round(b);  //
                // a = i;   //3.0ms int转浮点数  2周期 + 2周期存取
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms
                // h = b +c;   //2.8ms   0.25ms  50

                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //
                // a = b + a;   //1.5ns

                // a = b + a;   //
                // c = b + c;   //
                // d = b + d;   //
                // e = b + e;   //
                // g = b + g;   //
                // b1 = b + b1;   //
                // b2 = b + b2;   //
                // b3 = b + b3;   //
                // b4 = b + b4;   //
                // b5 = b + b5;   //0.5ns


                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //
                // a = b * a;   //145  374/8

                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //
                // a = a / b;   //580   560


                // a = b / c;
                // a = b *c * d*e*e*e*e*e*e;   //0.2ms
                // a = 1f / b/c/d/;   //6.4ms  15周期 + 2周期存取
            }

            // ScriptTest();
        }


        void ScriptTest()    //50ms
        {
            int a1 = 10000;
            int a2 = 1;
            int y = 0;
            int ystep = 1;

            for (int i = 0; i < 10000000; i++)   //21ms
            {
                a1 -= a2;  //15ms
                if (a1 < 0)
                {
                    y = y + ystep;
                    a1 = a1 + a2;  //15ms
                }
            }
        }
    }
}

