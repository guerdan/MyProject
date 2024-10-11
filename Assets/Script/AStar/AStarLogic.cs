using UnityEngine;
using System.Collections.Generic;
using UnityEngine;

public class AStarPathFinding
{
    public Node[,] grid = null; // 假设有一个Grid类负责处理网格数据
    public Node startNode;
    public Node targetNode;

    private List<Node> openList = new List<Node>();
    private HashSet<Node> closedList = new HashSet<Node>();

    public void init(int row, int column, Vector2Int start, Vector2Int target)
    {
        grid = new Node[row, column];
        for (int i = 0; i < row; i++)
        {
            for (int j = 0; j < column; j++)
            {
                grid[i, j] = new Node() { GridX = j, GridY = i };
            }
        }

        this.startNode = grid[start.x, start.y];
        this.targetNode = grid[target.x, target.y];
    }

    public void FindPathStep()
    {
        if (grid == null) return;

        openList.Clear();
        closedList.Clear();

        startNode.G = 0;
        startNode.H = Heuristic(startNode, targetNode); // 使用启发式函数计算H值
        startNode.F = startNode.G + startNode.H;

        openList.Add(startNode);
        if (openList.Count > 0)
        {
            Node currentNode = GetLowestFScore(openList); // 获取F值最低的节点

            if (currentNode == targetNode)
            {
                // RetracePath(startNode, targetNode);
                return;
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (var neighbor in GetNeighbors(currentNode))
            {
                if (closedList.Contains(neighbor)) continue;

                int newMovementCostToNeighbor = currentNode.G + GetDistance(currentNode, neighbor);

                //新的邻节点或者要更新G值的邻节点
                if (!openList.Contains(neighbor) || newMovementCostToNeighbor < neighbor.G)
                {
                    neighbor.G = newMovementCostToNeighbor;
                    neighbor.H = Heuristic(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }
    }

    private int Heuristic(Node a, Node b)
    {
        // 可能使用曼哈顿距离或其他启发式函数
        return Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);
    }

    //遍历找出F/G最小的节点
    private Node GetLowestFScore(List<Node> list)
    {
        Node min = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            Node node = list[i];
            if (node.F < min.F)
                min = node;
            if (node.F == min.F && node.G < min.G)
                min = node;
        }

        return min;
    }

    //找出节点的可达的邻节点
    private List<Node> GetNeighbors(Node node)
    {
        List<Node> list = new List<Node>();
        int x = node.GridX;
        int y = node.GridY;
        if (x > 0)
        {
            list.Add(grid[x - 1, y]);
            if (y > 0)
                list.Add(grid[x - 1, y - 1]);
            if (y < grid.GetLength(1) - 1)
                list.Add(grid[x - 1, y + 1]);
        }

        if (x < grid.GetLength(0) - 1)
        {
            list.Add(grid[x + 1, y]);
            if (y > 0)
                list.Add(grid[x + 1, y - 1]);
            if (y < grid.GetLength(1) - 1)
                list.Add(grid[x + 1, y + 1]);
        }

        if (y > 0)
            list.Add(grid[x, y - 1]);
        if (y < grid.GetLength(1) - 1)
            list.Add(grid[x, y + 1]);
        return list;
    }

    private int GetDistance(Node a, Node b)
    {
        // 可能使用曼哈顿距离或其他启发式函数
        return Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);
    }
}

// Node类表示网格中的单个节点
public class Node
{
    public int GridX;
    public int GridY;

    /// <summary>
    /// 实际值
    /// </summary>
    public int G;

    /// <summary>
    /// 启发值、估计值
    /// </summary>
    public int H;

    public int F;

    /// <summary>
    /// 链表
    /// </summary>
    public Node parent;

    // 其他属性和方法...
}


// public class Node
// {
//     Vector2 m_position; //下标
//     public Vector2 position => m_position;
//     public Node parent; //上一个node
//
//     //角色到该节点的实际距离
//     int m_g;
//
//     public int g
//     {
//         get => m_g;
//         set
//         {
//             m_g = value;
//             m_f = m_g + m_h;
//         }
//     }
//
//     //该节点到目的地的估价距离
//     int m_h;
//
//     public int h
//     {
//         get => m_h;
//         set
//         {
//             m_h = value;
//             m_f = m_g + m_h;
//         }
//     }
//
//     int m_f;
//     public int f => m_f;
//
//     public Node(Vector2 pos, Node parent, int g, int h)
//     {
//         m_position = pos;
//         this.parent = parent;
//         m_g = g;
//         m_h = h;
//         m_f = m_g + m_h;
//     }
// }

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