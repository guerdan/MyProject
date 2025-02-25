
namespace Script.XBattle.QuadTree
{
    public class QuadTreeManager
    {
        private static QuadTreeManager _inst;

        public static QuadTreeManager inst
        {
            get
            {
                _inst = _inst == null ? new QuadTreeManager() : _inst;
                return _inst;
            }
        }

        public QuadTreeManager()
        {
        }

        public void init(string name, int row, int column)
        {

        }

        public QuadTree GetTree(string name)
        {
            return null;
        }
    }

    public class QuadTree
    {
         

    }
}