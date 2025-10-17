
using Script.Model.Auto;
using Script.Util;

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

    }
}