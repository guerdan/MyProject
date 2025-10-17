
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

        MapData _mapData;       // 上层的地图数据
        int _s;                 // _scale, 定义大格子的边长, _scale x _scale的小格子

        BigCell[,] _grid;       // 大格子网格
        int _gridEdge;          // 网格边长

        byte[,] _cMap = new byte[5, 5];             // 用于构造大格子，暂存的二维数组
        Vector2Int[] stack = new Vector2Int[25];    // 用于构造大格子，栈

        public Vector2Int _target = new Vector2Int(-1, -1);     //寻路目标
        List<BigCell> accessList;

        #region 初始化
        public GridData(MapData mapData)
        {
            _mapData = mapData;
            _s = 5;

            _gridEdge = mapData._mapEdge / _s;
            _grid = new BigCell[_gridEdge, _gridEdge];


        }
        #endregion

        #region Apply

        public void Apply(Vector2Int map_start_pos, Vector2Int size)
        {
            var map_end_pos = map_start_pos + size - Vector2Int.one;
            var start_pos = map_start_pos / _s;
            var end_pos = map_end_pos / _s;


            // 把起点算出来，然后遍历更新
            // 大格子中有NewObstacleEdge就更新
            var map = _mapData._map1;
            for (int y = start_pos.y; y <= end_pos.y; y++)
                for (int x = start_pos.x; x <= end_pos.x; x++)
                {
                    // 解决bug，因为只能初始化一次，所以边界有可能碰到Undefined。等信息全了再更新
                    if (x * 5 < map_start_pos.x || y * 5 < map_start_pos.y
                        || x * 5 + 4 > map_end_pos.x || y * 5 + 4 > map_end_pos.y)
                        continue;

                    if (_grid[x, y] == null)
                    {
                        BigCell cell = new BigCell(x, y);
                        _grid[x, y] = cell;
                        RefreshCell(cell, map, x * _s, y * _s);
                    }

                }

        }

        public void RefreshCell(BigCell cell, CellType[,] map, int x_start, int y_start)
        {
            // 连通性，斜边-含障碍的，
            bool need_refresh = false;

            int x_end = x_start + _s;
            int y_end = y_start + _s;
            // for (int m = y_start; m < y_end; m++)
            //     for (int n = x_start; n < x_end; n++)
            //     {
            //         if (map[n, m] == CellType.NewObstacleEdge)
            //         {
            //             need_refresh = true;
            //             map[n, m] = CellType.ObstacleEdge;
            //         }
            //     }


            // for (int m = y_start; m < y_end; m++)
            //     for (int n = x_start; n < x_end; n++)
            //         if (map[n, m] == CellType.Undefined)
            //             return;


            int have_obstacle_count = 0;
            bool have_fog = false;

            for (int y = 0; y < _s; y++)
                for (int x = 0; x < _s; x++)
                    _cMap[x, y] = 0;

            for (int m = y_start; m < y_end; m++)
                for (int n = x_start; n < x_end; n++)
                {
                    var data = map[n, m];
                    if (data == CellType.ObstacleEdge)
                    {
                        have_obstacle_count++;
                        _cMap[n - x_start, m - y_start] = 1;
                    }
                }
            if (have_obstacle_count > 0)
            {


                cell.Type = BigCellType.HasObstacle;
                // 计算连通性
                if (have_obstacle_count == _s * _s)
                {
                    cell.Direction = 0;
                }
                else
                {
                    byte sort_count = 0;
                    // 其实只用外头一圈就行。两横,两竖
                    for (int x = 0; x < _s; x++)
                    {
                        if (_cMap[x, 0] == 0)
                        {
                            var save_num = sort_count + 2;
                            var total = Traversal(_cMap, x, 0, save_num);
                            if (total > 3)
                            {
                                for (int mx = 0; mx < _s; mx++)
                                    if (_cMap[mx, 0] == save_num && map[mx + x_start, y_start - 1] == CellType.Empty)
                                    {
                                        cell.Direction |= 1 << 7; // 下
                                        break;
                                    }


                                for (int mx = 0; mx < _s; mx++)
                                    if (_cMap[mx, 4] == save_num && map[mx + x_start, y_start + 5] == CellType.Empty)
                                    {
                                        cell.Direction |= 1 << 3; // 上
                                        break;
                                    }
                                for (int my = 0; my < _s; my++)
                                    if (_cMap[0, my] == save_num && map[x_start - 1, my + y_start] == CellType.Empty)
                                    {
                                        cell.Direction |= 1 << 1; // 左
                                        break;
                                    }
                                for (int my = 0; my < _s; my++)
                                    if (_cMap[4, my] == save_num && map[x_start + 5, my + y_start] == CellType.Empty)
                                    {
                                        cell.Direction |= 1 << 5; // 右
                                        break;
                                    }

                                if (_cMap[0, 0] == save_num && map[x_start - 1, y_start - 1] == CellType.Empty
                                && (map[x_start - 1, y_start] == CellType.Empty || map[x_start, y_start - 1] == CellType.Empty))
                                {
                                    cell.Direction |= 1 << 0; // 左下
                                }
                                if (_cMap[4, 0] == save_num && map[x_start + 5, y_start - 1] == CellType.Empty
                                && (map[x_start + 5, y_start] == CellType.Empty || map[x_start + 4, y_start - 1] == CellType.Empty))
                                {
                                    cell.Direction |= 1 << 6; // 右下
                                }
                                if (_cMap[0, 4] == save_num && map[x_start - 1, y_start + 5] == CellType.Empty
                                && (map[x_start - 1, y_start + 4] == CellType.Empty || map[x_start, y_start + 5] == CellType.Empty))
                                {
                                    cell.Direction |= 1 << 2; // 左上
                                }
                                if (_cMap[4, 4] == save_num && map[x_start + 5, y_start + 5] == CellType.Empty
                                && (map[x_start + 5, y_start + 4] == CellType.Empty || map[x_start + 4, y_start + 5] == CellType.Empty))
                                {
                                    cell.Direction |= 1 << 4; // 右上
                                }


                                sort_count++;
                            }
                        }
                    }
                    if (sort_count >= 2)
                    {
                        cell.Direction = 0;
                        DU.LogWarning($"存在多侧空地的大格子 sort_count:{sort_count}");
                    }
                }

            }
            else if (have_fog)
            {
                cell.Type = BigCellType.HasFog;
            }
            else
            {
                cell.Type = BigCellType.AllEmpty;
                cell.Direction = 0b1111_1111;
            }

        }
        /// <summary>
        /// 返回个数，连通方向
        /// </summary>
        public int Traversal(byte[,] cMap, int start_x, int start_y, int save_num)
        {
            var total = 0;
            var count = 0;
            stack[count++] = new Vector2Int(start_x, start_y);

            while (count > 0)
            {
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;

                cMap[x, y] = (byte)save_num;
                total++;

                if (y > 0 && cMap[x, y - 1] == 0)
                    stack[count++] = new Vector2Int(x, y - 1);
                if (x > 0 && cMap[x - 1, y] == 0)
                    stack[count++] = new Vector2Int(x - 1, y);
                if (y < 4 && cMap[x, y + 1] == 0)
                    stack[count++] = new Vector2Int(x, y + 1);
                if (x < 4 && cMap[x + 1, y] == 0)
                    stack[count++] = new Vector2Int(x + 1, y);
            }

            return total;
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

        public void StartAStar(Vector2Int start, Vector2Int target)
        {
            ClearAccessStatus();

            var times = 0;
            var openList = new BinarySearchTree<BigCell>();
            accessList = new List<BigCell>(200);
            _target = target;

            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = MapData.Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
            // _grid[start.x, start.y] = startNode;

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

                    int new_G = current.G + MapData.GetDistance(current_pos, neighbor_pos);

                    //新的邻节点或者要更新G值的邻节点
                    bool is_new = neighbor.G == 0;

                    if (is_new)
                    {
                        neighbor.G = new_G;
                        neighbor.F = new_G + MapData.Estimate(neighbor_pos.x, neighbor_pos.y, target.x, target.y);
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
            }
        }

        #endregion

        #region SaveAStar

        public void SaveAStar(string path)
        {
            _mapData.GetContentAttr(out Vector2Int xRange, out Vector2Int yRange
                , out var w, out var h);

            var map = _mapData._map;
            byte[] bytes = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var data = map[x + xRange.x, y + yRange.x];
                    int index = y * w + x;
                    var cell = _grid[(x + xRange.x) / _s, (y + yRange.x) / _s];
                    if (cell == null)
                    {
                        bytes[index] = 255;
                    }
                    else
                    {
                        bytes[index] = data == CellType.ObstacleEdge || data == CellType.NewObstacleEdge
                             ? (byte)255 : bytes[index];
                        bytes[index] = data == CellType.Fog ? (byte)250 : bytes[index];
                        bytes[index] = data == CellType.Empty ? (byte)10 : bytes[index];
                        if (cell.Access && data == CellType.Empty)
                        {
                            bytes[index] = 64;
                        }
                    }


                }



            var target = _grid[_target.x, _target.y];
            while (target.ParentPos != new Vector2Int(-1, -1))
            {
                var pos = target.ParentPos;
                var x = pos.x * _s - xRange.x;
                var y = pos.y * _s - yRange.x;

                for (int my = 0; my < _s; my++)
                    for (int mx = 0; mx < _s; mx++)
                    {
                        int index = (y + my) * w + (x + mx);
                        if (index >= 0 && index < bytes.Length)
                            bytes[index] = 128;
                    }

                target = _grid[pos.x, pos.y];
            }

            // 反转y轴，因为之前翻转过一次    
            byte[] bytes_new = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bytes_new[(h - 1 - y) * w + x] = bytes[y * w + x];
                }
            bytes = bytes_new;

            using (Mat mat = Mat.FromPixelData(h, w, MatType.CV_8UC1, bytes))
            {
                Cv2.ImWrite(path, mat);
            }
        }
        #endregion

    }


    public class BigCell : IComparable<BigCell>
    {
        public bool Access;
        public int x;
        public int y;

        /// <summary>
        /// 0-纯空地 1-空地含障碍 2-障碍 3-含迷雾
        /// </summary>
        public BigCellType Type;
        public byte Direction;      // 8方向的连通情况
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

    public enum BigCellType : byte
    {
        AllObstacle = 0,    // 纯障碍
        AllEmpty = 1,       // 纯空地
        HasObstacle = 2,    // 含障碍
        HasFog = 3,         // 含迷雾
    }
}