
using System.Numerics;
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

        // 结论：赋值Vector2Int的执行耗时 是 赋值int的4倍
        public void Test1()
        {
            DU.RunWithTimer(() =>
           {
               for (int i = 0; i < 10000000; i++)
               {
                   forTest1_a = new Vector2Int(-1, -1);
               }
           }, "赋值Vector2Int");

            DU.RunWithTimer(() =>
            {
                for (int i = 0; i < 10000000; i++)
                {
                    forTest1_b = 1;
                }
            }, "赋值int");
        }

    }
}