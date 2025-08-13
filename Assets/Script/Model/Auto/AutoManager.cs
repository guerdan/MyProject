
namespace Script.Model.Auto
{
    public class AutoManager
    {
        private static AutoManager _inst;
        public static AutoManager Inst
        {
            get
            {
                if (_inst == null) _inst = new AutoManager();
                return _inst;
            }
        }

        

        private AutoManager()
        {
        }

        public void Update()
        {
            // 更新逻辑
        }

    }
}