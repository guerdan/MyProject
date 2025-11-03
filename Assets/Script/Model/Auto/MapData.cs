using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Model.ListStruct;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public enum CellType : byte
    {
        Undefined = 0,      // 默认，此块不会参与功能。          灰色
        Empty = 1,          // 空地。                          黑色
        ObstacleEdge = 2,   // 障碍边界, 处理过后代表所有障碍。  白色
        ObstacleFill = 3,   // 障碍填充区域                    浅白色
        ObstacleByBig = 4,  // 大格子标定的障碍                 橙色
        NewObstacleEdge = 5,// 新障碍边界，用于通知大格子更新，后变为ObstacleEdge
        Fog = 6,            // 迷雾边界
        Temp = 7,           // 临时
    }

    /// <summary>
    /// 地图数据
    /// 从截屏Bitmap中截取小地图开始
    /// 左下角为(0,0)，涉及到模版匹配时，坐标系转换到左上角为(0,0)
    /// </summary>
    public class MapData
    {
        public Vector2Int Debug_SmallMap_Start;
        public int Debug_SmallMap_Player_In_Obstacle;
        readonly Color32 gray = new Color32(80, 80, 80, 255);      // 灰色,代表CellType.Undefined
        readonly int mapInitialEdge = 200;      // 初始地图边长
        readonly int mapDistanceThreshold = 10; // 移动阈值，距边10  这样不用判断map取值出界
        readonly int mapSizeThreshold = 50;     // 扩容阈值，边长还差50
        readonly int mapExpandEdge = 400;       // 每次扩容增加的长度

        readonly int sourceEdge = 150;          // 源图边长
        readonly int templateEdge = 100;        // 模版图边长

        public List<Vector2Int> moveRecord = new List<Vector2Int>();

        // 地图的总数据。 x轴向右，y轴向上。
        // Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了
        // 这种情况下，模版匹配的坐标是图片的左下角。
        // 0-为暗地  2-边界 4-迷雾边界  (3-临时待定边界)
        public CellType[,] _map;            // 只有Capture可写，其他方法可读
        public CellType[,] _map1;           // 可读可写
        public GridData _gridData;
        public int _mapEdge;                // 当前地图的容器边长
        Vector2Int _xRange;          // 内容x轴范围
        Vector2Int _yRange;          // 内容y轴范围
        int _w;                             // 内容宽  
        int _h;                             // 内容高


        CVRect _rect;                       // 小地图在屏幕中的位置 200 * 200
        int _rectW;                         // 小地图宽
        int _rectH;                         // 小地图高
        Mat _template;                      // 模版图，截取小地图中间120 * 120的区域
        Vector2Int _templatePos;            // 模版图左下角坐标

        Vector2Int ellipseRadius;           // 椭圆轴半径
        Vector3Int ellipseRadiusSquare;     // 椭圆轴半径平方,平方积


        Vector2Int[] _stack = new Vector2Int[40000];    //方法内部复用
        CellType[,] _mapT = new CellType[150, 150];     //方法内部复用


        /// <summary>
        /// rect: 小地图在屏幕中的位置;
        /// </summary>
        public MapData(CVRect rect)
        {
            _rect = rect;
            _rectW = rect.w;
            _rectH = rect.h;

            _mapEdge = mapInitialEdge;
            _map = new CellType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _map1 = new CellType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _templatePos = new Vector2Int((_mapEdge - 200) / 2, (_mapEdge - 200) / 2);   // 右上角就是(599,599)

            if (_gridData == null)
                _gridData = new GridData(this);

            ellipseRadius = new Vector2Int(48, 38);
            ellipseRadiusSquare = new Vector3Int(ellipseRadius.x * ellipseRadius.x, ellipseRadius.y * ellipseRadius.y, 0);
            ellipseRadiusSquare = new Vector3Int(ellipseRadiusSquare.x, ellipseRadiusSquare.y
                , ellipseRadiusSquare.x * ellipseRadiusSquare.y);
        }


        #region 拼地图

        public void Capture(Bitmap bitmap)
        {
            DU.RunWithTimer(() =>
            {
                Color32[] pixels;
                using (bitmap)
                {
                    // 转成像素数组, 再垂直方向反序一下，从下至上，和人观察习惯一致
                    // 
                    pixels = IU.BitmapToColor32(bitmap);
                    pixels = IU.Color32ReverseVertical(pixels, _rectW);
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
                    using (Mat source = GetMat(small_map, sourceEdge))// 指针指向像素数组，no copy
                    {
                        // 匹配
                        using (Mat result = IU.MatchTemplate1(source, _template))
                        {

                            var list = IU.FindResult(result, templateEdge, templateEdge, 0.1f, out _);
                            list.Sort((a, b) => b.Score.CompareTo(a.Score));
                            var score = list.Count > 0 ? list[0].Score : 0;
                            DU.LogWarning($"[Capture FindResult] {list.Count}个  {score}分");
                            if (list.Count == 0)
                                return;
                            var rect = list[0].Rect;

                            // IU.PrintScore(result, "SmallMap Match");

                            //计算位移  动了是(5, 8)    不动是(17,17)。最终就是匹配图像走了(-12,-9),人走了(12,9)
                            int offset = (sourceEdge - templateEdge) / 2;
                            var delta = new Vector2Int(offset - rect.x, offset - rect.y);
                            moveRecord.Add(delta);
                            _templatePos += delta;
                        }
                        _template.Dispose();  //用完了就释放
                        _template = null;
                        Apply(small_map, _templatePos);

                    }
                }

                // 模版图
                _template = GetMat(small_map, templateEdge);

            }, "MapData.Capture");
        }




        /// <summary>
        /// 处理 ,2-“边界”，4-“迷雾边界”
        /// </summary>
        CellType[,] ColorToData(Color32[] pixels)
        {
            CellType[,] result = new CellType[_rectH, _rectW]; // 200 * 200 
            var min_1th = new Color32(120, 120, 150, 255);
            var max_1th = new Color32(145, 145, 195, 255);
            var min_2th = new Color32(86, 88, 90, 255);
            var max_2th = max_1th;
            var min_3th = new Color32(0, 120, 135, 255);
            var max_3th = new Color32(85, 140, 195, 255);
            var B_to_G = new Vector2Int(30, 60);


            var first_list = new List<Vector2Int>();

            // DU.RunWithTimer(() =>
            //     {
            for (int i = 0; i < _rectH; i++)
            {
                for (int j = 0; j < _rectW; j++)
                {
                    int index = i * _rectW + j;
                    var color = pixels[index];

                    // if (!InEllipse(j - _rectW / 2, i - _rectH / 2))
                    //     continue;

                    if (Between(color, min_1th, max_1th)
                        && color.b - color.g >= B_to_G.x && color.b - color.g <= B_to_G.y)
                    {
                        first_list.Add(new Vector2Int(j, i));
                        result[j, i] = CellType.NewObstacleEdge;
                    }
                    else if (Between(color, min_3th, max_3th))
                    {
                        // "迷雾" 优先级高于"候选边界"
                        result[j, i] = CellType.Fog;
                    }
                    else if (Between(color, min_2th, max_2th))
                    {
                        // "候选边界"
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

            var count = 0;
            foreach (var pos in first_list)
            {
                _stack[count++] = pos;
            }

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;
                if (colorData[x, y] == CellType.Temp)
                {
                    result.Add(new Vector2Int(x, y));
                    colorData[x, y] = CellType.NewObstacleEdge; // 标记为"边界"
                }

                if (y > 0 && colorData[x, y - 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x, y - 1);
                if (x > 0 && colorData[x - 1, y] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x - 1, y);
                if (y < y_end && colorData[x, y + 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x, y + 1);
                if (x < x_end && colorData[x + 1, y] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x + 1, y);
                if (y > 0 && x > 0 && colorData[x - 1, y - 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x - 1, y - 1);
                if (y < y_end && x > 0 && colorData[x - 1, y + 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x - 1, y + 1);
                if (y < y_end && x < x_end && colorData[x + 1, y + 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x + 1, y + 1);
                if (y > 0 && x < x_end && colorData[x + 1, y - 1] == CellType.Temp)
                    _stack[count++] = new Vector2Int(x + 1, y - 1);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        bool InEllipse(int x, int y)
        {
            var result = x * x * ellipseRadiusSquare.y + y * y * ellipseRadiusSquare.x <= ellipseRadiusSquare.z;
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


            // debug
            // using (Mat flipped = new Mat())
            // {
            //     // 使用 Cv2.Flip 方法，参数 0 表示上下翻转
            //     Cv2.Flip(source, flipped, 0);
            //     var save_path = Application.streamingAssetsPath + $"/SmallMapDebug/pic_{debug_count}_{length}.png";
            //     IU.SaveMat(flipped, save_path);
            // }
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
            // DU.RunWithTimer(() =>
            // {
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
                    if (!InEllipse(j - x_start - _rectW / 2, i - y_start - _rectH / 2))
                        continue;

                    var data = small_map[j - x_start, i - y_start];
                    if (_map[j, i] != CellType.ObstacleEdge && _map[j, i] != CellType.NewObstacleEdge)
                        _map[j, i] = data;
                    // if (_map[j, i] == CellType.Undefined)
                    //     _map[j, i] = data;
                }

            // 因为_gridData依赖map1，所以先更新 map1，再更新 _gridData
            // 另外只选取 small_map(200X200) 中间的150X150区域更新，这部分的边界确保正常封口了。
            ApplyMap1(start);

            _gridData.Apply(start + new Vector2Int(25, 25), new Vector2Int(150, 150));

            // }, "Apply");
            Debug_SmallMap_Start = start;
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

                var x = (_mapEdge - x_len) / 2;         //理想的重建位置
                var y = (_mapEdge - y_len) / 2;
                var start = new Vector2Int(x, y);
                offset = new Vector2Int(start.x - _xRange.x, start.y - _yRange.x);
                offset = new Vector2Int(_gridData.GetDivisibleInt(offset.x), _gridData.GetDivisibleInt(offset.y));


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

        public void ApplyMap1(Vector2Int start)
        {

            // _map地块分类, 计算障碍
            // 从中心点出发，四方遍历刷格子
            for (int y = 0; y < 150; y++)
                for (int x = 0; x < 150; x++)
                {
                    _mapT[x, y] = CellType.ObstacleFill;
                }

            var x_start = start.x + 25;
            var y_start = start.y + 25;
            var x_end = x_start + 150 - 1;
            var y_end = y_start + 150 - 1;

            var count = 0;
            // 栈初始有中心3X3的空地。
            //
            var center = new Vector2Int(start.x + 100, start.y + 99);
            for (int i = -2; i <= 2; i++)
                for (int j = -2; j <= 2; j++)
                {
                    var pos = new Vector2Int(center.x + j, center.y + i);
                    if (_map[pos.x, pos.y] == CellType.Empty)
                        _stack[count++] = pos;
                }

            if (count == 0) Debug_SmallMap_Player_In_Obstacle++;

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;

                if (y > y_start && _map[x, y - 1] == CellType.Empty && _mapT[x - x_start, y - y_start - 1] != CellType.Empty)
                {
                    _mapT[x - x_start, y - y_start - 1] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x, y - 1);
                }
                if (x > x_start && _map[x - 1, y] == CellType.Empty && _mapT[x - x_start - 1, y - y_start] != CellType.Empty)
                {
                    _mapT[x - x_start - 1, y - y_start] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x - 1, y);
                }
                if (y < y_end && _map[x, y + 1] == CellType.Empty && _mapT[x - x_start, y - y_start + 1] != CellType.Empty)
                {
                    _mapT[x - x_start, y - y_start + 1] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x, y + 1);
                }
                if (x < x_end && _map[x + 1, y] == CellType.Empty && _mapT[x - x_start + 1, y - y_start] != CellType.Empty)
                {
                    _mapT[x - x_start + 1, y - y_start] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x + 1, y);
                }
            }

            var grid = _gridData._grid;
            var list_obstacleFill_to_empty = new List<Vector2Int>();
            for (int y = 0; y < 150; y++)
                for (int x = 0; x < 150; x++)
                {
                    var px = x + x_start;
                    var py = y + y_start;
                    var data0 = _map[px, py];
                    if (data0 == CellType.Undefined)
                        continue;

                    var cx = px / 5;
                    var cy = py / 5;
                    var cell = grid[cx, cy];

                    // "障碍边界"   第1优先级
                    // "大格子障碍" 第2
                    // "空地"       第3
                    // "障碍填充"   第4
                    // "Undefined"  第5

                    // _map1 只含Undefined、ObstacleEdge、ObstacleFill、ObstacleByBig、Empty
                    // 最初全是Undefined。然后听_map的写入ObstacleEdge 。听_grid的写入ObstacleByBig。
                    // 本部分内容： Undefined||ObstacleFill的像素 更新成 ObstacleFill 还是 Empty
                    if (data0 == CellType.NewObstacleEdge)
                    {
                        if (cell != null) cell.NeedRefresh = true;

                        _map[px, py] = CellType.ObstacleEdge;
                        _map1[px, py] = CellType.ObstacleEdge;
                    }
                    else if (_map1[px, py] == CellType.Undefined
                        || _map1[px, py] == CellType.ObstacleFill)
                    {
                        // "障碍填充" 转 "空地" 要通知大格子
                        if (cell != null && _map1[px, py] == CellType.ObstacleFill
                            && _mapT[x, y] == CellType.Empty)
                            list_obstacleFill_to_empty.Add(new Vector2Int(cx, cy));

                        _map1[px, py] = _mapT[x, y];
                    }
                }

            foreach (var cp in list_obstacleFill_to_empty)
            {
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        var cell = grid[cp.x + j, cp.y + i];
                        if (cell != null)
                        {
                            if (!cell.NeedRefresh) _gridData.ObstacleFill2EmptyCount++;
                            cell.NeedRefresh = true;
                        }
                    }
            }
        }


        #endregion

        #region 寻路

        public void StartAStarBigGrid(Vector2Int start, Vector2Int target)
        {
            var offset = new Vector2Int(_xRange.x, _yRange.x);
            start = start + offset;
            target = target + offset;
            _gridData.StartAStar(start, target);
        }



        #endregion

        #region Save

        public void Save(string path)
        {
            Color32[] colors = GetImageMap0();
            byte[] bytes = IU.Color32ToByte(colors);
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                IU.SaveMat(mat, path);
            }


            colors = GetImageMap1();
            bytes = IU.Color32ToByte(colors);
            var path1 = path.Substring(0, path.Length - 4) + "_1.png";
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                IU.SaveMat(mat, path1);
            }

            colors = GetImageGrid();
            bytes = IU.Color32ToByte(colors);
            var path2 = path.Substring(0, path.Length - 4) + "_grid.png";
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                IU.SaveMat(mat, path2);
            }
            PrintResult();
        }

        public void PrintResult()
        {
            var print = "";
            if (Debug_SmallMap_Player_In_Obstacle > 0)
            {
                print += $"[error] 进入障碍{Debug_SmallMap_Player_In_Obstacle}次  ";
            }

            print += $"大格子重建 {_gridData.RebuildCellCount}次  多侧空地的大格子 {_gridData.MultiEmptyCellCount}个  ";

            if (_gridData.ObstacleFill2EmptyCount > 0)
            {
                print += $"障碍填充转空地 {_gridData.ObstacleFill2EmptyCount}个 ";
            }

            DU.LogWarning(print);
        }


        public Color32[] GetImageMap0()
        {
            Color32[] bytes = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _map[x + _xRange.x, y + _yRange.x];
                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次
                    Color32 color = gray;
                    if (pix == CellType.ObstacleEdge || pix == CellType.NewObstacleEdge)
                        color = new Color32(240, 240, 240, 255);
                    if (pix == CellType.Fog) color = new Color32(0, 0, 255, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);

                    bytes[index] = color;
                }

            return bytes;
        }
        public Color32[] GetImageMap1()
        {
            Color32[] bytes = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _map1[x + _xRange.x, y + _yRange.x];
                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次  

                    Color32 color = gray;
                    if (pix == CellType.ObstacleEdge) color = new Color32(240, 240, 240, 255);
                    if (pix == CellType.ObstacleFill) color = new Color32(210, 210, 210, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);
                    if (pix == CellType.ObstacleByBig) color = new Color32(240, 140, 0, 255);

                    bytes[index] = color;
                }

            var player_pos = new Vector2Int(Debug_SmallMap_Start.x - _xRange.x + 100
                , Debug_SmallMap_Start.y - _yRange.x + 99);

            var player_block_list = new Vector2Int[]
            {
                new Vector2Int(0,0),
                new Vector2Int(-1,-1),
                new Vector2Int(-1,1),
                new Vector2Int(1,1),
                new Vector2Int(1,-1),
            };
            foreach (var _ in player_block_list)
            {
                var pos = new Vector2Int(player_pos.x + _.x, player_pos.y + _.y);
                bytes[(_h - 1 - pos.y) * _w + pos.x] = new Color32(0, 255, 0, 255);
            }

            return bytes;
        }
        public Color32[] GetImageMap1MapT()
        {
            Color32[] bytes = new Color32[150 * 150];
            for (int y = 0; y < 150; y++)
                for (int x = 0; x < 150; x++)
                {
                    var pix = _mapT[x, y];
                    int index = (150 - 1 - y) * 150 + x;                // 反转y轴，因为之前翻转过一次  

                    Color32 color = gray;
                    if (pix == CellType.ObstacleFill) color = new Color32(210, 210, 210, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);
                    bytes[index] = color;
                }


            if (moveRecord.Count > 0)
            {
                var move = moveRecord[moveRecord.Count - 1];
                if (move.x > 0)
                {
                    var start = 150 - move.x;
                    for (int y = 0; y < 150; y++)
                        for (int x = start; x < 150; x++)
                            Set1(bytes, x, y);
                }
                else
                {
                    var end = -move.x;
                    for (int y = 0; y < 150; y++)
                        for (int x = 0; x < end; x++)
                            Set1(bytes, x, y);
                }

                if (move.y > 0)
                {
                    var start = 150 - move.y;
                    for (int y = start; y < 150; y++)
                        for (int x = 0; x < 150; x++)
                            Set1(bytes, x, y);
                }
                else
                {
                    var end = -move.y;
                    for (int y = 0; y < end; y++)
                        for (int x = 0; x < 150; x++)
                            Set1(bytes, x, y);
                }

            }

            return bytes;
        }        public void Set1(Color32[] bytes, int x, int y)
        {
            int index = (150 - 1 - y) * 150 + x;
            Color32 color = bytes[index];
            var r = new Color32((byte)Math.Clamp(color.a * 2, 0, 255), 0, 0, 255);
            bytes[index] = r;
        }


        public Color32[] GetImageGrid()
        {
            Color32[] colors = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var p_x = x + _xRange.x;
                    var p_y = y + _yRange.x;
                    var pix = _map1[p_x, p_y];

                    Color32 color = gray;
                    if (pix == CellType.ObstacleEdge) color = new Color32(240, 240, 240, 255);
                    if (pix == CellType.ObstacleFill) color = new Color32(210, 210, 210, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);
                    if (pix == CellType.ObstacleByBig) color = new Color32(240, 140, 0, 255);


                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次    
                    colors[index] = color;
                }

            int cx_start = _xRange.x / 5;
            int cx_end = cx_start + (int)Mathf.Ceil((float)_w / 5);
            int cy_start = _yRange.x / 5;
            int cy_end = cy_start + (int)Mathf.Ceil((float)_h / 5);

            Color32 green0 = new Color32(20, 120, 20, 255);
            Color32 green1 = new Color32(170, 210, 170, 255);

            for (int cy = cy_start; cy < cy_end; cy++)
                for (int cx = cx_start; cx < cx_end; cx++)
                {

                    var cell = _gridData._grid[cx, cy];
                    if (cell == null)
                        continue;

                    int px_start = cx * 5;
                    int px_end = px_start + 5;
                    int py_start = cy * 5;
                    int py_end = py_start + 5;
                    if (cell.Direction == 0b1111_1111)      // 全方向
                    {
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (_map1[px, py] == CellType.Empty)
                                    colors[GetColorsIndex(px, py)] = new Color32(20, 60, 20, 255);    //绿
                            }
                    }
                    else if (cell.Direction == 0)           // 无方向
                    {
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (_map1[px, py] == CellType.Empty)
                                    colors[GetColorsIndex(px, py)] = new Color32(160, 0, 0, 255);    //红
                            }

                    }
                    else                                    // 有任意方向
                    {
                        // 咱们不涂黄了, 改为把"方向通路"地块涂绿。
                        byte direction = cell.Direction;
                        if ((direction & (1 << 7)) != 0)
                            colors[GetColorsIndex(cx * 5 + 2, cy * 5)] = _map1[cx * 5 + 2, cy * 5] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 1)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5 + 2)] = _map1[cx * 5, cy * 5 + 2] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 3)) != 0)
                            colors[GetColorsIndex(cx * 5 + 2, cy * 5 + 4)] = _map1[cx * 5 + 2, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 5)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5 + 2)] = _map1[cx * 5 + 4, cy * 5 + 2] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 0)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5)] = _map1[cx * 5, cy * 5] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 2)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5 + 4)] = _map1[cx * 5, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 4)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5 + 4)] = _map1[cx * 5 + 4, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 6)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5)] = _map1[cx * 5 + 4, cy * 5] == CellType.Empty ? green0 : green1;

                    }
                }


            return colors;
        }

        public int GetColorsIndex(int x, int y)
        {
            return (_h - 1 - (y - _yRange.x)) * _w + (x - _xRange.x);
        }

        public Color32[] GetImageGridAStart()
        {
            Color32[] colors = GetImageGrid();
            var target_c = _gridData._target_c;

            var grid = _gridData._grid;
            var cell = grid[target_c.x, target_c.y];
            var next_c = cell.ParentPos;

            //两格之间的寻路, 以目标像素点为最开始的起点。
            SmallCellFinder finder = new SmallCellFinder();
            Vector2Int cell_find_start = _gridData._target_p;
            Vector2Int cell_find_end = default;
            Color32 green = new Color32(20, 255, 20, 255);

            // 循环粒度：从cell_find_start走到 当前cell与next_cell的边界位置
            while (next_c != new Vector2Int(-1, -1))
            {
                var next_cell = grid[next_c.x, next_c.y];
                var cx = cell.x;
                var cy = cell.y;
                // 终点在哪里
                var map = GetByRegion(new Vector2Int(cx * 5, cy * 5), new Vector2Int(5, 5));
                var next_zero_pos = new Vector2Int(next_cell.x * 5, next_cell.y * 5);
                var next_map = GetByRegion(next_zero_pos, new Vector2Int(5, 5));
                cell_find_end = GridData.GetConnectPixel(map, next_map, new Vector2Int(cx, cy)
                , new Vector2Int(next_cell.x, next_cell.y)) + next_zero_pos;

                var find_zero_pos = new Vector2Int(cx * 5 - 1, cy * 5 - 1);
                var find_map = GetByRegion(find_zero_pos, new Vector2Int(7, 7));
                var result = finder.BeginAStar(find_map, cell_find_start - find_zero_pos, cell_find_end - find_zero_pos);

                // 涂颜色
                foreach (var _ in result)
                {
                    var pos = _ + find_zero_pos;
                    int index = GetColorsIndex(pos.x, pos.y);
                    colors[index] = green;
                }


                cell = next_cell;
                next_c = cell.ParentPos;

                //下个起点在哪里
                cell_find_start = cell_find_end;
            }



            return colors;
        }

        public bool[,] GetByRegion(Vector2Int from, Vector2Int size)
        {
            bool[,] result = new bool[size.x, size.y];
            int x_end = from.x + size.x;
            int y_end = from.y + size.y;
            for (int y = from.y; y < y_end; y++)
                for (int x = from.x; x < x_end; x++)
                    result[x - from.x, y - from.y] = _map1[x, y] == CellType.Empty;
            return result;
        }


        public void SaveAStar(string path)
        {
            var target_pos = _gridData._target_c;
            if (target_pos == new Vector2Int(-1, -1))
                return;


            var colors = GetImageGridAStart();
            var bytes = IU.Color32ToByte(colors);
            var path3 = path.Substring(0, path.Length - 4) + "_grid_AStar.png";
            using (Mat mat = Mat.FromPixelData(_h, _w, MatType.CV_8UC3, bytes))
            {
                Cv2.ImWrite(path3, mat);
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
}