
using System.Numerics;
using System.Runtime.CompilerServices;
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
               for (int i = 0; i < 10000000; i++)   //29ms 
               {
                   b = a1.GetHashCode();
               }
           }, $"哈希 {b}");
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

    }
    public struct StructA
    {
        public int a;
        public int a1;
        public int a2;

    }
}