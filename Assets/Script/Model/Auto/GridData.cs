
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Model.ListStruct;
using Script.UI.Component;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    // 地图的总数据。 x轴向右，y轴向上。
    // Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了
    // 这种情况下，模版匹配的坐标是图片的左下角。
    //
    // “空地” 指玩家可到达的地块
    //
    // 小格子更新，标注大格子更新。大格子基于全部小格子更新。
    // 需要额外的废弃边界优化吗？不需要，大格子寻路可以搞，但是其总耗时不高
    public class GridData
    {
        public int BuildCellTimes = 0;
        public int MultiEmptyCellCount = 0;
        public int ObstacleFill2EmptyCount = 0;
        MapData _mapData;               // 上层的地图数据
        public int _s;                  // _scale, 定义大格子的边长, _scale x _scale的小格子

        public BigCell[,] _grid;        // 大格子网格
        int _gridEdge;                  // 网格边长


        /// 用于构造大格子，同步相邻大格子的方向                                                         
        Dictionary<BigCell, (int, byte[,])> _afterDic = new Dictionary<BigCell, (int, byte[,])>();
        Vector2Int[] checkList;                             // 用于构造大格子，预存待遍历目标
        byte[,] _cMap = new byte[5, 5];                     // 用于构造大格子，暂存的二维数组
        Vector2Int[] _stack = new Vector2Int[25];           // 用于构造大格子，栈
        Vector2Int[] _mList = new Vector2Int[25];           // 用于构造大格子，复用动态列表
        int _mList_count = 0;                               // 用于构造大格子，复用动态列表


        public Vector2Int _target_c = new Vector2Int(-1, -1);       //寻路目标，大格子粒度
        public Vector2Int _target_p = new Vector2Int(-1, -1);       //寻路目标，像素粒度
        public Vector2Int _start_p = new Vector2Int(-1, -1);        //寻路起点，像素粒度
        List<BigCell> accessList;



        #region 初始化
        public GridData(MapData mapData)
        {
            _mapData = mapData;
            _s = 5;
            checkList = new Vector2Int[(_s - 1) * 4];
            for (int x = 0; x < _s; x++)
            {
                checkList[x] = new Vector2Int(x, 0);
                checkList[x + 5] = new Vector2Int(x, 4);
            }

            for (int y = 1; y < _s - 1; y++)
            {
                checkList[y + 10 - 1] = new Vector2Int(0, y);
                checkList[y + 13 - 1] = new Vector2Int(4, y);
            }

            _gridEdge = mapData._mapEdge / _s;
            _grid = new BigCell[_gridEdge, _gridEdge];
        }

        public int GetActiveCount()
        {
            int result = 0;
            int w = _grid.GetLength(0);
            int h = _grid.GetLength(1);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (_grid[x, y] != null)
                        result++;
                }

            return result;
        }

        #endregion


        #region Apply

        public void Apply(List<BigCell> needList)
        {
            _afterDic.Clear();

            // needList的大格子需要重建
            foreach (var cell in needList)
                CellConstruction(cell);

            // needList的HasObstacle大格子需要刷方向
            // ，并且改"有变化的方向"对应的相邻大格子的方向
            // 信息重点醒目并且提前。
            foreach (var cell in needList)
                if (cell.Type == BigCellType.HasObstacle)
                    CellRefreshDir(cell);


            //重置needRefresh标记
            foreach (var cell in needList)
                cell.NeedRefresh = false;


            BuildCellTimes += needList.Count;
        }

        #region CellConstruction

        // 大格子的构造
        public void CellConstruction(BigCell cell)
        {
            // 连通性，斜边-含障碍的，
            int x_start = cell.x * _s;
            int y_start = cell.y * _s;
            int x_end = x_start + _s;
            int y_end = y_start + _s;

            CellType[,] map = _mapData._map;
            // // debug    
            // var s_map = new CellType[5, 5];
            // for (int m = y_start; m < y_end; m++)
            //     for (int n = x_start; n < x_end; n++)
            //     {
            //         s_map[n - x_start, m - y_start] = map0[n, m];
            //     }


            // // debug
            // if (cell.x == 52 && cell.y == 61)
            // {
            //     var a = 2;
            // }


            int have_obstacle_count = 0;

            for (int y = 0; y < _s; y++)
                for (int x = 0; x < _s; x++)
                    _cMap[x, y] = 0;

            for (int m = y_start; m < y_end; m++)
                for (int n = x_start; n < x_end; n++)
                {
                    var data = map[n, m];
                    // 先重置 ObstacleByBig，后面再刷
                    if (data == CellType.ObstacleByBig)
                    {
                        data = CellType.Empty;
                        map[n, m] = data;
                    }

                    if (data != CellType.Empty)
                    {
                        have_obstacle_count++;
                        _cMap[n - x_start, m - y_start] = 1;
                    }
                }


            if (have_obstacle_count > 0)
            {
                var old_type = cell.Type;

                // 计算连通性
                if (have_obstacle_count < _s * _s)  // 起码有空地
                {
                    int sort_count = 0;             // 空地分类数
                    int sort_3_count = 0;           // 空地分类数  规模>3
                    int save_num_when_1 = 0;
                    // 其实只用外头一圈就行。两横,两竖
                    // 我决定先统计格子，然后遍历
                    var checkListLen = checkList.Length;
                    for (int i = 0; i < checkListLen; i++)
                    {
                        var pos = checkList[i];
                        if (_cMap[pos.x, pos.y] != 0)
                            continue;

                        var save_num = sort_count + 2;
                        sort_count++;
                        Traversal(_cMap, pos.x, pos.y, save_num);
                        if (_mList_count > 3)
                        {
                            save_num_when_1 = save_num;
                            sort_3_count++;
                        }
                        else
                        {
                            for (int j = 0; j < _mList_count; j++)
                            {
                                var p = _mList[j];
                                map[p.x + x_start, p.y + y_start] = CellType.ObstacleByBig;
                            }
                        }
                    }


                    if (sort_3_count == 1)
                    {
                        // 这种情况下，cell.Direction的计算放在 AfterRefreshCell中，为了正确的时机
                        byte[,] copy = new byte[_s, _s];
                        Array.Copy(_cMap, copy, _s * _s);
                        _afterDic[cell] = (save_num_when_1, copy);

                        cell.Type = BigCellType.HasObstacle;

                    }
                    else if (sort_3_count >= 2)
                    {
                        cell.Type = BigCellType.MutiSideEmpty;
                        cell.Direction = 0;
                        if (old_type != BigCellType.MutiSideEmpty) MultiEmptyCellCount++;
                    }
                    else
                    {
                        cell.Type = BigCellType.AllObstacle;
                        cell.Direction = 0;
                    }

                }
                else
                {
                    cell.Type = BigCellType.AllObstacle;
                    cell.Direction = 0;
                }

            }
            else
            {
                cell.Type = BigCellType.AllEmpty;
                cell.Direction = 0b1111_1111;
            }


        }

        #endregion

        #region CellRefreshDir


        //大格子刷新方向，并统计有变化的方向 对应的大格子，对方也要刷
        public void CellRefreshDir(BigCell cell)
        {
            int x_start = cell.x * _s;
            int y_start = cell.y * _s;
            int cx = cell.x;
            int cy = cell.y;

            CellType[,] map = _mapData._map;

            // debug
            // var s_map = new CellType[5, 5];
            // for (int m = y_start; m < y_start + 5; m++)
            //     for (int n = x_start; n < x_start + 5; n++)
            //     {
            //         s_map[n - x_start, m - y_start] = map1[n, m];
            //     }

            // debug
            // if (cell.x == 52 && cell.y == 61)
            // {
            //     var a = 2;
            // }

            if (!_afterDic.TryGetValue(cell, out var r))
                return;

            int left = 0;
            int right = 0;
            int top = 0;
            int bottom = 0;
            int left_top = 0;
            int left_bottom = 0;
            int right_bottom = 0;
            int right_top = 0;

            cell.Direction = 0;

            var save_num = r.Item1;
            var cMap = r.Item2;

            for (int mx = 0; mx < _s; mx++)
                if (cMap[mx, 0] == save_num && map[mx + x_start, y_start - 1] == CellType.Empty)
                {
                    cell.Direction |= 1 << 7; // 下
                    bottom = 1;   // 下
                    break;
                }


            for (int mx = 0; mx < _s; mx++)
                if (cMap[mx, 4] == save_num && map[mx + x_start, y_start + 5] == CellType.Empty)
                {
                    cell.Direction |= 1 << 3;
                    top = 1;   // 上
                    break;
                }
            for (int my = 0; my < _s; my++)
                if (cMap[0, my] == save_num && map[x_start - 1, my + y_start] == CellType.Empty)
                {
                    cell.Direction |= 1 << 1;
                    left = 1;   // 左
                    break;
                }
            for (int my = 0; my < _s; my++)
                if (cMap[4, my] == save_num && map[x_start + 5, my + y_start] == CellType.Empty)
                {
                    cell.Direction |= 1 << 5;
                    right = 1;   // 右
                    break;
                }

            if (cMap[0, 0] == save_num && map[x_start - 1, y_start - 1] == CellType.Empty
                && (map[x_start - 1, y_start] == CellType.Empty || map[x_start, y_start - 1] == CellType.Empty))
            {
                cell.Direction |= 1 << 0;
                left_bottom = 1;   // 左下
            }
            if (cMap[4, 4] == save_num && map[x_start + 5, y_start + 5] == CellType.Empty
                && (map[x_start + 5, y_start + 4] == CellType.Empty || map[x_start + 4, y_start + 5] == CellType.Empty))
            {
                cell.Direction |= 1 << 4;
                right_top = 1;   // 右上
            }

            if (cMap[0, 4] == save_num && map[x_start - 1, y_start + 5] == CellType.Empty
                && (map[x_start - 1, y_start + 4] == CellType.Empty || map[x_start, y_start + 5] == CellType.Empty))
            {
                cell.Direction |= 1 << 2;
                left_top = 1;   // 左上
            }

            if (cMap[4, 0] == save_num && map[x_start + 5, y_start - 1] == CellType.Empty
                && (map[x_start + 5, y_start] == CellType.Empty || map[x_start + 4, y_start - 1] == CellType.Empty))
            {
                cell.Direction |= 1 << 6;
                right_bottom = 1;   // 右下
            }


            BigCell adjacent = null;
            adjacent = _grid[cx, cy - 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 3)) | (bottom << 3));

            adjacent = _grid[cx, cy + 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 7)) | (top << 7));

            adjacent = _grid[cx - 1, cy];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 5)) | (left << 5));

            adjacent = _grid[cx + 1, cy];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 1)) | (right << 1));

            adjacent = _grid[cx - 1, cy - 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 4)) | (left_bottom << 4));

            adjacent = _grid[cx + 1, cy + 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 0)) | (right_top << 0));

            adjacent = _grid[cx - 1, cy + 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 6)) | (left_top << 6));

            adjacent = _grid[cx + 1, cy - 1];
            if (adjacent != null) adjacent.Direction = (byte)((adjacent.Direction & ~(1 << 2)) | (right_bottom << 2));


        }

        #endregion
        /// <summary>
        /// 返回个数，连通方向
        /// </summary>
        public void Traversal(byte[,] cMap, int start_x, int start_y, int save_num)
        {
            _mList_count = 0;

            var count = 0;
            _stack[count++] = new Vector2Int(start_x, start_y);
            cMap[start_x, start_y] = (byte)save_num;

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;

                _mList[_mList_count++] = new Vector2Int(x, y);

                if (y > 0 && cMap[x, y - 1] == 0)
                {
                    cMap[x, y - 1] = (byte)save_num;
                    _stack[count++] = new Vector2Int(x, y - 1);
                }
                if (x > 0 && cMap[x - 1, y] == 0)
                {
                    cMap[x - 1, y] = (byte)save_num;
                    _stack[count++] = new Vector2Int(x - 1, y);
                }
                if (y < 4 && cMap[x, y + 1] == 0)
                {
                    cMap[x, y + 1] = (byte)save_num;
                    _stack[count++] = new Vector2Int(x, y + 1);
                }
                if (x < 4 && cMap[x + 1, y] == 0)
                {
                    cMap[x + 1, y] = (byte)save_num;
                    _stack[count++] = new Vector2Int(x + 1, y);
                }
            }

        }

        #endregion

        #region Rebuild
        public void Rebuild(int old_mapEdge, int mapEdge, Vector2Int map_offset)
        {
            var old_gridEdge = old_mapEdge / _s;
            var gridEdge = mapEdge / _s;
            var offset = map_offset / _s;

            _gridEdge = gridEdge;
            var gridB = new BigCell[_gridEdge, _gridEdge];

            for (int i = 0; i < old_gridEdge; i++)
                for (int j = 0; j < old_gridEdge; j++)
                {
                    var data = _grid[j, i];
                    if (data == null)
                        continue;

                    data.x = j + offset.x;
                    data.y = i + offset.y;
                    gridB[j + offset.x, i + offset.y] = data;
                }

            _grid = gridB;
        }
        #endregion

        public int GetDivisibleInt(int number)
        {
            var a = number % _s;
            return number - a;
        }

        #region AStar

        public void StartAStar(Vector2Int start_p, Vector2Int target_p)
        {

            ClearAccessStatus();
            _start_p = start_p;
            _target_p = target_p;

            var start = start_p / 5;
            var target = target_p / 5;
            _target_c = target;

            var times = 0;
            var openList = new BinarySearchTree<BigCell>();
            accessList = new List<BigCell>(200);
            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值

            openList.Insert(startNode);

            var method_list = new Vector2Int[8];
            var method_count = 0;
            int x_end = _grid.GetLength(0) - 1;
            int y_end = _grid.GetLength(1) - 1;


            // openList 要排序，也要查找
            while (!openList.Empty())
            {

                times++;
                // 获取F值最低的节点
                var current = openList.FindMin().Value;
                openList.Delete(current);

                current.Access = true;
                accessList.Add(current);

                if (current.x == target.x && current.y == target.y)
                {
                    break;
                }


                method_count = 0;
                int x = current.x;
                int y = current.y;
                var current_pos = new Vector2Int(x, y);
                var direction = current.Direction;

                if ((direction & (1 << 7)) != 0 && _grid[x, y - 1] != null && !_grid[x, y - 1].Access)
                    method_list[method_count++] = new Vector2Int(x, y - 1);

                if ((direction & (1 << 1)) != 0 && _grid[x - 1, y] != null && !_grid[x - 1, y].Access)
                    method_list[method_count++] = new Vector2Int(x - 1, y);

                if ((direction & (1 << 3)) != 0 && _grid[x, y + 1] != null && !_grid[x, y + 1].Access)
                    method_list[method_count++] = new Vector2Int(x, y + 1);

                if ((direction & (1 << 5)) != 0 && _grid[x + 1, y] != null && !_grid[x + 1, y].Access)
                    method_list[method_count++] = new Vector2Int(x + 1, y);

                if ((direction & (1 << 0)) != 0 && _grid[x - 1, y - 1] != null && !_grid[x - 1, y - 1].Access)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);

                if ((direction & (1 << 2)) != 0 && _grid[x - 1, y + 1] != null && !_grid[x - 1, y + 1].Access)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);

                if ((direction & (1 << 4)) != 0 && _grid[x + 1, y + 1] != null && !_grid[x + 1, y + 1].Access)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);

                if ((direction & (1 << 6)) != 0 && _grid[x + 1, y - 1] != null && !_grid[x + 1, y - 1].Access)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

                    //debug
                    //
                    // if (neighbor_pos.x == 75 && neighbor_pos.y == 72)
                    // {
                    //     var a = 1;
                    // }

                    int new_G = current.G + GetDistance(current_pos, neighbor_pos);

                    //新的邻节点或者要更新G值的邻节点
                    bool is_new = neighbor.G == 0;

                    if (is_new)
                    {
                        neighbor.G = new_G;
                        neighbor.F = new_G + Estimate(neighbor_pos.x, neighbor_pos.y, target.x, target.y);
                        neighbor.ParentPos = current_pos;
                        openList.Insert(neighbor);
                    }
                    else if (new_G < neighbor.G)
                    {
                        var success = openList.Delete(neighbor);
                        if (!success)
                            DU.LogError("Delete fail");

                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                        // _grid[neighbor_pos.x, neighbor_pos.y] = neighbor;
                        openList.Insert(neighbor);
                    }

                }
            }



            DU.LogWarning("AStar times:" + times);
        }

        public void ClearAccessStatus()
        {
            if (accessList == null)
                return;
            foreach (var pos in accessList)
            {
                pos.Access = false;
                pos.G = 0;
                pos.F = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Estimate(int x0, int y0, int x1, int y1)
        {
            int delta_x = x0 - x1;
            delta_x = delta_x < 0 ? -delta_x : delta_x;
            int delta_y = y0 - y1;
            delta_y = delta_y < 0 ? -delta_y : delta_y;
            return (delta_x + delta_y) * 1000;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetDistance(Vector2Int a, Vector2Int b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var c = dx * dx + dy * dy;
            if (c == 1) return 1000;
            if (c == 2) return 1414;
            return 0;
        }

        #endregion
        #region Api

        public static Vector2Int[] list = new Vector2Int[5];

        /// <summary>
        /// 喂A大格子，B大格子。返回连通两者的像素，此像素在B中
        /// 参0:true为空地，false为障碍 
        /// </summary>
        public static Vector2Int GetConnectPixel(bool[,] mapA, bool[,] mapB, Vector2Int pA, Vector2Int pB)
        {
            int x_delta = pB.x - pA.x;
            int y_delta = pB.y - pA.y;
            Vector2Int result = Vector2Int.zero;

            int list_count = 0;
            if (x_delta != 0 && y_delta != 0)
            {
                // B的四个顶点其中一个 为交界
                result.x = x_delta == 1 ? 0 : 4;
                result.y = y_delta == 1 ? 0 : 4;
            }
            else
            {
                // x_delta、y_delta有一个为0
                if (x_delta == -1)          //B的右边线 为交界
                {
                    for (int y = 0; y < 5; y++)
                        if (mapB[4, y] && mapA[0, y])
                            list[list_count++] = new Vector2Int(4, y);
                }
                else if (x_delta == 1)      //B的左边线 为交界
                {
                    for (int y = 0; y < 5; y++)
                        if (mapB[0, y] && mapA[4, y])
                            list[list_count++] = new Vector2Int(0, y);
                }
                else if (y_delta == -1)     //B的上边线 为交界
                {
                    for (int x = 0; x < 5; x++)
                        if (mapB[x, 4] && mapA[x, 0])
                            list[list_count++] = new Vector2Int(x, 4);
                }
                else if (y_delta == 1)      //B的下边线 为交界
                {
                    for (int x = 0; x < 5; x++)
                        if (mapB[x, 0] && mapA[x, 4])
                            list[list_count++] = new Vector2Int(x, 0);
                }

                if (list_count > 0)
                {
                    // 取中间的一个，偶数则偏右
                    int index = list_count / 2;
                    result = list[index];
                }
            }


            return result;
        }



        #endregion

    }

    #region BigCell

    public class BigCell : IComparable<BigCell>
    {
        public bool NeedRefresh;
        public bool Access;
        public int x;
        public int y;

        /// <summary>
        /// 0-纯空地 1-空地含障碍 2-障碍 3-含迷雾  4-多侧空地
        /// </summary>
        public BigCellType Type;    // 逻辑不严密，尽量采用Direction来判断
        public byte Direction;      // 8方向的连通情况，是决定性因素
        public int G;               // 实际值
        public int F;               // 总值

        public int H => F - G;      // 启发值、估计值
        public Vector2Int ParentPos;        // 指向上一格，实现链表

        public BigCell(int x, int y)
        {
            this.x = x;
            this.y = y;
            Type = BigCellType.AllObstacle;
            ParentPos = new Vector2Int(-1, -1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(BigCell other)
        {
            if (F != other.F)
                return F - other.F > 0 ? 1 : -1;
            else if (G != other.G)
                return G - other.G < 0 ? 1 : -1;
            else if (x != other.x)
                return x - other.x > 0 ? 1 : -1;
            else
                return y.CompareTo(other.y);

        }

        public override string ToString()
        {
            return $"{F}";
        }

    }
    #endregion

    public enum BigCellType : byte
    {
        AllObstacle = 0,    // 全障碍
        AllEmpty = 1,       // 全空地
        HasObstacle = 2,    // 含障碍
        MutiSideEmpty = 3,  // 多侧空地，在系统中等同于全障碍
    }



    #region 小寻路

    public enum SmallCellType : byte
    {
        Obstacle = 0,       // 障碍
        Empty = 1,          // 空地
        Access = 2,         // 已遍历过的
    }

    // 研究明白了，格子类是不能改成Struct的，因为要使用列表排序。当改G、F值时，在列表内的和网格上的都要改。
    public class SmallCell
    {
        public int x;
        public int y;
        public SmallCellType Type;
        public int G;               // 实际值
        public int F;               // 总值

        public int H => F - G;      // 启发值、估计值
        public Vector2Int ParentPos;        // 指向上一格，实现链表

        public SmallCell(int x, int y, bool is_empty)
        {
            this.x = x;
            this.y = y;
            Type = is_empty ? SmallCellType.Empty : SmallCellType.Obstacle;
            G = 0;
            F = 0;
            ParentPos = new Vector2Int(-1, -1);
        }

        /// <summary>
        /// 是否比目标大
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareTo(SmallCell other)
        {
            if (F != other.F)
                return F - other.F > 0;
            else if (G != other.G)
                return G - other.G < 0;

            return false;
        }

    }

    public class SmallCellFinder
    {
        public SmallCell[,] _grid;       // 网格
        public int _w;
        public int _h;

        public List<Vector2Int> BeginAStar(bool[,] map, Vector2Int start, Vector2Int target)
        {
            int w = map.GetLength(0);
            int h = map.GetLength(1);
            // 周围一圈，铺上障碍， 因此，全部坐标被垫高了(1,1)
            //
            if (_grid == null || _w != w || _h != h)
            {
                _grid = new SmallCell[w + 2, h + 2];
                _w = w;
                _h = h;
            }

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    _grid[x + 1, y + 1] = new SmallCell(x + 1, y + 1, map[x, y]);
                }
            for (int y = 0; y < h + 2; y++)
            {
                _grid[0, y] = new SmallCell(0, y, false);
                _grid[w + 1, y] = new SmallCell(w + 1, y, false);
            }
            for (int x = 0; x < w + 2; x++)
            {
                _grid[x, 0] = new SmallCell(x, 0, false);
                _grid[x, h + 1] = new SmallCell(x, h + 1, false);
            }

            start = start + Vector2Int.one;
            target = target + Vector2Int.one;

            var openList = new List<SmallCell>();

            var startCell = _grid[start.x, start.y];
            startCell.G = 0;
            startCell.F = GridData.Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
            openList.Add(startCell);

            var method_list = new Vector2Int[8];
            var method_count = 0;

            while (openList.Count != 0)
            {
                var current = openList[0];
                int min_index = 0;
                // 先找最小F值的格子
                //
                var len = openList.Count;
                for (int i = 0; i < len; i++)
                {
                    var c = openList[i];
                    if (current.CompareTo(c))
                    {
                        current = c;
                        min_index = i;
                    }
                }

                if (current.x == target.x && current.y == target.y)
                {
                    break;
                }

                openList.RemoveAt(min_index);
                current.Type = SmallCellType.Access;


                method_count = 0;
                int x = current.x;
                int y = current.y;
                var current_pos = new Vector2Int(x, y);

                if (_grid[x, y - 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x, y - 1);
                if (_grid[x - 1, y].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x - 1, y);
                if (_grid[x, y + 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x, y + 1);
                if (_grid[x + 1, y].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x + 1, y);
                if (_grid[x - 1, y - 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);
                if (_grid[x - 1, y + 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);
                if (_grid[x + 1, y + 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);
                if (_grid[x + 1, y - 1].Type == SmallCellType.Empty)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

                    int new_G = current.G + GridData.GetDistance(current_pos, neighbor_pos);

                    //新的邻节点或者要更新G值的邻节点
                    bool is_new = neighbor.G == 0;

                    if (is_new)
                    {
                        neighbor.G = new_G;
                        neighbor.F = new_G + GridData.Estimate(neighbor_pos.x, neighbor_pos.y, target.x, target.y);
                        neighbor.ParentPos = current_pos;
                        openList.Add(neighbor);
                    }
                    else if (new_G < neighbor.G)
                    {
                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                    }

                }

            }

            List<Vector2Int> result = new List<Vector2Int>();

            var cell = _grid[target.x, target.y];
            result.Add(new Vector2Int(cell.x - 1, cell.y - 1));
            while (cell.ParentPos != new Vector2Int(-1, -1))
            {
                cell = _grid[cell.ParentPos.x, cell.ParentPos.y];
                result.Add(new Vector2Int(cell.x - 1, cell.y - 1));
            }


            return result;
        }

    }



    #endregion

}