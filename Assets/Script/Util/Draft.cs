
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Model.Auto;
using Script.Model.ListStruct;
using UnityEngine;
using Mathf = UnityEngine.Mathf;
using Rect = OpenCvSharp.Rect;

namespace Script.Util
{
    public class Draft
    {
        #region  IU

        // 自己写。结果与opencv接口是一致的。
        // 耗时8600ms 优化跟不上。 我估计官方是用窗口思想优化的 只动变的
        // 优化了一版 4300ms。应该就是思路上的优化了。窗口移动时只用关注删行和新行，优化计算总和。
        // c#侧存图片矩阵  3ms
        // 然后就是数学计算。T的模
        // 遍历每个I， 计算点积，计算I区域的模
        public static float[,] MatchTemplate2(string inputPath, string templatePath)
        {
            // 读取图片
            readImage(inputPath, out Vec3b[,] dataI, out int wI, out int hI, out var _);
            readImage(templatePath, out Vec3b[,] dataT, out int wT, out int hT, out byte[,] mask);
            var DontMask = mask == null; // 不使用掩码

            int wR = wI - wT + 1;
            int hR = hI - hT + 1;
            float[,] result = new float[hR, wR];

            if (DontMask)
            {
                //可以先求得零均值
                Vec3f[,] mapT = HandlerMap(dataT, new Rect(0, 0, wT, hT));
                float[] modT = Modulo(mapT);
                for (int y = 0; y < hR; y++)
                {
                    for (int x = 0; x < wR; x++)
                    {
                        Vec3f[,] mapI = HandlerMap(dataI, new Rect(x, y, wT, hT));
                        float[] modI = Modulo(mapI);
                        float[] dot = Dot(mapI, mapT);

                        float sum = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            sum += dot[i] / Mathf.Sqrt(modI[i] * modT[i]);
                        }
                        result[y, x] = sum / 3; // 平均
                    }
                }
            }
            else
            {

                Vec3f[,] mapT = HandlerMap(dataT, new Rect(0, 0, wT, hT), mask);
                float[] modT = Modulo(mapT, mask);
                DU.StartTimer();

                for (int y = 0; y < hR; y++)
                {
                    for (int x = 0; x < wR; x++)
                    {
                        Vec3f[,] mapI = HandlerMap(dataI, new Rect(x, y, wT, hT), mask);
                        float[] modI = Modulo(mapI, mask);
                        float[] dot = Dot(mapI, mapT, mask);

                        float sum = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            sum += dot[i] / Mathf.Sqrt(modI[i] * modT[i]);
                        }
                        result[y, x] = sum / 3; // 平均
                    }
                }
                DU.Log(DU.StopTimer($"read1"));

            }


            return result;
        }

        private static void readImage(string fileName, out Vec3b[,] pixels
        , out int w, out int h, out byte[,] mask)
        {
            var mat = Cv2.ImRead(fileName, ImreadModes.Unchanged);
            w = mat.Width;
            h = mat.Height;
            var UseMask = mat.Channels() == 4; // 是否使用掩码
            if (UseMask)
            {
                mat.GetArray<Vec4b>(out var data);
                pixels = new Vec3b[h, w];
                mask = new byte[h, w];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var index = y * w + x;
                        pixels[y, x] = new Vec3b(data[index].Item0, data[index].Item1, data[index].Item2);
                        mask[y, x] = data[index].Item3; // alpha通道作为mask
                    }
                }

            }
            else
            {
                mask = null;
                mat.GetArray<Vec3b>(out var data);
                pixels = new Vec3b[h, w];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var index = y * w + x;
                        pixels[y, x] = new Vec3b(data[index].Item0, data[index].Item1, data[index].Item2);
                    }
                }
            }
        }

        // public static  Color32[] readImage(string fileName)
        // {
        //     byte[] bytes = File.ReadAllBytes(fileName);
        //     Texture2D tex = new Texture2D(2, 2);
        //     tex.LoadImage(bytes);
        //     return tex.GetPixels32();
        // }


        /// <summary>
        /// mask是描述rect的
        /// </summary>.
        private static Vec3f[,] HandlerMap(Vec3b[,] data, Rect rect, byte[,] mask = null)
        {
            var h = rect.Height;
            var w = rect.Width;
            var rY = rect.Y;
            var rX = rect.X;


            Vec3f[,] map = new Vec3f[h, w];
            int[] total = new int[3];
            int total_count = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask != null) // 如果掩码为0，跳过
                    {
                        if (mask[y, x] == 0)
                            continue;
                    }
                    total_count++;
                    var d = data[y + rY, x + rX];
                    total[0] += d.Item0;
                    total[1] += d.Item1;
                    total[2] += d.Item2;
                }
            }
            total[0] = total[0] / total_count;
            total[1] = total[1] / total_count;
            total[2] = total[2] / total_count;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask != null) // 如果掩码为0，跳过
                    {
                        if (mask[y, x] == 0)
                            continue;
                    }
                    var d = data[y + rY, x + rX];
                    var t = new Vec3f(
                        d.Item0 - total[0],
                        d.Item1 - total[1],
                        d.Item2 - total[2]
                    );
                    map[y, x] = t;
                }
            }

            return map;
        }

        private static float[] Dot(Vec3f[,] map0, Vec3f[,] map1, byte[,] mask = null)
        {
            var h = map0.GetLength(0);
            var w = map0.GetLength(1);
            float[] sum = new float[3];

            if (mask == null)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        sum[0] += map0[y, x].Item0 * map1[y, x].Item0;
                        sum[1] += map0[y, x].Item1 * map1[y, x].Item1;
                        sum[2] += map0[y, x].Item2 * map1[y, x].Item2;
                    }
                }
            }
            else
            {

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y, x] == 0) // 如果掩码为0，跳过
                            continue;
                        sum[0] += map0[y, x].Item0 * map1[y, x].Item0;
                        sum[1] += map0[y, x].Item1 * map1[y, x].Item1;
                        sum[2] += map0[y, x].Item2 * map1[y, x].Item2;
                    }
                }
            }

            return sum;
        }

        private static float[] Modulo(Vec3f[,] map0, byte[,] mask = null)
        {
            var h = map0.GetLength(0);
            var w = map0.GetLength(1);
            float[] sum = new float[3];

            if (mask == null)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        sum[0] += map0[y, x].Item0 * map0[y, x].Item0;
                        sum[1] += map0[y, x].Item1 * map0[y, x].Item1;
                        sum[2] += map0[y, x].Item2 * map0[y, x].Item2;
                    }
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y, x] == 0) // 如果掩码为0，跳过
                            continue;
                        sum[0] += map0[y, x].Item0 * map0[y, x].Item0;
                        sum[1] += map0[y, x].Item1 * map0[y, x].Item1;
                        sum[2] += map0[y, x].Item2 * map0[y, x].Item2;
                    }
                }
            }

            return sum;
        }





        public static List<CVMatchResult> Draw1(Mat result, float[,] rf, int wT, int hT, float threshold = 0.9f)
        {
            List<CVMatchResult> matchResults = new List<CVMatchResult>();
            int hR, wR;
            if (result != null)
            {
                hR = result.Rows; //继续优化了10ms 
                wR = result.Cols;
            }
            else
            {
                hR = rf.GetLength(0);
                wR = rf.GetLength(1);
            }

            byte[,] cull = new byte[hR, wR];    //优化了10ms. 从Mat(拆装箱)转[,]


            // 筛选所有结果，找出所有大于阈值的位置
            for (int y = 0; y < hR; y++)
            {
                for (int x = 0; x < wR; x++)
                {
                    if (cull[y, x] > 0)
                        continue; // 如果这个位置已经被标记过，跳过

                    float score;
                    if (result != null) score = result.At<float>(y, x);
                    else score = rf[y, x];

                    var tx = x;
                    var ty = y;
                    if (score >= threshold)
                    {
                        // 设置非筛选区域，并找出最大的
                        for (int i = 0; i < hT; i++)
                        {
                            for (int j = 0; j < wT; j++)
                            {
                                if (y + i < hR && x + j < wR)
                                {
                                    cull[y + i, x + j] = 1;

                                    float tscore;
                                    if (result != null) tscore = result.At<float>(y + i, x + j);
                                    else tscore = rf[y + i, x + j];

                                    if (tscore > score)
                                    {
                                        tx = x + j;
                                        ty = y + i;
                                        score = tscore;
                                    }
                                }
                            }
                        }

                        // 得到匹配结果
                        var rect = new CVRect(tx, ty, wT, hT);
                        var r = new CVMatchResult
                        {
                            Rect = rect,
                            Score = score
                        };
                        matchResults.Add(r);
                    }
                }
            }
            return matchResults;
        }

        #endregion



        #region MapData



        // A星寻路，几种数据结构

        public PixType[,] _map;            // 只有Capture可写，其他方法可读


        Vector2Int _xRange;          // 内容x轴范围
        Vector2Int _yRange;          // 内容y轴范围
        int _w;                             // 内容宽  
        int _h;                             // 内容高

        /// <summary>
        /// Type: 0-为暗地 2-边界 4-迷雾边界 5-已遍历过 
        /// </summary>
        AStarCell[,] _grid;
        int times;
        int change_times;

        BinarySearchTree<AStarCell> openList;
        Vector2Int astar_target;


        public void InitGridOriginal()
        {

            _grid = new AStarCell[_w + 2, _h + 2];

            // 小格子寻路 特殊处理
            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    var x = j + _xRange.x;
                    var y = i + _yRange.x;
                    var data = _map[x, y];
                    // if (data == CellType.NewObstacleEdge)
                    {
                        data = PixType.ObstacleEdge;
                        _map[x, y] = data;
                    }
                }


            // 填充 斜对角 成 斜着不能过的边界
            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    var x = j + _xRange.x;
                    var y = i + _yRange.x;
                    var data = _map[x, y];

                    // 左上，右上斜对角，检测
                    if (data == PixType.ObstacleEdge)
                    {
                        if (j > 0 && i < _h - 1 && _map[x - 1, y + 1] == PixType.ObstacleEdge
                        && _map[x - 1, y] == PixType.Empty && _map[x, y + 1] == PixType.Empty)
                        {
                            _map[x - 1, y] = PixType.ObstacleEdgeTemp;
                            _map[x, y + 1] = PixType.ObstacleEdgeTemp;
                        }

                        if (j < _w - 1 && i < _h - 1 && _map[x + 1, y + 1] == PixType.ObstacleEdge
                        && _map[x + 1, y] == PixType.Empty && _map[x, y + 1] == PixType.Empty)
                        {

                            _map[x + 1, y] = PixType.ObstacleEdgeTemp;
                            _map[x, y + 1] = PixType.ObstacleEdgeTemp;
                        }
                    }

                }


            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    var data = _map[j + _xRange.x, i + _yRange.x];
                    var cell = new AStarCell()
                    {
                        x = j + 1,
                        y = i + 1,
                        G = 0,
                        F = 0,
                        Type = (byte)data == 1 ? (byte)0 : (byte)data, //先不管"迷雾边界"
                        ParentPos = new Vector2Int(-1, -1)
                    };
                    if (data == PixType.ObstacleEdge || data == PixType.ObstacleEdgeTemp)
                    {
                        cell.Type = 2;
                    }
                    // 还原
                    if (data == PixType.ObstacleEdgeTemp)
                    {
                        _map[j + _xRange.x, i + _yRange.x] = PixType.Empty;
                    }

                    _grid[j + 1, i + 1] = cell;
                }

            for (int i = 0; i < _h + 2; i++)
            {
                _grid[0, i] = new AStarCell() { x = 0, y = i, Type = 10 };
                _grid[_w + 1, i] = new AStarCell() { x = _w + 1, y = i, Type = 10 };
            }
            for (int i = 0; i < _w + 2; i++)
            {
                _grid[i, 0] = new AStarCell() { x = i, y = 0, Type = 10 };
                _grid[i, _h + 1] = new AStarCell() { x = i, y = _h + 1, Type = 10 };
            }

        }

        AStarCell GridGet(int x, int y)
        {
            return _grid[x + 1, y + 1];
        }

        void GridSet(int x, int y, AStarCell cell)
        {
            _grid[x + 1, y + 1] = cell;
        }

        public void StartAStar(Vector2Int start, Vector2Int target)
        {
            if (_grid == null) return;
            astar_target = target;
            times = 0;
            openList = new BinarySearchTree<AStarCell>(null);

            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
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

                if (current.x == target.x && current.y == target.y)
                {
                    break;
                }
                // var current = _grid[current_pos.x, current_pos.y];
                openList.Delete(current);
                current.Type = 5; // 标记为已遍历过
                // _grid[current_pos.x, current_pos.y] = current;

                method_count = 0;
                int x = current.x;
                int y = current.y;
                var current_pos = new Vector2Int(x, y);

                if (y > 0 && _grid[x, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y - 1);
                if (x > 0 && _grid[x - 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y);
                if (y < y_end && _grid[x, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y + 1);
                if (x < x_end && _grid[x + 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && _grid[x - 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && _grid[x - 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && _grid[x + 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && _grid[x + 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

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

        }


        public void StartAStarAVL(Vector2Int start, Vector2Int target)
        {
            if (_grid == null) return;
            astar_target = target;
            times = 0;
            change_times = 0;
            var openList = new AVLTree<AStarCell>();

            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
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

                if (current.x == target.x && current.y == target.y)
                {
                    break;
                }
                // var current = _grid[current_pos.x, current_pos.y];
                openList.Delete(current);
                current.Type = 5; // 标记为已遍历过
                // _grid[current_pos.x, current_pos.y] = current;

                method_count = 0;
                int x = current.x;
                int y = current.y;
                var current_pos = new Vector2Int(x, y);

                if (y > 0 && _grid[x, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y - 1);
                if (x > 0 && _grid[x - 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y);
                if (y < y_end && _grid[x, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y + 1);
                if (x < x_end && _grid[x + 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && _grid[x - 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && _grid[x - 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && _grid[x + 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && _grid[x + 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

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
                        // var success = openList.Delete(neighbor);
                        // if (!success)
                        //     DU.LogError("Delete fail");
                        change_times++;
                        openList.Delete(neighbor);
                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                        // _grid[neighbor_pos.x, neighbor_pos.y] = neighbor;
                        openList.Insert(neighbor);
                    }

                }
            }

            DU.LogWarning($"AStar times:{times}  change_times:{change_times}");
        }

        public void StartAStarRedBlack(Vector2Int start, Vector2Int target)
        {
            if (_grid == null) return;
            start = start + new Vector2Int(1, 1);
            target = target + new Vector2Int(1, 1);
            astar_target = target;
            times = 0;
            change_times = 0;
            var openList = new RedBlackTree<AStarCell>();

            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
            // _grid[start.x, start.y] = startNode;

            openList.Insert(startNode);

            var method_list = new Vector2Int[8];
            var method_count = 0;


            // openList 要排序，也要查找
            while (!openList.Empty())
            {
                times++;
                // 获取F值最低的节点
                var current = openList.FindMin();

                if (current.x == target.x && current.y == target.y)
                {
                    break;
                }
                // var current = _grid[current_pos.x, current_pos.y];
                openList.Delete(current);
                current.Type = 5; // 标记为已遍历过
                // _grid[current_pos.x, current_pos.y] = current;

                method_count = 0;
                int x = current.x;
                int y = current.y;
                var current_pos = new Vector2Int(x, y);

                if (_grid[x, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y - 1);
                if (_grid[x - 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y);
                if (_grid[x, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y + 1);
                if (_grid[x + 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y);
                if (_grid[x - 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);
                if (_grid[x - 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);
                if (_grid[x + 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);
                if (_grid[x + 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

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
                        // var success = openList.Delete(neighbor);
                        // if (!success)
                        //     DU.LogError("Delete fail");
                        change_times++;
                        openList.Delete(neighbor);
                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                        // _grid[neighbor_pos.x, neighbor_pos.y] = neighbor;
                        openList.Insert(neighbor);
                    }

                }
            }

            DU.LogWarning($"AStar times:{times}  change_times:{change_times}");
        }

        public void Print()
        {
            openList.InOrderTraversal();
            openList.InOrderTraversalCount();

        }
        public void Step()
        {
            if (openList.Empty()) return;

            // 获取F值最低的节点
            var current = openList.FindMin().Value;
            DU.Log(current.F);
            if (current.x == astar_target.x && current.y == astar_target.y)
            {
                return;
            }

            times++;
            openList.Delete(current);
            current.Type = 5; // 标记为已遍历过

            int x_end = _grid.GetLength(0) - 1;
            int y_end = _grid.GetLength(1) - 1;
            var method_list = new Vector2Int[8];
            int method_count = 0;
            int x = current.x;
            int y = current.y;
            var current_pos = new Vector2Int(x, y);

            if (y > 0 && _grid[x, y - 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x, y - 1);
            if (x > 0 && _grid[x - 1, y].Type == 0)
                method_list[method_count++] = new Vector2Int(x - 1, y);
            if (y < y_end && _grid[x, y + 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x, y + 1);
            if (x < x_end && _grid[x + 1, y].Type == 0)
                method_list[method_count++] = new Vector2Int(x + 1, y);
            if (y > 0 && x > 0 && _grid[x - 1, y - 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x - 1, y - 1);
            if (y < y_end && x > 0 && _grid[x - 1, y + 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x - 1, y + 1);
            if (y < y_end && x < x_end && _grid[x + 1, y + 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x + 1, y + 1);
            if (y > 0 && x < x_end && _grid[x + 1, y - 1].Type == 0)
                method_list[method_count++] = new Vector2Int(x + 1, y - 1);

            for (int i = 0; i < method_count; i++)
            {
                var neighbor_pos = method_list[i];
                var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

                int new_G = current.G + GetDistance(current_pos, neighbor_pos);

                //新的邻节点或者要更新G值的邻节点
                bool is_new = neighbor.G == 0;

                if (is_new)
                {
                    neighbor.G = new_G;
                    neighbor.F = new_G + Estimate(neighbor_pos.x, neighbor_pos.y, astar_target.x, astar_target.y);
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
        public void StartAStarOri(Vector2Int start, Vector2Int target)
        {
            if (_grid == null) return;
            astar_target = target;
            times = 0;
            var openList = new HashSet<Vector2Int>();

            var startNode = _grid[start.x, start.y];
            startNode.G = 0;
            startNode.F = Estimate(start.x, start.y, target.x, target.y); // 使用启发式函数计算H值
            // _grid[start.x, start.y] = startNode;

            openList.Add(start);

            var method_list = new Vector2Int[8];
            var method_count = 0;
            int x_end = _grid.GetLength(0) - 1;
            int y_end = _grid.GetLength(1) - 1;


            // openList 要排序，也要查找
            while (openList.Count > 0)
            {
                times++;
                // 获取F值最低的节点
                var current_pos = GetLowestFScore(openList);

                if (current_pos.x == target.x && current_pos.y == target.y)
                {
                    break;
                }
                var current = _grid[current_pos.x, current_pos.y];
                openList.Remove(current_pos);
                current.Type = 5; // 标记为已遍历过
                // _grid[current_pos.x, current_pos.y] = current;

                method_count = 0;
                int x = current_pos.x;
                int y = current_pos.y;

                if (y > 0 && _grid[x, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y - 1);
                if (x > 0 && _grid[x - 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y);
                if (y < y_end && _grid[x, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x, y + 1);
                if (x < x_end && _grid[x + 1, y].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && _grid[x - 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && _grid[x - 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && _grid[x + 1, y + 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && _grid[x + 1, y - 1].Type == 0)
                    method_list[method_count++] = new Vector2Int(x + 1, y - 1);

                for (int i = 0; i < method_count; i++)
                {
                    var neighbor_pos = method_list[i];
                    var neighbor = _grid[neighbor_pos.x, neighbor_pos.y];

                    int new_G = current.G + GetDistance(current_pos, neighbor_pos);

                    //新的邻节点或者要更新G值的邻节点
                    bool is_new = neighbor.G == 0;
                    if (new_G < neighbor.G)
                    {
                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                        // _grid[neighbor_pos.x, neighbor_pos.y] = neighbor;

                    }
                    if (is_new)
                    {
                        neighbor.G = new_G;
                        neighbor.F = new_G + Estimate(neighbor_pos.x, neighbor_pos.y, target.x, target.y);
                        neighbor.ParentPos = current_pos;
                        openList.Add(neighbor_pos);
                    }
                    else if (new_G < neighbor.G)
                    {
                        neighbor.F = neighbor.F - neighbor.G + new_G;
                        neighbor.G = new_G;
                        neighbor.ParentPos = current_pos;
                        // _grid[neighbor_pos.x, neighbor_pos.y] = neighbor;
                    }

                }

                // foreach (var neighbor_pos in GetNeighbors(current_pos))
                // {

                // }
            }

            DU.LogWarning("AStar times:" + times);
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

        private Vector2Int GetLowestFScore(HashSet<Vector2Int> list)
        {
            var def = new Vector2Int(-1, -1);
            Vector2Int min_pos = def;
            AStarCell min = default;

            foreach (var pos in list)
            {
                var node = _grid[pos.x, pos.y];
                if (min_pos == def || node.F < min.F || node.F == min.F && node.H < min.H)
                {
                    min = node;
                    min_pos = pos;
                }
            }

            return min_pos;
        }


        #endregion


        #region 筛选

        public class MapRegion
        {
            public int type = 0;        // 0-暗地，1-亮地（迷雾和边界）
            public int team_index = -1; // 所属分类，4向相邻为1类。用-2代表 由连线生成的“亮地”
        }


        public void Filter()
        {
            #region 筛-初始亮地


            // int[,] colorData = new int[_imgW + 4, _imgH + 4];
            int[,] colorData = null;


            int region_len = 20;
            int region_map_len = 200 / region_len;
            MapRegion[,] regions = new MapRegion[region_map_len, region_map_len];
            for (int ry = 0; ry < region_map_len; ry++)
                for (int rx = 0; rx < region_map_len; rx++)
                {
                    regions[rx, ry] = new MapRegion();
                }

            Action<int, int> action = (rx, ry) =>
            {
                int px_start = rx * region_len;
                int py_start = ry * region_len;
                int px_end = (rx + 1) * region_len;
                int py_end = (ry + 1) * region_len;
                // 防止游动图标干扰。废弃顶部、底部21个像素
                //
                px_start = Math.Clamp(px_start, 22, 178);
                py_start = Math.Clamp(py_start, 22, 178);
                px_end = Math.Clamp(px_end, 22, 178);
                py_end = Math.Clamp(py_end, 22, 178);
                for (int py = py_start; py < py_end; py++)
                    for (int px = px_start; px < px_end; px++)
                    {
                        if (colorData[px, py] != 0)
                        {
                            regions[rx, ry].type = 1;
                            return;
                        }
                    }
            };
            // 外圈可能有动态图标，咱们往内缩一格
            //
            for (int ry = 1; ry < region_map_len - 1; ry++)
                for (int rx = 1; rx < region_map_len - 1; rx++)
                {
                    action(rx, ry);
                }

            #endregion






            #region 筛-分类亮地
            // 全部连线
            Dictionary<int, List<Vector2Int>> all_line = new Dictionary<int, List<Vector2Int>>();
            Dictionary<int, List<Vector2Int>> top_line = new Dictionary<int, List<Vector2Int>>();
            Dictionary<int, List<Vector2Int>> right_line = new Dictionary<int, List<Vector2Int>>();
            Dictionary<int, List<Vector2Int>> bottom_line = new Dictionary<int, List<Vector2Int>>();
            Dictionary<int, List<Vector2Int>> left_line = new Dictionary<int, List<Vector2Int>>();

            int team_index = 0;
            for (int ry = 1; ry < region_map_len - 1; ry++)
                for (int rx = 1; rx < region_map_len - 1; rx++)
                {
                    MapRegion region = regions[rx, ry];
                    if (region.type == 1 && region.team_index == -1)
                    {
                        var list = Traversal1(regions, new Vector2Int(rx, ry), team_index);
                        all_line[team_index] = list;
                        List<Vector2Int> top = new List<Vector2Int>();
                        List<Vector2Int> right = new List<Vector2Int>();
                        List<Vector2Int> bottom = new List<Vector2Int>();
                        List<Vector2Int> left = new List<Vector2Int>();

                        foreach (var pos in list)
                        {
                            int x = pos.x;
                            int y = pos.y;
                            if (x + y >= 9 && y >= x) top.Add(pos);
                            if (x + y <= 9 && y <= x) bottom.Add(pos);
                            if (x + y >= 9 && y <= x) right.Add(pos);
                            if (x + y <= 9 && y >= x) left.Add(pos);
                        }
                        if (top.Count > 0) top_line[team_index] = top;
                        if (right.Count > 0) right_line[team_index] = right;
                        if (bottom.Count > 0) bottom_line[team_index] = bottom;
                        if (left.Count > 0) left_line[team_index] = left;

                        team_index++;
                    }
                }
            #endregion






            #region 筛-

            for (int ry = 1; ry < region_map_len - 1; ry++)
                for (int rx = 1; rx < region_map_len - 1; rx++)
                {
                    MapRegion region = regions[rx, ry];
                    if (region.type == 1 && region.team_index == -1)
                    {

                    }
                }


            #endregion






            #region 筛-画图

            // Check    
            for (int ry = 1; ry < region_map_len - 1; ry++)
                for (int rx = 1; rx < region_map_len - 1; rx++)
                {
                    MapRegion region = regions[rx, ry];
                    if (region.type == 1)
                    {
                        int px_start = rx * region_len;
                        int py_start = ry * region_len;
                        int px_end = (rx + 1) * region_len;
                        int py_end = (ry + 1) * region_len;
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (colorData[px, py] == 0)
                                {
                                    colorData[px, py] = 20 + region.team_index;
                                }
                            }
                    }
                }

            #endregion

        }

        List<Vector2Int> Traversal1(MapRegion[,] colorData, Vector2Int start, int teamId)
        {
            var result = new List<Vector2Int>();
            colorData[start.x, start.y].team_index = teamId;

            var count = 0;
            var stack = new Vector2Int[64];
            stack[count++] = start;

            while (count > 0)
            {
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;
                result.Add(pop);

                foreach (var offset in Utils.FourDirList)
                {
                    MapRegion r = colorData[offset.x + x, offset.y + y];
                    if (r.type == 1 && r.team_index == -1)
                    {
                        r.team_index = teamId;
                        stack[count++] = new Vector2Int(offset.x + x, offset.y + y);
                    }
                }
            }



            return result;
        }
        #endregion

    }

    public class AStarCell : IComparable<AStarCell>
    {
        public int x;
        public int y;

        public byte Type; // 0-为暗地 2-边界 4-迷雾边界 5-已遍历过

        /// <summary>
        /// 实际值
        /// </summary>
        public int G;

        /// <summary>
        /// 启发值、估计值
        /// </summary>
        public int H => F - G;

        public int F;

        /// <summary>
        /// 链表
        /// </summary>
        public Vector2Int ParentPos;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(AStarCell other)
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



        ////////////////////   在ImageCompareTestPanel中的，对MapData的A*寻路的测试方法  ///////////////
        Sprite _show_sprite;
        MapData _mapData;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // for (int i = 0; i < 5; i++)
                //     _mapData.Step();

                // _mapData.Print();
                // var save_path = Application.streamingAssetsPath + $"/SmallMap/astar.png";
                // _mapData.SaveAStar(save_path);

                // _show_sprite = ImageManager.Inst.LoadSpriteInStreaming(save_path);
                // RightImage.SetData(_show_sprite);
            }
        }

        void OnClickBtn4th()
        {
            // var str = InputText.text;
            var str = "(60,40,200,230)";
            str.Replace(" ", "");
            str = str.Substring(1, str.Length - 2);
            var arr = str.Split(',');
            var start = new Vector2Int(int.Parse(arr[0]), int.Parse(arr[1]));
            var target = new Vector2Int(int.Parse(arr[2]), int.Parse(arr[3]));



            // if (_mapData == null)
            // {
            MapDataManager.Inst.Remove("Map-22");
            MapDataManager.Inst.Create("Map-22", new CVRect(0, 0, 200, 200),0);
            _mapData = MapDataManager.Inst.Get("Map-22");
            // var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\SmallMap\小地图_22";
            var dir = @"D:\unityProject\MyProject\Assets\StreamingAssets\Capture\小地图_22";


            for (int i = 11; i <= 21; i++)
            {
                var file_path = dir + $"/{i}.png";
                _mapData.Capture(new Bitmap(file_path));
            }
            // }




            // _mapData.InitGridOriginal();

            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarOri(start, target);
            // }, "AStarOri");

            // _mapData.InitGrid();

            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStar(start, target);
            // }, "AStar");

            // _mapData.InitGrid();
            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarAVL(start, target);
            // }, "AStarAVLTree");


            // _mapData.InitGridOriginal();
            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarRedBlack(start, target);
            // }, "AStarRedBlackTree");


            // DU.RunWithTimer(() =>
            // {
            //     _mapData.StartAStarBigGrid(start, target);
            // }, "StartAStarBigGrid");


            // _mapData.Print();

            var save_path = Application.streamingAssetsPath + $"/SmallMap/astar.png";
            _mapData.SaveAStar(save_path);

        }

        ////////////////////   End     //////////////////// 

        #region ApplyEdgeFog

        // 补充边缘迷雾
        // 补充迷雾：[160X160]的最外层厚度1px像素。若(与空地相连)&& ，则标为迷雾
        // _light_map ：视野刷子区域可能比 _map区域外扩1px
        // void ApplyEdgeFog(CellType[,] small_map)
        // {
        //     Vector2Int offset = _templatePos - _judgePos;
        //     int xs = 21, xe = 179, ys = 21, ye = 179;

        //     // 补充边缘迷雾
        //     for (int y = ys; y < ye; y++)
        //     {
        //         // 原来算法, 走到主循环中会污染一下Undefined，所以放弃走主循环，一步到位

        //         var mx = 20 + _templatePos.x;
        //         var my = y + _templatePos.y;
        //         var jx = 20 + offset.x;
        //         var jy = y + offset.y;
        //         var jData = _judge_map[jx, jy];
        //         // 本像素没访问过，small_map邻近像素是空地
        //         if (small_map[21, y] == CellType.Empty && !jData.Access)
        //         {
        //             _map[mx, my] = CellType.Fog;
        //             jData.Access = true;
        //             _judge_map[jx, jy] = jData;
        //         }


        //         mx = 179 + _templatePos.x;
        //         my = y + _templatePos.y;
        //         jx = 179 + offset.x;
        //         jy = y + offset.y;
        //         jData = _judge_map[jx, jy];
        //         if (small_map[178, y] == CellType.Empty && !jData.Access)
        //         {
        //             _map[mx, my] = CellType.Fog;
        //             jData.Access = true;
        //             _judge_map[jx, jy] = jData;
        //         }

        //     }
        //     // 补充边缘迷雾
        //     for (int x = xs; x < xe; x++)
        //     {

        //         var mx = x + _templatePos.x;
        //         var my = 20 + _templatePos.y;
        //         var jx = x + offset.x;
        //         var jy = 20 + offset.y;
        //         var jData = _judge_map[jx, jy];
        //         if (small_map[x, 21] == CellType.Empty && !jData.Access)
        //         {
        //             _map[mx, my] = CellType.Fog;
        //             jData.Access = true;
        //             _judge_map[jx, jy] = jData;
        //         }


        //         mx = x + _templatePos.x;
        //         my = 179 + _templatePos.y;
        //         jx = x + offset.x;
        //         jy = 179 + offset.y;
        //         jData = _judge_map[jx, jy];
        //         if (small_map[x, 178] == CellType.Empty && !jData.Access)
        //         {
        //             _map[mx, my] = CellType.Fog;
        //             jData.Access = true;
        //             _judge_map[jx, jy] = jData;
        //         }
        //     }

        // }

        #endregion
    }

}