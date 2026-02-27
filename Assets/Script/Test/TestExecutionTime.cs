
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Model.Auto;
using Script.Util;
using UnityEngine;

namespace Script.Test
{
    public class TestExecutionTime
    {
        private static TestExecutionTime _inst;
        public static TestExecutionTime Inst
        { get { if (_inst == null) _inst = new TestExecutionTime(); return _inst; } }



        public void Test()
        {
            //结论，三种比较几乎一样耗时。
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)
                {
                    var a = (i & (1 << 5)) != 0;
                }
            }, "位与+比较");


            DU.RunWithTimer(() =>
            {
                var b = BigCellType.AllEmpty;
                for (int i = 0; i < 10000000; i++)
                {
                    var a = b == BigCellType.HasObstacle;
                }
            }, "枚举比较");

            DU.RunWithTimer(() =>
            {
                var b = 2;
                for (int i = 0; i < 10000000; i++)
                {
                    var a = b == 3;
                }
            }, "数字比较");


        }
        Vector2Int forTest1_a;
        int forTest1_b;

        // 结论：赋值:Vector2Int 与 int 相差不大
        // 构造：相差巨大。Vector2Int通过函数构造会有函数开销，用默认构造函数无函数开销
        public void Test1()
        {
            Vector2Int a;
            Vector2Int a1 = Vector2Int.one;
            int b;
            StructT s;

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //29ms 
               {
               }
           }, "空转");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)
            //        {
            //               forTest1_a = a1;                 //34ms
            //            //    s = new StructA();                 //34ms
            //            //    forTest1_a = Vector2Int.one;     //120ms
            //            //    forTest1_a = new Vector2Int(1,1);     //120ms
            //        }
            //    }, "赋值字段-Vector2Int");

            //     DU.RunWithTimer(() =>
            //     {
            //         for (int i = 0; i < 10000000; i++)
            //         {
            //             forTest1_b = 1;                 //29ms 这个应该是被"即时编译"给优化了
            //         }
            //     }, "赋值字段-int");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)
               {
                   a = a1;                          //37ms
               }
           }, "赋值栈变量-Vector2Int");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //94ms
               {
                   a = new Vector2Int(1, 1);
               }
           }, "赋值栈变量-Vector2Int 构造函数");

            DU.RunWithTimer(() =>
          {
              for (int i = 0; i < 10000000; i++)    //94ms
              {
                  s = new StructT(2, 3);
              }
          }, "赋值栈变量-StructT 构造函数");
            DU.RunWithTimer(() =>
          {
              for (int i = 0; i < 10000000; i++)    //41ms
              {
                  s = new StructT();
                  //   s = default;                   //两者一样
                  s.a = 2;
                  s.a1 = 3;
              }
          }, "赋值栈变量-StructT 构造函数 分开");


            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)  //29ms
                {
                    b = i;
                }
            }, "赋值栈变量-int");
        }


        // 结论是：
        // 访问字段：如果对象已经取到栈上，那么相当于有个副本了并几乎一样。
        //          如果对象不在栈上，就多了取到栈上的耗时。
        // new操作：Class 970ms, Struct 36ms  ;30倍;  回收没估算
        // 赋空值差不多一样
        public void Test2()
        {

            ClassA obj = new ClassA();
            StructA stru = new StructA();

            var map = new ClassA[1, 1];
            map[0, 0] = obj;
            var map1 = new StructA[1, 1];
            map1[0, 0] = stru;

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //33ms 
               {
               }
           }, "空转");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //
               {
                   var b = map[0, 0].a;
               }
           }, "访问字段-Class");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)  //
                {
                    var b = map1[0, 0].a;
                }
            }, "访问字段-Struct");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)   //970ms
            //        {
            //            var b = new ClassA();
            //        }
            //    }, "赋值-Class");

            //     DU.RunWithTimer(() =>
            //     {
            //         for (int i = 0; i < 10000000; i++)  //36ms
            //         {
            //             StructA b = new StructA();
            //         }
            //     }, "赋值-Struct");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)
            //        {
            //            ClassA b = null;
            //        }
            //    }, "赋空-Class");

            //     DU.RunWithTimer(() =>
            //     {
            //         for (int i = 0; i < 10000000; i++)  //34
            //         {
            //             StructA b = default;
            //         }
            //     }, "赋空-Struct");
        }

        public void Test3()
        {
            Vector2Int a1 = new Vector2Int(100, 190);
            int b = 0;
            DU.LogWarning($"{new Vector2Int(100, 190).GetHashCode()}");
            DU.LogWarning($"{new Vector2Int(100, 110).GetHashCode()}");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //300ms 
               {
                   b = a1.GetHashCode();
               }
           }, $"哈希 {b}");
        }

        PixType[,] map0 = new PixType[1000, 1000];
        PixType[] map1 = new PixType[1000];
        HashSet<Vector2Int> set = new HashSet<Vector2Int>();
        List<Vector2Int> list = new List<Vector2Int>(100000000);
        public void Test4()
        {
            for (int i = 0; i < 1000; i++)
            {
                set.Add(new Vector2Int(i + 100000, i + 100000));
            }

            var b = new Vector2Int(2, 3);

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)   // 30ms
                {
                    var a = map1[500];
                }
            }, $"一维数组取");
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)   // 24ms
                {
                    map1[500] = PixType.Empty;
                }
            }, $"一维数组存");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)   // 40ms
                {
                    var a = map0[500, 500];
                }
            }, $"二维数组取");
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)   // 40ms
                {
                    map0[500, 500] = PixType.Empty;

                }
            }, $"二维数组存");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)   // 换算后 600ms 
                {
                    bool a = set.Contains(b);
                }
            }, $"哈希表判断");
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 100000; i++)    // 换算后 1300ms
                {
                    set.Add(new Vector2Int(i, i));
                }
            }, $"哈希表存");


            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)    // 170ms 
                {
                    list.Add(b);
                }
            }, $"列表存");
        }

        public void Test5()
        {
            double b = 0;
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)    // 60ms
                {
                    var dx = i - 200;
                    var dy = i - 200;

                    double b = Math.Sqrt(dx * dx + dy * dy);
                }
            }, $"开根号1");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)    // 8ms
                {
                    var dx = i - 200;
                    dx = dx < 0 ? -dx : dx;
                    var dy = i - 200;
                    dy = dy < 0 ? -dy : dy;

                    if (dx > dy)
                        b = 1414 * dy + 1000 * (dx - dy);
                    else
                        b = 1414 * dx + 1000 * (dy - dx);
                }
            }, $"开根号2");
        }

        public void Test6()
        {
            DU.RunWithTimer(() =>
           {
               for (int i = -1000000; i < 1000000; i++)    // 36ms 
               {
                   var b = Math.Abs(i);
               }
           }, $"Math方法");
            DU.RunWithTimer(() =>
           {
               for (int i = -1000000; i < 1000000; i++)    // 8ms 
               {
                   var b = i < 0 ? -i : i;
               }
           }, $"手动判断");

        }

        public void Test7()
        {
            var pathI = $"{Application.streamingAssetsPath}/木质1.png";
            var pathT = $"{Application.streamingAssetsPath}/木质1.png";
            var matI = IU.GetMat(pathI);
            var matT = IU.GetMat(pathT);
            matI = new Mat(144, 140, MatType.CV_8UC3);      //生成的是随机图像
            matT = new Mat(144, 140, MatType.CV_8UC3);      //生成的是随机图像
            // Cv2.ImShow("matInput",matI);


            List<CVMatchResult> resultL = null;

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000; i++)    // 
                {
                    // var result = IU.MatchTemplate1(matI, matT, true, TemplateMatchModes.SqDiff);
                    using (var result = IU.MatchTemplate1(matI, matT, true, TemplateMatchModes.CCorrNormed))
                    {
                        if (resultL == null)
                            resultL = IU.FindResult(result, 140, 144, 0, out _);
                    }
                }
            }, $"模版匹配");


            // DU.LogWarning($"结果：{resultL[0].Score}");


            // DU.RunWithTimer(() =>
            // {
            //     for (int i = -1000000; i < 1000000; i++)    // 8ms 
            //     {
            //         var b = i < 0 ? -i : i;
            //     }
            // }, $"手动判断");

        }

        public void Test8()
        {
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)    // 26ms 
                {
                }
            }, $"单层循环");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000; i++)    // 29ms 
                {
                    for (int j = 0; j < 1000; j++)
                    {
                    }
                }
            }, $"双层循环");
        }



        // 1. 访问栈上声明字段 = 访问对象字段 (联级访问与次数成正比)
        // 2. 调用方法(只算壳)50ms = 10倍 访问字段3.5ms
        // 3. List等高封装数据结构的Count是属性,属性是方法。int[]是基础列表，Length是字段。
        public void Test9()
        {
            var list = new int[1000000];
            var list1 = new List<int>();
            for (int i = 0; i < 10000000; i++)
            {
                list1.Add(0);
            }

            var a = new ClassA();
            a.b = new ClassB();
            var b = 20;

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)    // 42ms 
                {
                    var k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                    k = a.b.a;
                }
            }, $"暂存对象字段，再访问");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)    // 36ms 
                {
                    var a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                    a = b;
                }
            }, $"访问栈对象");
            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    // 36ms 
            //     {
            //         var a = 20;
            //     }
            // }, $"访问栈对象");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)    // 30ms 
               {
               }
           }, $"空");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < list1.Count; i++)    // 88ms 
                {
                }
            }, $"对象方法");

        }

        int field_1 = 10000000;
        int field_2 => field_1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetNumber()
        {
            return 10000000;
        }

        // 测试查string字典的耗时。 int字典比string字典要快一些。int = 8次空方法 string = 13次空方法
        public void Test10()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic["2"] = "3";
            dic["3"] = "3";
            dic["4"] = "3";
            dic["5"] = "3";
            dic["6"] = "3";
            Dictionary<int, string> int_dic = new Dictionary<int, string>();
            int_dic[2] = "3";
            int_dic[3] = "3";
            int_dic[4] = "3";
            int_dic[5] = "3";
            int_dic[6] = "3";

            List<string> list = new List<string>() { "2", "2", "2", "2", "2", "2", "2", "2", "2", "2" };

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)    // 30ms 
                {
                }
            }, $"空");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)    // 1100ms
            //        {
            //            var k = dic.TryGetValue("2", out _);
            //        }
            //    }, $"查string字典 Try");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)    //800ms
            //        {
            //            var k = dic["2"];
            //        }
            //    }, $"查string字典 []");
            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)    // 800ms
               {
                   var k = int_dic.TryGetValue(2, out _);
               }
           }, $"查int字典 Try");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)    //500ms
               {
                   var k = int_dic[2];
               }
           }, $"查int字典 []");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 10000000; i++)    //8000ms
            //        {
            //            var k = list.IndexOf("3");
            //        }
            //    }, $"查string list");
        }
        /// <summary>
        /// 测试截图性能,
        /// 1. 全屏截图一次 33ms      范围缩10倍——耗时缩2倍
        /// </summary>
        public void Test11()
        {
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1; i++)    //
                {
                    using (var bitmap = WU.CaptureWindow(new CVRect(0, 0, 1980, 1080)))
                    {
                    }

                    // var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    // tex.ReadPixels(new UnityEngine.Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    // tex.Apply();
                }
            }, $"截图");

        }


        /// <summary>
        /// 封装栈比原生数组要慢6倍
        /// 原生数组 17ms
        /// 封装栈 push 100ms, pop 100ms
        /// </summary>n
        public void Test12()
        {
            int[] stackR = new int[10000000];
            Stack<int> stack = new Stack<int>();

            int stackR_count = 0;
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    stackR[stackR_count++] = i;     //17ms
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                    stackR[stackR_count++] = i;
                }
            }, $"原生数组");

            for (int i = 0; i < 10000000; i++)
            {
                stack.Push(i);
            }

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    // stack.Push(i);                      //100ms
                    stack.Pop();                      //100ms
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                    stack.Pop();
                }
            }, $"封装栈");

        }

        /// <summary>
        /// 研究拆装箱 与 字符串字典的性能
        /// 
        /// 结论是：10000000次循环
        /// 1. 装箱 1000ms
        /// 2. 字符串字典取值 1200ms
        /// 3. 取对象的类型 2700ms
        /// </summary>
        public void Test13()
        {
            object obj = 10000000;
            int obj_i = (int)obj;

            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["a"] = 10000000;

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    int a = (int)obj;     // 20ms
                }
            }, $"拆箱");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    object a = obj_i;     // 1000ms
                }
            }, $"装箱");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    object a = dic["a"];     // 1200ms
                }
            }, $"字符串字典取值");

             DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    string n = obj.GetType().Name;    // 2700ms
                }
            }, $"取对象的类型");

        }
    }


    public struct StructT
    {
        public int a;
        public int a1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructT(int a, int a1)
        {
            this.a = a;
            this.a1 = a1;
        }
    }

    public class ClassA
    {
        public int a;
        public int a1;
        public int a2;
        public ClassB b;

    }
    public struct StructA
    {
        public int a;
        public int a1;
        public int a2;

    }

    public class ClassB
    {
        public int a;
    }
}