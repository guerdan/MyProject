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
        Undefined = 0,          // 默认，此块不会参与功能。          灰色
        Empty = 1,              // 空地。                          黑色
        EmptyTemp = 2,          // 候选空地。                   
        ObstacleEdge = 3,       // 障碍边界                        白色
        ObstacleEdgeTemp = 4,   // 候选障碍边界
        ObstacleByBig = 5,      // 大格子标定的障碍（临时障碍）       橙色
        Fog = 6,                // 迷雾边界                         蓝色
        FogArea = 7,            // 迷雾区域，为迷雾边界延伸2格范围
    }


    /// <summary>
    /// 地图数据
    /// 从截屏Bitmap中截取小地图开始
    /// 左下角为(0,0)，涉及到模版匹配时，坐标系转换到左上角为(0,0)
    /// </summary>
    public class MapData
    {
        readonly Color32 grayColor = new Color32(80, 80, 80, 255);      // 灰色,代表CellType.Undefined
        readonly int mapInitialEdge = 200;      // 初始地图边长
        readonly int mapDistanceThreshold = 10; // 移动阈值，距边10  这样不用判断map取值出界
        readonly int mapSizeThreshold = 50;     // 扩容阈值，边长还差50
        readonly int mapExpandEdge = 400;       // 每次扩容增加的长度

        readonly int sourceEdge = 150;          // 源图边长
        readonly int templateEdge = 100;        // 模版图边长

        public List<Vector2Int> moveRecord = new List<Vector2Int>();

        // 地图的总数据。 x轴向右，y轴向上。
        // Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了
        public CellType[,] _map;            // 只有Capture可写，其他方法可读
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

        CellType[,] _small_map = new CellType[200, 200];    //本帧小地图
        public JudgeCell[,] _judge_map = new JudgeCell[300, 300];  //本帧的判定地图
        Vector2Int _templatePos;            // 模版图左下角坐标
        public Vector2Int _judgePos;               // 判定图左下角坐标

        // Vector2Int ellipseRadius;           // 椭圆轴半径
        // Vector3Int ellipseRadiusSquare;     // 椭圆轴半径平方,平方积

        Vector2Int[] _stack = new Vector2Int[40000];        //方法内部复用
        CellType[,] _last_small_map;    //上帧小地图


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
            _templatePos = new Vector2Int((_mapEdge - 200) / 2, (_mapEdge - 200) / 2);   // 右上角就是(599,599)
            _judgePos = _templatePos + new Vector2Int(-50, -50);

            if (_gridData == null)
                _gridData = new GridData(this);

            // ellipseRadius = new Vector2Int(48, 38);
            // ellipseRadiusSquare = new Vector3Int(ellipseRadius.x * ellipseRadius.x, ellipseRadius.y * ellipseRadius.y, 0);
            // ellipseRadiusSquare = new Vector3Int(ellipseRadiusSquare.x, ellipseRadiusSquare.y
            //     , ellipseRadiusSquare.x * ellipseRadiusSquare.y);
        }


        #region Capture

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
                _small_map = ColorToData(pixels);

                bool is_first = _template == null;
                if (is_first)
                {
                    InitEmpty(_small_map, true, default);
                    Apply(_small_map, _templatePos);
                }
                else
                {
                    // 源图，计算位移
                    using (Mat source = GetMat(_small_map, sourceEdge))// 指针指向像素数组，no copy
                    {
                        // 匹配
                        using (Mat result = IU.MatchTemplate1(source, _template))
                        {

                            var list = IU.FindResult(result, templateEdge, templateEdge, 0.1f, out _);
                            list.Sort((a, b) => b.Score.CompareTo(a.Score));
                            var score = list.Count > 0 ? list[0].Score : 0;
                            // DU.LogWarning($"[Capture FindResult] {list.Count}个  {score}分");
                            if (list.Count == 0)
                                return;
                            var rect = list[0].Rect;

                            // IU.PrintScore(result, "SmallMap Match");

                            //计算位移  动了是(5, 8)    不动是(17,17)。最终就是匹配图像走了(-12,-9),人走了(12,9)
                            int offset = (sourceEdge - templateEdge) / 2;
                            var delta = new Vector2Int(offset - rect.x, offset - rect.y);
                            moveRecord.Add(delta);
                            _templatePos += delta;
                            InitEmpty(_small_map, false, delta);
                        }
                        _template.Dispose();  //用完了就释放
                        _template = null;
                        Apply(_small_map, _templatePos);

                    }
                }

                // 模版图
                _template = GetMat(_small_map, templateEdge);
                _last_small_map = _small_map;
            }, "MapData.Capture");
        }



        /// <summary>
        /// 处理 ,2-“边界”，4-“迷雾边界”
        /// </summary>
        CellType[,] ColorToData(Color32[] pixels)
        {

            // 第2版  颜色偏蓝   
            //
            // 1类边界
            var min_1th = new Color32(110, 110, 150, 255);
            var max_1th = new Color32(145, 145, 195, 255);
            var B_to_G_1th = new Vector2Int(30, 60);
            // 2类边界
            var min_2th = new Color32(86, 88, 90, 255);
            var max_2th = max_1th;
            // 1类迷雾
            var min_3th = new Color32(0, 130, 170, 255);
            var max_3th = new Color32(90, 142, 192, 255);
            var B_to_G_3th = new Vector2Int(38, 55);


            CellType[,] colorData = new CellType[_rectW + 4, _rectH + 4]; // 200 * 200 

            var x_start = 2;
            var x_end = _rectW + 2;
            var y_start = 2;
            var y_end = _rectH + 2;

            var first_list = new List<Vector2Int>();
            var fog_constant = new Vector2Int[]{
                new Vector2Int(-2,0),new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(2,0),
                new Vector2Int(-1,1),new Vector2Int(0,1),new Vector2Int(1,1),
                new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1),
                new Vector2Int(0,2),new Vector2Int(0,-2),
            };

            // DU.RunWithTimer(() =>
            //     {

            // 先处理迷雾
            for (int i = y_start; i < y_end; i++)
                for (int j = x_start; j < x_end; j++)
                {
                    int index = (i - 2) * _rectW + j - 2;
                    var color = pixels[index];
                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;

                    if (r == 0 && g == 0 && b == 0) // 文字描边
                        colorData[j, i] = CellType.Undefined;

                    else if (r < 50 && g < 50 && b < 50) // 空地
                        colorData[j, i] = CellType.EmptyTemp;

                    else if (Between(color, min_3th, max_3th)
                        && b - g >= B_to_G_3th.x && b - g <= B_to_G_3th.y)
                    {
                        colorData[j, i] = CellType.Fog;

                        foreach (var offset in fog_constant)
                        {
                            int px = j + offset.x;
                            int py = i + offset.y;
                            var data = colorData[px, py];
                            if (data != CellType.Fog)
                                colorData[px, py] = CellType.FogArea;
                        }
                    }
                }

            // 再处理边界
            for (int i = y_start; i < y_end; i++)
                for (int j = x_start; j < x_end; j++)
                {
                    int index = (i - 2) * _rectW + j - 2;
                    var color = pixels[index];
                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;
                    var data = colorData[j, i];
                    if (data != CellType.Undefined)
                        continue;

                    if (Between(color, min_1th, max_1th)
                        && b - g >= B_to_G_1th.x && b - g <= B_to_G_1th.y)
                    {
                        first_list.Add(new Vector2Int(j, i));
                        colorData[j, i] = CellType.ObstacleEdge;
                    }
                    else if (Between(color, min_2th, max_2th))
                    {
                        colorData[j, i] = CellType.ObstacleEdgeTemp;
                    }
                }


            // }, "Condition check");

            ColorToDataTraversal(colorData, first_list);


            var temp = new CellType[_rectW, _rectH];
            for (int i = y_start; i < y_end; i++)
                for (int j = x_start; j < x_end; j++)
                    if (colorData[j, i] != CellType.ObstacleEdgeTemp)
                        temp[j - 2, i - 2] = colorData[j, i];

            colorData = temp;

            return colorData;
        }


        /// <summary>
        /// 遍历
        /// 递归,发散型递归。感觉用栈可以实现非递归
        /// 猜想优化点：将colorData扩展为[w+1,h+1]，边缘设置为0，这样就不用每次都判断边界了
        /// </summary>
        void ColorToDataTraversal(CellType[,] colorData, List<Vector2Int> first_list)
        {
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
                if (colorData[x, y] == CellType.ObstacleEdgeTemp)
                {
                    colorData[x, y] = CellType.ObstacleEdge; // 标记为"边界"
                }

                foreach (var offset in Utils.SurroundList)
                {
                    int px = offset.x + x;
                    int py = offset.y + y;
                    if (colorData[px, py] == CellType.ObstacleEdgeTemp)
                    {
                        colorData[px, py] = CellType.ObstacleEdge;
                        _stack[count++] = new Vector2Int(px, py);
                    }

                }
            }

        }

        #region InitEmpty

        /// <summary>
        /// 测试过没问题。用上一帧的图盖掉角色图标。
        /// </summary>
        void InitEmpty(CellType[,] colorData, bool is_first, Vector2Int move_offset)
        {
            // 在这里处理 空地遍历  EmptyTemp => Empty
            int xs = 98, xe = xs + 5, ys = 97, ye = ys + 5;

            if (is_first)
            {
                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        colorData[x, y] = CellType.EmptyTemp;
                    }

                ColorToDataTraversalEmpty(colorData, new List<Vector2Int>() { new Vector2Int(101, 100) });
            }
            else
            {
                var list = new List<Vector2Int>();
                // 根据偏移结果来
                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        // 用上一帧的中心图像
                        var data = _last_small_map[x + move_offset.x, y + move_offset.y];
                        colorData[x, y] = data;
                        if (data == CellType.Empty)
                            list.Add(new Vector2Int(x, y));
                    }

                ColorToDataTraversalEmpty(colorData, list);
            }

        }
        void ColorToDataTraversalEmpty(CellType[,] colorData, List<Vector2Int> first_list)
        {
            var x_end = _rectW - 1;
            var y_end = _rectH - 1;
            var count = 0;

            foreach (var pos in first_list)
            {
                _stack[count++] = pos;
                colorData[pos.x, pos.y] = CellType.Empty;
            }

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;


                if (y > 0 && colorData[x, y - 1] == CellType.EmptyTemp)
                {
                    colorData[x, y - 1] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x, y - 1);
                }
                if (x > 0 && colorData[x - 1, y] == CellType.EmptyTemp)
                {
                    colorData[x - 1, y] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x - 1, y);
                }
                if (y < y_end && colorData[x, y + 1] == CellType.EmptyTemp)
                {
                    colorData[x, y + 1] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x, y + 1);
                }
                if (x < x_end && colorData[x + 1, y] == CellType.EmptyTemp)
                {
                    colorData[x + 1, y] = CellType.Empty;
                    _stack[count++] = new Vector2Int(x + 1, y);
                }
            }

        }

        #endregion

        // [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        // bool InEllipse(int x, int y)
        // {
        //     var result = x * x * ellipseRadiusSquare.y + y * y * ellipseRadiusSquare.x <= ellipseRadiusSquare.z;
        //     return result;
        // }
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
                    if (map_data == CellType.ObstacleEdge)
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

        #endregion



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
            var refreshBigCell = ApplyJudgeMap(small_map);
            _gridData.Apply(refreshBigCell);

            // }, "Apply");


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
                    }
                _map = newMap;

                _templatePos += offset;
                _judgePos += offset;
                //改xRange
                _xRange = _xRange + new Vector2Int(offset.x, offset.x);
                _yRange = _yRange + new Vector2Int(offset.y, offset.y);

                // offset一定能整除_gridData._scale
                _gridData.Rebuild(old_edge, _mapEdge, offset);
            }

        }

        #endregion

        #region judge_map
        List<BigCell> ApplyJudgeMap(CellType[,] small_map)
        {
            var refreshBigCell = new List<BigCell>();

            int s_len = 158;
            int j_len = 300;
            int delta_len = j_len - s_len;


            // 超界居中策略，重建次数平衡，另外5帧位移不超过50px
            if (_templatePos.x - _judgePos.x > delta_len || _templatePos.x - _judgePos.x < 0
            || _templatePos.y - _judgePos.y > delta_len || _templatePos.y - _judgePos.y < 0)
            {
                var old = _judgePos;
                _judgePos = _templatePos - new Vector2Int(delta_len / 2, delta_len / 2);
                var off = _judgePos - old;                  //offset

                var jLen = _judge_map.GetLength(0);

                var temp = new JudgeCell[jLen, jLen];
                var xs = Mathf.Max(off.x, 0);               //x_start
                var xe = Mathf.Min(off.x + jLen, jLen);     //x_end
                var ys = Mathf.Max(off.y, 0);               //y_start
                var ye = Mathf.Min(off.y + jLen, jLen);     //y_end
                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        temp[x - off.x, y - off.y] = _judge_map[x, y];
                    }
                _judge_map = temp;
            }

            Vector2Int offset = _templatePos - _judgePos;


            {
                //  将中间的[158X158]区域写入
                //
                int xs = 21, xe = 159, ys = 21, ye = 159;

                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        var jx = x + offset.x;
                        var jy = y + offset.y;

                        var sData = small_map[x, y];
                        if (sData == CellType.Fog || sData == CellType.FogArea)
                        {
                            _map[x + _templatePos.x, y + _templatePos.y] = CellType.Fog;
                            _judge_map[jx, jy] = default;
                            continue;
                        }


                        var px = x + _templatePos.x;
                        var py = y + _templatePos.y;
                        var mData = _map[px, py];
                        // "空地""边界""大格子障碍"是明确状态，"Undefined"和"迷雾"是待定状态可以往下走
                        //
                        if (mData != CellType.Undefined && mData != CellType.Fog)
                            continue;

                        if (mData == CellType.Fog)
                            _map[px, py] = CellType.Undefined;


                        var jData = _judge_map[jx, jy];

                        // 如果 空地和边界未开始计数，则Undefined无效
                        if (sData == CellType.Undefined && jData.empty_num == 0
                            && jData.obstacle_edge_num == 0)
                            continue;

                        if (sData == CellType.Undefined)
                            jData.undefined_num++;
                        else if (sData == CellType.Empty)
                            jData.empty_num++;
                        else if (sData == CellType.ObstacleEdge)
                            jData.obstacle_edge_num++;


                        if (jData.CheckFull(out CellType result))
                        {
                            _judge_map[jx, jy] = default;
                            _map[px, py] = result;

                            if (result == CellType.Empty)
                            {
                                var cx = px / 5;
                                var cy = py / 5;
                                var cell = _gridData._grid[cx, cy];
                                if (cell == null)
                                {
                                    cell = new BigCell(cx, cy);
                                    _gridData._grid[cx, cy] = cell;
                                }
                                if (!cell.NeedRefresh)
                                {
                                    cell.NeedRefresh = true;
                                    refreshBigCell.Add(cell);
                                }
                            }

                        }
                        else
                        {
                            _judge_map[jx, jy] = jData;
                        }

                    }
            }
            return refreshBigCell;
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

            int big_cell_active = _gridData.GetActiveCount();
            int big_cell_total = _gridData.BuildCellTimes;
            int big_cell_multi = _gridData.MultiEmptyCellCount;

            print += $"大格子重建次数 {big_cell_total - big_cell_active}  多侧空地的大格子 {big_cell_multi}个  构造次数 {big_cell_total}";

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
                    Color32 color = grayColor;
                    if (pix == CellType.ObstacleEdge) color = new Color32(240, 240, 240, 255);
                    if (pix == CellType.Fog) color = new Color32(0, 0, 255, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);
                    if (pix == CellType.ObstacleByBig) color = new Color32(240, 140, 0, 255);

                    bytes[index] = color;
                }

            return bytes;
        }

        public Color32[] GetImageSmallMap()
        {
            var len = _small_map.GetLength(0);
            Color32[] bytes = new Color32[len * len];
            for (int y = 0; y < len; y++)
                for (int x = 0; x < len; x++)
                {
                    var pix = _small_map[x, y];
                    int index = (len - 1 - y) * len + x;                // 反转y轴，因为之前翻转过一次  

                    Color32 color = grayColor;
                    if (pix == CellType.ObstacleEdge)
                        color = new Color32(240, 240, 240, 255);
                    if (pix == CellType.Fog) color = new Color32(0, 0, 255, 255);
                    if (pix == CellType.Empty) color = new Color32(20, 20, 20, 255);

                    bytes[index] = color;
                }


            return bytes;
        }

        public Color32[] GetImageJudgeMap()
        {

            Color32[] bytes = new Color32[_w * _h];

            var jLen = _judge_map.GetLength(0);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var px = x + _xRange.x;
                    var py = y + _yRange.x;
                    var pix = _map[px, py];

                    Color32 color = grayColor;
                    if (px >= _judgePos.x && px < _judgePos.x + jLen
                        && py >= _judgePos.y && py < _judgePos.y + jLen)
                    {
                        if (pix == CellType.Fog)
                            color = new Color32(0, 0, 255, 255);
                        else
                        {
                            var jData = _judge_map[px - _judgePos.x, py - _judgePos.y];
                            var r = jData.CheckResult();
                            if (r == CellType.ObstacleEdge)
                                color = new Color32(240, 240, 240, 255);
                            if (r == CellType.Empty)
                                color = new Color32(20, 20, 20, 255);
                        }
                    }

                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次  
                    bytes[index] = color;
                }

            List<Vector2Int> list = new List<Vector2Int>();

            // 红线
            // 
            for (int y = -1; y < jLen + 1; y++)
            {
                list.Add(new Vector2Int(-1, y));
                list.Add(new Vector2Int(jLen, y));
            }
            for (int x = -1; x < jLen + 1; x++)
            {
                list.Add(new Vector2Int(x, -1));
                list.Add(new Vector2Int(x, jLen));
            }

            foreach (var pos in list)
            {
                int px = pos.x + _judgePos.x - _xRange.x;
                int py = pos.y + _judgePos.y - _yRange.x;
                if (px >= 0 && px < _w && py >= 0 && py < _h)
                {
                    int index = (_h - 1 - py) * _w + px;
                    bytes[index] = new Color32(255, 0, 0, 255);
                }
            }

            return bytes;
        }


        public Color32[] GetImageGrid()
        {
            Color32[] colors = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var p_x = x + _xRange.x;
                    var p_y = y + _yRange.x;
                    var pix = _map[p_x, p_y];

                    Color32 color = grayColor;
                    if (pix == CellType.ObstacleEdge) color = new Color32(240, 240, 240, 255);
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
                                if (_map[px, py] == CellType.Empty)
                                    colors[GetColorsIndex(px, py)] = new Color32(20, 60, 20, 255);    //绿
                            }
                    }
                    else if (cell.Direction == 0)           // 无方向
                    {
                        for (int py = py_start; py < py_end; py++)
                            for (int px = px_start; px < px_end; px++)
                            {
                                if (_map[px, py] == CellType.Empty)
                                    colors[GetColorsIndex(px, py)] = new Color32(160, 0, 0, 255);    //红
                            }

                    }
                    else                                    // 有任意方向
                    {
                        // 咱们不涂黄了, 改为把"方向通路"地块涂绿。
                        byte direction = cell.Direction;
                        if ((direction & (1 << 7)) != 0)
                            colors[GetColorsIndex(cx * 5 + 2, cy * 5)] = _map[cx * 5 + 2, cy * 5] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 1)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5 + 2)] = _map[cx * 5, cy * 5 + 2] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 3)) != 0)
                            colors[GetColorsIndex(cx * 5 + 2, cy * 5 + 4)] = _map[cx * 5 + 2, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 5)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5 + 2)] = _map[cx * 5 + 4, cy * 5 + 2] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 0)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5)] = _map[cx * 5, cy * 5] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 2)) != 0)
                            colors[GetColorsIndex(cx * 5, cy * 5 + 4)] = _map[cx * 5, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 4)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5 + 4)] = _map[cx * 5 + 4, cy * 5 + 4] == CellType.Empty ? green0 : green1;

                        if ((direction & (1 << 6)) != 0)
                            colors[GetColorsIndex(cx * 5 + 4, cy * 5)] = _map[cx * 5 + 4, cy * 5] == CellType.Empty ? green0 : green1;

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
            Color32 yellow = new Color32(150, 150, 20, 255);

            int gw = grid.GetLength(0);
            int gh = grid.GetLength(1);
            for (int cy = 0; cy < gh; cy++)
                for (int cx = 0; cx < gw; cx++)
                {
                    var c = grid[cx, cy];
                    if (c == null) continue;
                    if (!c.Access) continue;
                    int pxs = cx * 5;
                    int pys = cy * 5;
                    int pxe = pxs + 5;
                    int pye = pys + 5;
                    for (int py = pys; py < pye; py++)
                        for (int px = pxs; px < pxe; px++)
                        {
                            if (_map[px, py] != CellType.Empty) continue;
                            int index = GetColorsIndex(px, py);
                            colors[index] = yellow;
                        }
                }

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
                    result[x - from.x, y - from.y] = _map[x, y] == CellType.Empty;
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

        #region JudgeCell

        public struct JudgeCell
        {
            public byte undefined_num;
            public byte empty_num;
            public byte obstacle_edge_num;

            public bool CheckFull(out CellType type)
            {
                type = CellType.Undefined;
                if (undefined_num + empty_num + obstacle_edge_num < 5)
                    return false;


                if (undefined_num >= empty_num && undefined_num >= obstacle_edge_num)
                    type = CellType.Undefined;
                else if (obstacle_edge_num >= empty_num)
                    type = CellType.ObstacleEdge;
                else
                    type = CellType.Empty;


                return true;
            }
            public CellType CheckResult()
            {
                CellType type;
                if (undefined_num >= empty_num && undefined_num >= obstacle_edge_num)
                    type = CellType.Undefined;
                else if (obstacle_edge_num >= empty_num)
                    type = CellType.ObstacleEdge;
                else
                    type = CellType.Empty;

                return type;
            }

            public static bool operator ==(JudgeCell left, JudgeCell right)
            {
                return left.undefined_num == right.undefined_num && left.empty_num == right.empty_num
                    && left.obstacle_edge_num == right.obstacle_edge_num;
            }

            public static bool operator !=(JudgeCell left, JudgeCell right)
            {
                return !(left == right);
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
}