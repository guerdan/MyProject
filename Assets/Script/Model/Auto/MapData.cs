using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Model.ListStruct;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public enum CellType : byte
    {
        Undefined = 0,      // 默认，此块不会参与功能
        Empty = 1,          // 空地
        ObstacleEdge = 2,   // 障碍边界, 处理过后代表所有障碍。
        NewObstacleEdge = 3,// 新障碍边界，用于通知大格子更新，后变为ObstacleEdge
        Fog = 4,            // 迷雾边界
        Abandoned = 5,      // 废弃地块
        Visited = 6,        // 已遍历过
        Temp = 7,           // 临时

    }

    /// <summary>
    /// 地图数据
    /// 从截屏Bitmap中截取小地图开始
    /// 左下角为(0,0)，涉及到模版匹配时，坐标系转换到左上角为(0,0)
    /// </summary>
    public class MapData
    {
        public float CaptureInterval = 0.5f;    // 截图间隔时间
        readonly int mapInitialEdge = 200;      // 初始地图边长
        readonly int mapDistanceThreshold = 10; // 移动阈值，距边10  这样不用判断map取值出界
        readonly int mapSizeThreshold = 50;     // 扩容阈值，边长还差50
        readonly int mapExpandEdge = 400;       // 每次扩容增加的长度

        float _captureTimer = 0f;           // 截图计时器

        // 地图的总数据。 x轴向右，y轴向上。
        // Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了
        // 这种情况下，模版匹配的坐标是图片的左下角。
        // 0-为暗地  2-边界 4-迷雾边界  (3-临时待定边界)
        public CellType[,] _map;            // 只有Capture可写，其他方法可读
        public CellType[,] _map1;           // 可读可写
        public int _mapEdge;                // 当前地图的容器边长
        Vector2Int _xRange;          // 内容x轴范围
        Vector2Int _yRange;          // 内容y轴范围
        int _w;                             // 内容宽  
        int _h;                             // 内容高

        GridData _gridData;

        CVRect _rect;                       // 小地图在屏幕中的位置 200 * 200
        int _rectW;                         // 小地图宽
        int _rectH;                         // 小地图高
        Mat _template;                      // 模版图，截取小地图中间120 * 120的区域
        Vector2Int _templatePos;            // 模版图左下角坐标


        /// <summary>
        /// rect: 小地图在屏幕中的位置;
        /// </summary>
        public MapData(CVRect rect)
        {
            _rect = rect;
            _rectW = rect.w;
            _rectH = rect.h;

            _captureTimer = CaptureInterval;
            _mapEdge = mapInitialEdge;
            _map = new CellType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _map1 = new CellType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _templatePos = new Vector2Int((_mapEdge - 200) / 2, (_mapEdge - 200) / 2);   // 右上角就是(599,599)

            if (_gridData == null)
                _gridData = new GridData(this);

        }


        #region 拼地图
        public void Capture(Bitmap bitmap)
        {

            Color32[] pixels;
            using (bitmap)
            {
                // 转成像素数组, 再垂直方向反序一下，从下至上，和人观察习惯一致
                // 
                pixels = IU.BitmapToColor32(bitmap);
                pixels = ReverseVerticalColor(pixels, _rectW);
            }
            CellType[,] small_map = ColorToData(pixels);

            bool is_start = _template == null;
            if (is_start)
            {
                Apply(small_map, _templatePos);
            }
            else
            {
                // 源图，计算位移
                using (Mat source = GetMat(small_map, 154))// 指针指向像素数组，no copy
                {
                    // 匹配
                    using (Mat result = IU.MatchTemplate1(source, _template))
                    {
                        var list = IU.FindResult(result, 120, 120, 0f, out _);
                        list.Sort((a, b) => b.Score.CompareTo(a.Score));
                        var rect = list[0].Rect;

                        // IU.PrintScore(result, "SmallMap Match");

                        //计算位移  动了是(5, 8)    不动是(17,17)。最终就是匹配图像走了(-12,-9),人走了(12,9)
                        var delta = new Vector2Int(17 - (int)rect.x, 17 - (int)rect.y);
                        _templatePos += delta;
                    }
                    _template.Dispose();  //用完了就释放
                    _template = null;
                    Apply(small_map, _templatePos);
                }
            }

            // 模版图
            _template = GetMat(small_map, 120);
        }


        Color32[] ReverseVerticalColor(Color32[] pixels, int w)
        {
            int h = pixels.Length / w;
            Color32[] result = new Color32[pixels.Length];
            for (int i = 0; i < h; i++)
                for (int j = 0; j < w; j++)
                    result[(h - 1 - i) * w + j] = pixels[i * w + j];
            return result;
        }


        /// <summary>
        /// 处理 ,2-“边界”，4-“迷雾边界”
        /// </summary>
        CellType[,] ColorToData(Color32[] pixels)
        {
            CellType[,] result = new CellType[_rectH, _rectW]; // 200 * 200 
            var min_1th = new Color32(120, 120, 160, 255);
            var max_1th = new Color32(145, 145, 190, 255);
            var min_2th = new Color32(80, 80, 110, 255);
            var max_2th = max_1th;
            var min_3th = new Color32(0, 125, 165, 255);
            var max_3th = new Color32(100, 145, 190, 255);
            var first_list = new List<Vector2Int>();

            // DU.RunWithTimer(() =>
            //     {
            for (int i = 0; i < _rectH; i++)
            {
                for (int j = 0; j < _rectW; j++)
                {
                    int index = i * _rectW + j;
                    var color = pixels[index];

                    if (Between(color, min_1th, max_1th)
                        && color.b - color.g >= 40 && color.b - color.g <= 60)
                    {
                        first_list.Add(new Vector2Int(j, i));
                        result[j, i] = CellType.NewObstacleEdge;
                    }
                    else if (Between(color, min_3th, max_3th))
                    {
                        result[j, i] = CellType.Fog;
                    }

                    else if (Between(color, min_2th, max_2th)
                        && color.b - color.g >= 28 && color.b - color.g <= 60)
                    {
                        result[j, i] = CellType.Temp;
                    }
                    else
                    {
                        result[j, i] = CellType.Empty;
                    }

                }
            }
            // }, "Condition check");

            // < 1ms
            // DU.RunWithTimer(() =>
            // {
            ColorToDataTraversal(result, first_list);
            // }, "Traversal");

            for (int i = 0; i < _rectH; i++)
                for (int j = 0; j < _rectW; j++)
                    if (result[j, i] == CellType.Temp)
                        result[j, i] = CellType.Empty;


            return result;
        }

        /// <summary>
        /// 遍历
        /// 递归,发散型递归。感觉用栈可以实现非递归
        /// 猜想优化点：将colorData扩展为[w+1,h+1]，边缘设置为0，这样就不用每次都判断边界了
        /// </summary>
        List<Vector2Int> ColorToDataTraversal(CellType[,] colorData, List<Vector2Int> first_list)
        {
            var result = new List<Vector2Int>();
            int y_end = _rectH - 1;
            int x_end = _rectW - 1;

            Vector2Int[] stack = new Vector2Int[first_list.Count * 4];
            var count = 0;
            foreach (var pos in first_list)
            {
                stack[count++] = pos;
            }

            while (count > 0)
            {
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;
                if (colorData[x, y] == CellType.Temp)
                {
                    result.Add(new Vector2Int(x, y));
                    colorData[x, y] = CellType.NewObstacleEdge; // 标记为"边界"
                }

                if (y > 0 && colorData[x, y - 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x, y - 1);
                if (x > 0 && colorData[x - 1, y] == CellType.Temp)
                    stack[count++] = new Vector2Int(x - 1, y);
                if (y < y_end && colorData[x, y + 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x, y + 1);
                if (x < x_end && colorData[x + 1, y] == CellType.Temp)
                    stack[count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && colorData[x - 1, y - 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && colorData[x - 1, y + 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && colorData[x + 1, y + 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && colorData[x + 1, y - 1] == CellType.Temp)
                    stack[count++] = new Vector2Int(x + 1, y - 1);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        bool Between(Color32 val, Color32 min, Color32 max)
        {
            return val.r >= min.r && val.g >= min.g && val.b >= min.b
                && val.r <= max.r && val.g <= max.g && val.b <= max.b;
        }

        Mat GetMat(CellType[,] small_map, int length)
        {
            int x_start = (_rectW - length) / 2;
            int y_start = (_rectH - length) / 2;

            byte[] source_bytes = new byte[length * length];

            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    var map_data = small_map[j + x_start, i + y_start];
                    if (map_data == CellType.ObstacleEdge || map_data == CellType.NewObstacleEdge)
                    {
                        source_bytes[i * length + j] = 255;
                    }
                }


            Mat source = Mat.FromPixelData(length, length, MatType.CV_8UC1, source_bytes);
            return source;
        }

        #region Apply

        /// <summary>
        /// 把一张小地图，更新到大地图中
        /// 大地图中，边界永久保留，非“边界”实时更新
        /// 更新原点
        /// </summary>
        void Apply(CellType[,] small_map, Vector2Int start)
        {
            DU.RunWithTimer(() =>
            {
                var x_start = start.x;
                var y_start = start.y;
                var x_end = x_start + _rectW - 1;
                var y_end = y_start + _rectH - 1;

                if (_xRange == default)
                {
                    _xRange = new Vector2Int(x_start, x_end);
                    _yRange = new Vector2Int(y_start, y_end);
                }
                else
                {
                    if (x_start < _xRange.x) _xRange.x = x_start;
                    if (x_end > _xRange.y) _xRange.y = x_end;
                    if (y_start < _yRange.x) _yRange.x = y_start;
                    if (y_end > _yRange.y) _yRange.y = y_end;
                }

                CheckRebuild(out bool is_out, out Vector2Int offset);
                UpdateContentAttr();    

                if (is_out)
                {
                    start += offset;
                    x_start = start.x;
                    y_start = start.y;
                    x_end = x_start + _rectW - 1;
                    y_end = y_start + _rectH - 1;
                }

                for (int i = y_start; i <= y_end; i++)
                    for (int j = x_start; j <= x_end; j++)
                    {
                        var data = small_map[j - x_start, i - y_start];
                        if (_map[j, i] != CellType.ObstacleEdge && _map[j, i] != CellType.NewObstacleEdge)
                            _map[j, i] = data;
                    }

                // 因为_gridData依赖map1，所以先更新 map1，再更新 _gridData
                // 另外只选取 small_map(200X200) 中间的150X150区域更新，这部分的边界确保正常封口了。
                ApplyMap1(small_map, start);

                _gridData.Apply(start + new Vector2Int(25, 25), new Vector2Int(150, 150));

            }, "Apply");
        }
        /// <summary>
        /// 更新内容的宽高
        /// </summary>
        public void UpdateContentAttr()
        {
            _w = _xRange.y - _xRange.x + 1;
            _h = _yRange.y - _yRange.x + 1;
        }
        public void GetContentAttr(out Vector2Int xRange, out Vector2Int yRange, out int w, out int h)
        {
            xRange = _xRange;
            yRange = _yRange;
            w = _w;
            h = _h;
        }
 

        /// <summary>
        /// 检查：实际内容是否超出了存储边界
        /// 执行：1.只是边界超出就移动；2.若容量超出就扩容+移动
        /// </summary>
        void CheckRebuild(out bool is_out, out Vector2Int offset)
        {
            is_out = false;
            offset = default;
            int x_min = _xRange.x;
            int x_max = _xRange.y;
            int y_min = _yRange.x;
            int y_max = _yRange.y;
            int x_len = x_max - x_min;
            int y_len = y_max - y_min;


            bool expand = false;
            int expand_reach = _mapEdge - mapSizeThreshold;

            if (x_len >= expand_reach || y_len >= expand_reach)
            {
                expand = true;
                _mapEdge += mapExpandEdge;
            }


            if (x_min < mapDistanceThreshold
            || x_max >= _mapEdge - mapDistanceThreshold
            || y_min < mapDistanceThreshold
            || y_max >= _mapEdge - mapDistanceThreshold)
            {
                is_out = true;
            }

            if (is_out || expand)
            {
                // 计算地图的新偏移。让非空内容居中，并满足_gridData的整除要求

                var x = _gridData.GetDivisibleInt((_mapEdge - x_len) / 2);
                var y = _gridData.GetDivisibleInt((_mapEdge - y_len) / 2);
                var start = new Vector2Int(x, y);
                offset = new Vector2Int(start.x - _xRange.x, start.y - _yRange.x);

                CellType[,] newMap = new CellType[_mapEdge, _mapEdge];
                CellType[,] newMap1 = new CellType[_mapEdge, _mapEdge];
                int old_edge = _map.GetLength(0);
                x_min = Math.Max(0, x_min);
                x_max = Math.Min(old_edge - 1, x_max);
                y_min = Math.Max(0, y_min);
                y_max = Math.Min(old_edge - 1, y_max);

                for (int i = y_min; i <= y_max; i++)
                    for (int j = x_min; j <= x_max; j++)
                    {
                        newMap[j + offset.x, i + offset.y] = _map[j, i];
                        newMap1[j + offset.x, i + offset.y] = _map1[j, i];
                    }
                _map = newMap;
                _map1 = newMap1;

                _templatePos += offset;
                //改xRange
                _xRange = _xRange + new Vector2Int(offset.x, offset.x);
                _yRange = _yRange + new Vector2Int(offset.y, offset.y);

                // offset一定能整除_gridData._scale
                _gridData.Rebuild(old_edge, _mapEdge, offset);
            }

        }

        #endregion



        #endregion







        #region map1
        Vector2Int[] stack = new Vector2Int[40000];
        CellType[,] _mapT = new CellType[150, 150];

        public void ApplyMap1(CellType[,] small_map, Vector2Int start)
        {

            // _map地块分类, 计算障碍
            // 从中心点出发，四方遍历刷格子
            for (int y = 0; y < 150; y++)
                for (int x = 0; x < 150; x++)
                {
                    _mapT[x, y] = CellType.ObstacleEdge;
                }

            var x_start = start.x + 25;
            var y_start = start.y + 25;
            var x_end = x_start + 150 - 1;
            var y_end = y_start + 150 - 1;


            var count = 0;
            stack[count++] = new Vector2Int(start.x + 100, start.y + 99);

            while (count > 0)
            {
                var pop = stack[--count];
                int x = pop.x;
                int y = pop.y;

                _mapT[x - x_start, y - y_start] = CellType.Empty;


                if (y > y_start && _map[x, y - 1] == CellType.Empty && _mapT[x - x_start, y - y_start - 1] != CellType.Empty)
                    stack[count++] = new Vector2Int(x, y - 1);
                if (x > x_start && _map[x - 1, y] == CellType.Empty && _mapT[x - x_start - 1, y - y_start] != CellType.Empty)
                    stack[count++] = new Vector2Int(x - 1, y);
                if (y < y_end && _map[x, y + 1] == CellType.Empty && _mapT[x - x_start, y - y_start + 1] != CellType.Empty)
                    stack[count++] = new Vector2Int(x, y + 1);
                if (x < x_end && _map[x + 1, y] == CellType.Empty && _mapT[x - x_start + 1, y - y_start] != CellType.Empty)
                    stack[count++] = new Vector2Int(x + 1, y);
            }

            for (int y = 0; y < 150; y++)
                for (int x = 0; x < 150; x++)
                {
                    if (_map1[x + x_start, y + y_start] != CellType.Empty)
                        _map1[x + x_start, y + y_start] = _mapT[x, y];
                }

        }


        #endregion
        #region Save
        public void Save(string path)
        {
            byte[] bytes = new byte[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var data = _map[x + _xRange.x, y + _yRange.x];
                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次    
                    bytes[index] = data == CellType.ObstacleEdge || data == CellType.NewObstacleEdge
                         ? (byte)255 : (byte)0;
                    bytes[index] = data == CellType.Fog ? (byte)128 : bytes[index];
                    bytes[index] = data == CellType.Empty ? (byte)10 : bytes[index];
                }

            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC1, bytes))
            {
                Cv2.ImWrite(path, mat);
            }


            bytes = new byte[_w * _h];
            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    var data = _map1[j + _xRange.x, i + _yRange.x];
                    int index = (_h - 1 - i) * _w + j;                // 反转y轴，因为之前翻转过一次   

                    bytes[index] = data == CellType.Empty ? (byte)10 : bytes[index];
                    bytes[index] = data == CellType.ObstacleEdge ? (byte)255 : bytes[index];
                }

            var path1 = path.Substring(0, path.Length - 4) + "_1.png";

            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC1, bytes))
            {
                Cv2.ImWrite(path1, mat);
            }

        }
        #endregion


        public void Dispose()
        {
            if (_template != null)
            {
                _template.Dispose();
                _template = null;
            }

        }


        #region A*寻路





        /// <summary>
        /// Type: 0-为暗地 2-边界 4-迷雾边界 5-已遍历过 
        /// </summary>
        AStarCell[,] _grid;
        int times;
        int change_times;

        BinarySearchTree<AStarCell> openList;
        Vector2Int astar_target;

        #region InitGridOld

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
                    if (data == CellType.NewObstacleEdge)
                    {
                        data = CellType.ObstacleEdge;
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
                    if (data == CellType.ObstacleEdge)
                    {
                        if (j > 0 && i < _h - 1 && _map[x - 1, y + 1] == CellType.ObstacleEdge
                        && _map[x - 1, y] == CellType.Empty && _map[x, y + 1] == CellType.Empty)
                        {
                            _map[x - 1, y] = CellType.Temp;
                            _map[x, y + 1] = CellType.Temp;
                        }

                        if (j < _w - 1 && i < _h - 1 && _map[x + 1, y + 1] == CellType.ObstacleEdge
                        && _map[x + 1, y] == CellType.Empty && _map[x, y + 1] == CellType.Empty)
                        {

                            _map[x + 1, y] = CellType.Temp;
                            _map[x, y + 1] = CellType.Temp;
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
                    if (data == CellType.ObstacleEdge || data == CellType.Temp)
                    {
                        cell.Type = 2;
                    }
                    // 还原
                    if (data == CellType.Temp)
                    {
                        _map[j + _xRange.x, i + _yRange.x] = CellType.Empty;
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

        #endregion
        AStarCell GridGet(int x, int y)
        {
            return _grid[x + 1, y + 1];
        }

        void GridSet(int x, int y, AStarCell cell)
        {
            _grid[x + 1, y + 1] = cell;
        }
        #region StartAStar

        public void StartAStar(Vector2Int start, Vector2Int target)
        {
            if (_grid == null) return;
            astar_target = target;
            times = 0;
            openList = new BinarySearchTree<AStarCell>();

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

            DU.LogWarning("AStar times:" + times);
        }

        public void StartAStarBigGrid(Vector2Int start, Vector2Int target)
        {
            var offset = new Vector2Int(_xRange.x, _yRange.x);
            start = (start + offset) / 5;
            target = (target + offset) / 5;
            _gridData.StartAStar(start, target);
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

        #endregion
        public void Print()
        {
            openList.InOrderTraversal();
            openList.InOrderTraversalCount();

        }
        #region Step
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
        #endregion
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

        public void SaveAStar(string path)
        {
            var path1 = path.Substring(0, path.Length - 4) + "_BigCell.png";
            _gridData.SaveAStar(path1);

            byte[] bytes = new byte[_w * _h];
            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    var data = _grid[j + 1, i + 1].Type;
                    int index = i * _w + j;                // 反转y轴，因为之前翻转过一次    
                    bytes[index] = data == 2 ? (byte)255 : (byte)0;
                    bytes[index] = data == 4 ? (byte)200 : bytes[index];
                    bytes[index] = data == 5 ? (byte)64 : bytes[index];
                    bytes[index] = data == 1 ? (byte)10 : bytes[index];
                }



            var target = GridGet(astar_target.x, astar_target.y);
            while (target.ParentPos != new Vector2Int(-1, -1))
            {
                var pos = target.ParentPos;
                var show_pos = new Vector2Int(pos.x - 1, pos.y - 1);
                int index = show_pos.y * _w + show_pos.x;
                bytes[index] = 128;
                target = _grid[pos.x, pos.y];
            }


            byte[] bytes_new = new byte[_w * _h];
            for (int i = 0; i < _h; i++)
                for (int j = 0; j < _w; j++)
                {
                    bytes_new[(_h - 1 - i) * _w + j] = bytes[i * _w + j];
                }
            bytes = bytes_new;

            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC1, bytes))
            {
                Cv2.ImWrite(path, mat);
            }
        }


        #endregion

    }
    #region AStarCell

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

    }

    #endregion
    #region MapDataManager

    /// <summary>
    /// MapDataManager为MapData服务
    /// </summary>
    public class MapDataManager
    {
        private static MapDataManager _inst;
        public static MapDataManager Inst
        { get { if (_inst == null) _inst = new MapDataManager(); return _inst; } }

        public Dictionary<string, MapData> mapDataDic = new Dictionary<string, MapData>();


        public void Create(string id, CVRect rect)
        {
            if (mapDataDic.ContainsKey(id))
                return;
            mapDataDic[id] = new MapData(rect);
        }

        public MapData Get(string id)
        {
            if (mapDataDic.TryGetValue(id, out var mapData))
                return mapData;
            return null;
        }


        public void Remove(string id)
        {
            if (!mapDataDic.ContainsKey(id))
                return;
            var mapData = mapDataDic[id];
            mapDataDic.Remove(id);
            mapData.Dispose();
        }

    }

    #endregion
}