using UnityEngine;

namespace Script
{
    
    public class Node
    {
        Vector2 m_position;//下标
        public Vector2 position => m_position;
        public Node parent;//上一个node
    
        //角色到该节点的实际距离
        int m_g;
        public int g {
            get => m_g;
            set {
                m_g = value;
                m_f = m_g + m_h;
            }
        }

        //该节点到目的地的估价距离
        int m_h;
        public int h {
            get => m_h;
            set {
                m_h = value;
                m_f = m_g + m_h;
            }
        }

        int m_f;
        public int f => m_f;

        public Node(Vector2 pos, Node parent, int g, int h) {
            m_position = pos;
            this.parent = parent;
            m_g = g;
            m_h = h;
            m_f = m_g + m_h;
        }
    }
    public class AStarLogic
    {
        private static AStarLogic _inst;

        public static AStarLogic inst
        {
            get
            {
                if (_inst == null) _inst = new AStarLogic();
                return _inst;
            }
        }
        
        
    }
}