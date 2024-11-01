using UnityEngine;
using System.Collections.Generic;
using UnityEngine;

public class AStarLogicManager
{
    private static AStarLogicManager _inst;

    public static AStarLogicManager inst
    {
        get
        {
            _inst = _inst == null ? new AStarLogicManager() : _inst;
            return _inst;
        }
    }


    public AStarLogicNode[,] grid = null; // Node[y,x]
    private AStarLogicNode startNode;
    private AStarLogicNode targetNode;

    public List<AStarLogicNode> openList = new List<AStarLogicNode>();
    public HashSet<AStarLogicNode> closedList = new HashSet<AStarLogicNode>();

    public void init(int row, int column)
    {
        grid = new AStarLogicNode[row, column];
        for (int i = 0; i < row; i++)
        {
            for (int j = 0; j < column; j++)
            {
                grid[i, j] = new AStarLogicNode() { GridX = j, GridY = i };
            }
        }


        openList.Clear();
        closedList.Clear();
        startNode = null;
        targetNode = null;
    }

    public bool inArea(Vector2Int pos)
    {
        if (grid == null) return false;
        if (pos.y < 0 || pos.y >= grid.GetLength(0)) return false;
        if (pos.x < 0 || pos.x >= grid.GetLength(1)) return false;
        return true;
    }

    /// <summary>
    /// 设置障碍
    /// </summary>
    /// <param name="pos"></param>
    public void SetBlock(Vector2Int pos)
    {
        if (!inArea(pos)) return;
        AStarLogicNode node = grid[pos.y, pos.x];
        if (node.Type == AStarLogicNodeType.Block)
            node.Type = AStarLogicNodeType.Normal;
        else if (node.Type == AStarLogicNodeType.Normal)
            node.Type = AStarLogicNodeType.Block;
    }

    /// <summary>
    /// 设置起点
    /// </summary>
    /// <param name="pos"></param>
    public void SetStart(Vector2Int pos)
    {
        if (!inArea(pos)) return;
        foreach (var node in grid)
        {
            if (node.Type == AStarLogicNodeType.Start)
                node.Type = AStarLogicNodeType.Normal;
        }

        startNode = grid[pos.y, pos.x];
        startNode.Type = AStarLogicNodeType.Start;
    }

    /// <summary>
    /// 设置终点
    /// </summary>
    /// <param name="pos"></param>
    public void SetTarget(Vector2Int pos)
    {
        if (!inArea(pos)) return;
        foreach (var node in grid)
        {
            if (node.Type == AStarLogicNodeType.Target)
                node.Type = AStarLogicNodeType.Normal;
        }

        targetNode = grid[pos.y, pos.x];
        targetNode.Type = AStarLogicNodeType.Target;
    }

    /// <summary>
    /// A*寻路逻辑，每执行一步
    /// </summary>
    public void FindPathStep()
    {
        if (grid == null) return;
        if (targetNode == null) return;
        if (startNode == null) return;

        //若未初始化则初始
        if (startNode.H == 0)
        {
            startNode.G = 0;
            startNode.H = Heuristic(startNode, targetNode); // 使用启发式函数计算H值

            openList.Add(startNode);
        }

        if (openList.Count > 0)
        {
            AStarLogicNode currentNode = GetLowestFScore(openList); // 获取F值最低的节点

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

    private int Heuristic(AStarLogicNode a, AStarLogicNode b)
    {
        // 可能使用曼哈顿距离或其他启发式函数
        return Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);
    }

    //遍历找出F最小H最小（H越小估计失误越少）的节点
    private AStarLogicNode GetLowestFScore(List<AStarLogicNode> list)
    {
        AStarLogicNode min = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            AStarLogicNode node = list[i];
            if (node.F < min.F)
                min = node;
            if (node.F == min.F && node.H < min.H)
                min = node;
        }

        return min;
    }

    //找出节点的可达的邻节点
    private List<AStarLogicNode> GetNeighbors(AStarLogicNode node)
    {
        List<AStarLogicNode> list = new List<AStarLogicNode>();
        int x = node.GridX;
        int y = node.GridY;
        if (x > 0)
        {
            list.Add(grid[y, x - 1]);
            if (y > 0)
                list.Add(grid[y - 1, x - 1]);
            if (y < grid.GetLength(0) - 1)
                list.Add(grid[y + 1, x - 1]);
        }

        if (x < grid.GetLength(1) - 1)
        {
            list.Add(grid[y, x + 1]);
            if (y > 0)
                list.Add(grid[y - 1, x + 1]);
            if (y < grid.GetLength(0) - 1)
                list.Add(grid[y + 1, x + 1]);
        }

        if (y > 0)
            list.Add(grid[y - 1, x]);
        if (y < grid.GetLength(0) - 1)
            list.Add(grid[y + 1, x]);

        list = list.FindAll(node => node.Type != AStarLogicNodeType.Block);
        return list;
    }

    private int GetDistance(AStarLogicNode a, AStarLogicNode b)
    {
        // 可能使用曼哈顿距离或其他启发式函数
        return Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);
    }
}

public enum AStarLogicNodeType
{
    Normal = 0,
    Start = 1,
    Target = 2,
    Block = 3,
}

// Node类表示网格中的单个节点
public class AStarLogicNode
{
    public int GridX;
    public int GridY;
    public AStarLogicNodeType Type = AStarLogicNodeType.Normal;

    /// <summary>
    /// 实际值
    /// </summary>
    public int G;

    /// <summary>
    /// 启发值、估计值
    /// </summary>
    public int H;

    public int F => H + G;

    /// <summary>
    /// 链表
    /// </summary>
    public AStarLogicNode parent;

    // 其他属性和方法...
}