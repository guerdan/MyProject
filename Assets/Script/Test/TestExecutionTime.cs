
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using OpenCvSharp;
using Script.Framework.Else;
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


        private static bool _dummyBool;
        private static bool _dummyBool1;
        private static bool _dummyBool2;
        /// <summary>
        /// 比较操作
        /// 
        /// 结论：
        /// int比较 = byte比较 = long比较 -3ms
        /// 枚举比较-3ms
        /// 字符串比较-(长度不一样220ms)(长度一样1000ms)
        /// 浮点整型互转-50ms
        /// >=和>耗时相同
        /// </summary>
        public void Test()
        {

            //
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)      //30ms
                {
                    // var a = (i & (1 << 5)) != 0;
                }
            }, "位与+比较");


            // DU.RunWithTimer(() =>
            // {
            //     var b = BigCellType.AllEmpty;
            //     for (int i = 0; i < 10000000; i++)      //30ms
            //     {
            //         var a = b == BigCellType.HasObstacle;
            //     }
            // }, "枚举比较");

            DU.RunWithTimer(() =>
            {
                var b = 2;
                for (int i = 0; i < 10000000; i++)
                {
                    var a = b == 3;                     // 每次6ms
                }
            }, "整型比较");

            DU.RunWithTimer(() =>
            {
                byte b = 2;
                byte c = 6;
                for (int i = 0; i < 10000000; i++)
                {
                    var a = b == c;                     // 每次6ms
                }
            }, "byte比较");

            var s1 = DU.RunWithTimer(() =>
            {
                bool result = false;
                bool result1 = false;
                bool result2 = false;
                long b = DateTime.UtcNow.Ticks;
                long c = DateTime.UtcNow.Ticks;
                long b1 = DateTime.UtcNow.Ticks;
                long c1 = DateTime.UtcNow.Ticks;
                long b2 = DateTime.UtcNow.Ticks;
                long c2 = DateTime.UtcNow.Ticks;

                for (int i = 0; i < 10000000; i++)
                {
                    result = b == c;                     // 每次6ms
                    result1 = b1 == c1;                     // 每次6ms
                    result2 = b2 == c2;                     // 每次6ms
                }

                _dummyBool = result;
                _dummyBool1 = result1;
                _dummyBool2 = result2;
            }, "long比较");

            ConsolePrint(s1);

            string str = "aaasdasdasdasfsdfasdfasdfasdfasfaaasdasdasdasfsdfasdfasdfasdfasfaaasdasdasdasfsdfasdfasdfasdfasf";
            string str1 = "aaasdasdasdasfsdfasdfasdfasdfasfaaasdasdasdasfsdfasdfasdfasdfasfaaasdasdasdasfsdfasdfasdfasdfask";
            // string str = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            // string str1 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbba";
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)      //看长度
                {
                    var a = object.ReferenceEquals(str, str1);                     //
                }
            }, "字符串比较");

            // ClassA a = new ClassA();
            // ClassA b = new ClassA();
            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)      //30ms
            //     {
            //         var ab = a == b;
            //     }
            // }, "引用比较");

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)      //30ms
            //     {
            //         float a = i;
            //         int b = (int)a;
            //     }
            // }, "浮点整型互转");


            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)      //35ms
            //     {
            //         bool k = 3 >= 2;
            //     }
            // }, ">=比较");
            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)      //35ms
            //     {
            //         bool k = 3 > 2;
            //     }
            // }, ">比较");
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
        // new操作：Class-970ms, Struct-10ms, Action-10ms
        // 赋空值差不多一样
        public void Test2()
        {

            ClassA obj = new ClassA();
            StructA stru = new StructA();

            // var map = new ClassA[1, 1];
            // map[0, 0] = obj;
            // var map1 = new StructA[1, 1];
            // map1[0, 0] = stru;

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //33ms 
               {
               }
           }, "空转");

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)   //970ms
               {
                   var b = new ClassA();
               }
           }, "创建 Class");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)  //36ms
                {
                    StructA b = new StructA();
                }
            }, "创建 Struct");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)  //36ms  在栈上创建方法对象
                {
                    Action b = () => { var a = 2; };
                }
            }, "创建 方法对象");


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

        /// <summary>
        /// 一次最少1ms，随着图变大，匹配次数变多，会慢慢涨。平均20ms
        /// </summary>
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
                }
            }, $"暂存对象字段，再访问");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)    // 36ms 
                {
                    var a = b;
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

        // 
        //  int字典比string字典要快一些。int = 8次空方法 string = 13次空方法
        /// <summary>
        /// 测试查字典的耗时。
        /// 
        /// string字典-1000ms               真机 300ms
        /// int字典 = char字典 -500ms       真机 110ms
        /// Vector2Int字典-1000ms           真机 300ms
        /// </summary>
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

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    // 30ms 
            //     {
            //     }
            // }, $"空");

            // var s1 = DU.RunWithTimer(() =>
            //  {
            //      for (int i = 0; i < 10000000; i++)    // 1800ms
            //      {
            //          var k = dic.TryGetValue("2", out _);
            //      }
            //  }, $"查string字典 Try");
            // ConsolePrint(s1);

            // var s2 = DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    //1200ms
            //     {
            //         var k = dic["2"];
            //     }
            // }, $"查string字典 []");
            // ConsolePrint(s2);

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    // 800ms
            //     {
            //         var k = int_dic.TryGetValue(2, out _);
            //     }
            // }, $"查int字典 Try");

            var s3 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)    //500ms
                {
                    var k = int_dic[2];
                }
            }, $"查int字典 []");
            ConsolePrint(s3);

            // Dictionary<Tuple4Long, string> dic_tuple = new Dictionary<Tuple4Long, string>();
            // var tuple_item = new Tuple4Long(1, 1, 1, 1);
            // dic_tuple[tuple_item] = "good";

            // var s4 = DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    //500ms
            //     {
            //         var k = dic_tuple[tuple_item];
            //     }
            // }, $"查Tuple5Long字典 []");
            // ConsolePrint(s4);


            // Dictionary<Vector2Int, string> dic_v2 = new Dictionary<Vector2Int, string>();
            // var v2_item = new Vector2Int(1, 1);
            // dic_v2[v2_item] = "good";
            // var s5 = DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)    
            //     {
            //         var k = dic_v2[v2_item];
            //     }
            // }, $"查Vector2Int字典 []");
            // ConsolePrint(s5);

            Dictionary<char, string> dic_c = new Dictionary<char, string>();
            var c_item = 'c';
            dic_c[c_item] = "good";
            var s6 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)
                {
                    var k = dic_c[c_item];
                }
            }, $"查char字典 []");
            ConsolePrint(s6);


            var hashset = new HashSet<string>();
            string key = "MwD2D2YA0vB/AMGABfAHCIL9A38PYwALAAcA";
            hashset.Add(key); 
            var s7 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)    //
                {
                    bool k = hashset.Contains(key);
                }
            }, $"查string集合");
            ConsolePrint(s7);
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
        /// 2. 字符串字典取值 至少1200ms，本质上是用字符串每个字母算hash值。所以越长越耗时
        /// 3. 取对象的类型 2700ms
        /// </summary>
        public void Test13()
        {
            object obj = 10000000;
            int obj_i = (int)obj;

            Dictionary<string, object> dic = new Dictionary<string, object>();
            string key = "akkjinbvyugiukjbkvbhuvufhbkbkbkhakkjinbvyugiukjbkvbhuvufhbkbkbkhakkjinbvyugiukjbkvbhuvufhbkbkbkhakkjinbvyugiukjbkvbhuvufhbkbkbkh";
            dic[key] = 10000000;

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 1000000; i++)
            //     {
            //         int a = (int)obj;     // 20ms
            //     }
            // }, $"拆箱");

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 1000000; i++)
            //     {
            //         object a = obj_i;     // 1000ms
            //     }
            // }, $"装箱");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    object a = dic[key];     // 1200ms
                }
            }, $"字符串字典取值");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 1000000; i++)
            //        {
            //            string n = obj.GetType().Name;    // 2700ms
            //        }
            //    }, $"取对象的类型");



        }

        /// <summary>
        /// 一些接口耗时
        /// 
        /// 空方法-50ms
        /// float.TryParse()-8000ms
        /// string.IndexOf()-5000ms
        /// Screen.height()-500ms
        /// Array.Copy()比for要快10倍
        /// </summary>
        public void Test14()
        {
            int[] list1 = new int[10000000];
            int[] list2 = new int[10000000];

            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)
               {
                   //    EmptyMethod();                   
               }
           }, $"空方法");


            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 100; i++)
               {
                   Array.Copy(list1, list2, 1000000);     //200ms 0.02         
               }
           }, $"数组拷贝");

            //     DU.RunWithTimer(() =>
            //    {
            //        for (int i = 0; i < 1000000; i++)
            //        {
            //            float.TryParse("3.14", out var f);
            //        }
            //    }, $"float.TryParse()");

            //     DU.RunWithTimer(() =>
            //     {
            //         for (int i = 0; i < 1000000; i++)
            //         {
            //             var is_method = "ssssaaadfasddass".IndexOf("{") >= 0;
            //         }
            //     }, $"string.IndexOf()");

            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)
            //     {
            //         var k = Screen.height;
            //     }
            // }, $"Screen.height()");

        }

        /// <summary>
        /// 图像识别专场
        /// 几乎一样的。如果要转到Color32[] 则会耗时激增—50ms。转到Vec3b[]—1ms
        /// </summary>
        public void Test15()
        {
            Mat mat = IU.GetMat($"{Application.streamingAssetsPath}/木质1.png");
            Bitmap bitmap = new Bitmap($"{Application.streamingAssetsPath}/木质1.png");
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 100; i++)       // 50ms
                {
                    var colors = IU.BitmapToColor32(bitmap);
                }
            }, $"BitmapToColor32()");


            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 100; i++)       // 50ms
                {
                    var colors = IU.MatToColor32(mat);
                }
            }, $"MatToColor32()");

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EmptyMethod()
        {

        }

        /// <summary>
        /// 1层遍历比2层遍历略微快一些
        /// </summary>
        public void Test16()
        {
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 24ms
                {
                }
            }, $"1层遍历");
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000; i++)       // 28ms
                {
                    for (int j = 0; j < 1000; j++)
                    {

                    }
                }
            }, $"2层遍历");
        }

        /// <summary>
        /// 列表专题
        /// 自定义的SList 性能优于 系统自带List。它没有额外开销
        /// 
        /// 真机上，循环开销可以忽略不计。方法可以内联
        /// </summary>
        public void Test17()
        {
            var s1 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机6ms
                {

                }
            }, $"空");
            ConsolePrint(s1);

            var List = new List<int>(10000000);

            var s2 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机8ms
                {
                    var a = List.Count;
                }
            }, $"系统List.Count访问");
            ConsolePrint(s2);

            var s3 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机26ms
                {
                    List.Add(i);
                }
            }, $"系统List.Add()");
            ConsolePrint(s3);

            var s4 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机31ms
                {
                    var a = List[i];
                }
            }, $"系统List取值");
            ConsolePrint(s4);

            var s5 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机38ms
                {
                    List[i] = i;
                }
            }, $"系统List赋值");
            ConsolePrint(s5);


            SList<int> l = new SList<int>(10000000);

            var s6 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机6.7ms
                {
                    var a = l.Count;
                }
            }, $"自定义SList.Count访问");
            ConsolePrint(s6);

            var s7 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机22.5ms
                {
                    // l[l.Count++] = i;
                    l.Add(i);                // 测试下来，居然Count++(完整的内存读写)的耗时最高，
                }
            }, $"自定义SList.Add()");
            ConsolePrint(s7);


            var s8 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机10ms
                {
                    var a = l[i];
                    var b = l[i];
                    var c = l[i];
                }
            }, $"自定义SList取值");
            ConsolePrint(s8);

            var s9 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 真机10.6ms
                {
                    l[i] = i;
                    l[i] = i;
                    l[i] = i;
                }
            }, $"自定义SList赋值");
            ConsolePrint(s9);

        }


        public void ConsolePrint(string str)
        {
            AutoScriptManager.Inst.AddLog(ScriptLogType.Warning, str);
        }

        /// <summary>
        /// 循环与if嵌套
        /// 结论：1.能把if移到外面的尽量移外面去。
        /// 2.switch能够加速 if逻辑
        /// </summary>
        public void Test18()
        {
            var type = 20;

            var s1 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)      // 编辑器 370ms | 67ms(项数变多，耗时没有涨)
                {                                       // 真机 30ms | 12ms(项数变多，会涨)
                    var b = 0;
                    if (type == 1)
                        b = 1;
                    else if (type == 2)
                        b = 2;
                    else if (type == 3)
                        b = 3;
                    else if (type == 4)
                        b = 4;
                    else if (type == 5)
                        b = 5;
                    else if (type == 6)
                        b = 6;
                    else if (type == 7)
                        b = 7;
                    else if (type == 8)
                        b = 8;
                    else if (type == 9)
                        b = 9;
                    else if (type == 10)
                        b = 10;
                    else if (type == 11)
                        b = 11;
                    else if (type == 12)
                        b = 12;
                    else if (type == 13)
                        b = 13;
                    else if (type == 14)
                        b = 14;
                    else if (type == 15)
                        b = 15;
                    else if (type == 16)
                        b = 16;
                    else if (type == 17)
                        b = 17;
                    else if (type == 18)
                        b = 18;
                    else if (type == 19)
                        b = 19;
                    else if (type == 20)
                        b = 20;

                    // switch (type)               // 12ms
                    // {
                    //     case 1:
                    //         b = 1; break;
                    //     case 2:
                    //         b = 2; break;
                    //     case 3:
                    //         b = 3; break;
                    //     case 4:
                    //         b = 4; break;
                    //     case 5:
                    //         b = 5; break;
                    //     case 6:
                    //         b = 6; break;
                    //     case 7:
                    //         b = 7; break;
                    //     case 8:
                    //         b = 8; break;
                    //     case 9:
                    //         b = 9; break;
                    //     case 10:
                    //         b = 10; break;
                    //     case 11:
                    //         b = 11; break;
                    //     case 12:
                    //         b = 12; break;
                    //     case 13:
                    //         b = 13; break;
                    //     case 14:
                    //         b = 14; break;
                    //     case 15:
                    //         b = 15; break;
                    //     case 16:
                    //         b = 16; break;
                    //     case 17:
                    //         b = 17; break;
                    //     case 18:
                    //         b = 18; break;
                    //     case 19:
                    //         b = 19; break;
                    //     case 20:
                    //         b = 20; break;
                    // }
                }
            }, $"循环内if判断");
            ConsolePrint(s1);

            var s2 = DU.RunWithTimer(() =>
            {
                var b = 0;
                if (type == 1)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 1;
                    }
                else if (type == 2)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 2;
                    }
                else if (type == 3)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 3;
                    }
                else if (type == 4)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 4;
                    }
                else if (type == 5)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 5;
                    }
                else if (type == 6)
                    for (int i = 0; i < 10000000; i++)
                    {
                        b = 6;
                    }

            }, $"循环外if判断");
            ConsolePrint(s2);

        }


        /// <summary>
        /// CopyFromScreen - 27ms         0像素保底-13ms
        /// </summary>
        public void Test19()
        {

            DU.RunWithTimer(() =>
            {
                // var size = new System.Drawing.Size(Utils.ScreenWidth, Utils.ScreenHeight);
                var size = new System.Drawing.Size(Utils.ScreenWidth / 2, Utils.ScreenHeight);
                // var size = new System.Drawing.Size(1, 1);
                using (Bitmap screenBmp = new Bitmap(size.Width, size.Height))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(screenBmp))
                    {
                        //第1、2个参数：屏幕起始坐标（如 0,0）
                        //第3、4个参数：目标 Bitmap 的起始坐标（如 0,0）
                        //第5个参数：复制的区域大小
                        g.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);
                    }
                }

            }, $"CopyFromScreen 32");

        }

        /// <summary>
        /// 字符串专题
        /// 
        /// string常量池只包括：string字面量 + 编译期常量拼接。走的是复用内存
        /// </summary>
        public void Test20()
        {
            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 48
                {
                }
            }, $"空");


            string str = "aaaaaaaaaaa";
            var s0 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // 65 _ 17ms  
                {
                    char c = str[10];
                }
            }, $"string取char");
            ConsolePrint(s0);


            var s1 = DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)       // _3ms
                {
                    var c = str.Length;                 // 为字段
                }
            }, $"Length");

            // var s2= DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000000; i++)       // 2000ms
            //     {
            //         var c = str.ToCharArray();
            //     }
            // }, $"ToCharArray()");
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