using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        readonly int mapInitialEdge = 200;      // 初始地图边长
        readonly int mapDistanceThreshold = 10; // 移动阈值，距边10  这样不用判断map取值出界
        readonly int mapSizeThreshold = 50;     // 扩容阈值，边长还差50
        readonly int mapExpandEdge = 400;       // 每次扩容增加的长度

        readonly int sourceEdge = 150;          // 源图边长
        readonly int templateEdge = 100;        // 模版图边长，一帧最多走17像素，这里有25像素的空间。

        Vector2Int ellipseRadius;           // 椭圆轴半径
        Vector3Int ellipseRadiusSquare;     // 椭圆轴半径平方,平方积
        List<(Vector2Int, Vector2Int)> EdgeFogList;       // 边缘项，属于边缘迷雾需求，

        /// <summary>
        /// 与光照地图需求有关，也可以查看。
        /// 序越大越新，容量1000
        /// </summary>
        public List<Vector2Int> MoveRecord;
        public List<(int, float)> AccuracyRecord;      //匹配准确率记录
        public int FrameCount { get => _frameCount; }     // 当前帧数
        int _frameCount;

        /// <summary>
        /// 总地图，只含(Undef/Empty/ObstacleEdge/ObstacleByBig/Fog)。
        /// x轴向右，y轴向上。Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了.
        /// </summary>
        public CellType[,] _map;
        public LightType[,] _light_map;     // 总 光照地图，=视野刷过的区域。
        public bool[,] _confirm_map;        // 总 判定地图，false表示在判定系统中属于未定义、在寻路里属于确定
        public GridData _gridData;
        public int _mapEdge;                // 当前地图的容器边长
        Vector2Int _xRange;                 // 内容x轴范围
        Vector2Int _yRange;                 // 内容y轴范围
        int _w;                             // 内容宽  
        int _h;                             // 内容高


        CVRect _rect;                       // 小地图在屏幕中的位置 200 * 200
        int _rectW;                         // 小地图宽
        int _rectH;                         // 小地图高

        CellType[,] _small_map = new CellType[200, 200];            // 本帧小地图
        public JudgeCell[,] _judge_map = new JudgeCell[300, 300];   // 本帧的判定地图
        LightType[,] _light_cal_map = new LightType[200, 200];       // 用于运算的光照地图
        CellType[,] _last_small_map;                                // 上帧小地图
        Vector2Int _zeroPos;
        Vector2Int _templatePos;                                    // 模版图左下角坐标
        public Vector2Int _judgePos;                                // 判定图左下角坐标

        Dictionary<PixRecordType, List<Vector2Int>> _pixRecord;     // debug用的像素记录

        Vector2Int[] _light_range = new Vector2Int[2];              // 光照：5个移动总光照范围
        int _light_count;                                           // 光照：5个移动后触发光照地图检查


        public Vector2Int _find_start = Utils.DefaultV2I;           //寻路起点，像素粒度
        public Vector2Int _find_target = Utils.DefaultV2I;          //寻路最终目标，像素粒度
        public Vector2Int _find_aStar_target = Utils.DefaultV2I;    //实际目标，像素粒度。迷雾情况下会切换
        public bool _find_in_empty;

        Vector2Int[] _EdgeTraversal_list1 = new Vector2Int[800];        //方法内部复用
        Vector2Int[] _EdgeTraversal_list2 = new Vector2Int[800];        //方法内部复用

        Vector2Int[] _stack = new Vector2Int[40000];        //方法内部复用

        List<Vector2Int> TargetList;


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
            _light_map = new LightType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _confirm_map = new bool[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _templatePos = new Vector2Int((_mapEdge - 200) / 2, (_mapEdge - 200) / 2);   // 右上角就是(599,599)
            _zeroPos = new Vector2Int(_templatePos.x + 100, _templatePos.y + 100);
            _judgePos = _templatePos + new Vector2Int(-50, -50);

            if (_gridData == null)
                _gridData = new GridData(this);

            ellipseRadius = new Vector2Int(48, 38);
            ellipseRadiusSquare = new Vector3Int(ellipseRadius.x * ellipseRadius.x, ellipseRadius.y * ellipseRadius.y, 0);
            ellipseRadiusSquare = new Vector3Int(ellipseRadiusSquare.x, ellipseRadiusSquare.y
                , ellipseRadiusSquare.x * ellipseRadiusSquare.y);

            MoveRecord = new List<Vector2Int>();
            AccuracyRecord = new List<(int, float)>();
            _pixRecord = new Dictionary<PixRecordType, List<Vector2Int>>();
            _pixRecord[PixRecordType.ObstacleEdgeOfLight] = new List<Vector2Int>();
            _pixRecord[PixRecordType.LineOfFindPath] = new List<Vector2Int>();
            _pixRecord[PixRecordType.AreaOfFindNearestFog] = new List<Vector2Int>();

        }

        public Vector2Int GetTargetPos(int index)
        {
            if (TargetList == null)
            {
                TargetList = new List<Vector2Int>()
                {
                    new Vector2Int(-400, 200),
                };
            }
            return TargetList[index] + _zeroPos;
        }


        #region Capture

        public void Capture(Bitmap bitmap)
        {
            _frameCount++;
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

                bool is_first = _frameCount == 1;
                if (is_first)
                {
                    InitEmpty(_small_map, true, default);
                    Apply(_small_map, _templatePos);

                    ApplyAccuracyRecord(0);
                }
                else
                {
                    Vector2Int delta = default;
                    // DU.RunWithTimer(() =>
                    // {
                    // 源图，计算位移
                    using (Mat template = GetMat(_small_map, templateEdge))
                    using (Mat source = GetSourceMat(sourceEdge))
                    // 匹配结果
                    using (Mat result = IU.MatchTemplateCustomMask(source, template, template))
                    {
                        // debug
                        // if (_frameCount == 43)
                        // {
                        //        var a = 0;
                        // }

                        var list = IU.FindResultMin(result, templateEdge, templateEdge, 0.5f);
                        list.Sort((a, b) => b.Score.CompareTo(a.Score));
                        var score = list.Count > 0 ? list[0].Score : 0;
                        ApplyAccuracyRecord(score);

                        if (list.Count == 0)
                            return;
                        var rect = list[0].Rect;

                        //计算位移  动了是(5, 8)    不动是(17,17)。最终就是匹配图像走了(-12,-9),人走了(12,9)
                        int offset = (sourceEdge - templateEdge) / 2;
                        delta = new Vector2Int(rect.x - offset, rect.y - offset);
                        ApplyMoveRecord(delta);

                        _templatePos += delta;
                    }
                    // }, "MapData.TemplateMatch");

                    InitEmpty(_small_map, false, delta);
                    Apply(_small_map, _templatePos);

                }

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

                    else if (r < 50 && g < 50 && b < 50 && colorData[j, i] != CellType.FogArea) // 空地
                        colorData[j, i] = CellType.EmptyTemp;

                    else if (Between(color, min_3th, max_3th)
                        && b - g >= B_to_G_3th.x && b - g <= B_to_G_3th.y)
                    {
                        colorData[j, i] = CellType.Fog;

                        // 一次循环干了两次循环的事，所以上面要加colorData[j, i] != CellType.FogArea
                        //
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

                foreach (var offset in Utils.EightDirList)
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
                        source_bytes[i * length + j] = 255;
                }

            Mat source = Mat.FromPixelData(length, length, MatType.CV_8UC1, source_bytes);


            // debug
            // using (Mat flipped = new Mat())
            // {
            //     // 使用 Cv2.Flip 方法，参数 0 表示上下翻转
            //     Cv2.Flip(source, flipped, 0);
            //     var save_path = IU.PicPath + $"/SmallMapDebug/pic_{_frameCount}_{length}.png";
            //     IU.SaveMat(flipped, save_path);
            // }
            return source;
        }

        Mat GetSourceMat(int length)
        {
            int x_start = (_rectW - length) / 2 + _templatePos.x;
            int y_start = (_rectH - length) / 2 + _templatePos.y;

            byte[] source_bytes = new byte[length * length];

            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    var map_data = _map[j + x_start, i + y_start];
                    if (map_data == CellType.ObstacleEdge)
                        source_bytes[i * length + j] = 255;

                }

            Mat source = Mat.FromPixelData(length, length, MatType.CV_8UC1, source_bytes);


            // debug
            // using (Mat flipped = new Mat())
            // {
            //     // 使用 Cv2.Flip 方法，参数 0 表示上下翻转
            //     Cv2.Flip(source, flipped, 0);
            //     var save_path = IU.PicPath + $"/SmallMapDebug/pic_{_frameCount}_{length}.png";
            //     IU.SaveMat(flipped, save_path);
            // }

            return source;
        }



        void ApplyMoveRecord(Vector2Int delta)
        {
            MoveRecord.Add(delta);
            if (MoveRecord.Count >= 100)
            {
                MoveRecord = MoveRecord.GetRange(50, 50);
            }

        }
        void ApplyAccuracyRecord(float num)
        {
            AccuracyRecord.Add((_frameCount, num));
            if (MoveRecord.Count >= 500)
            {
                MoveRecord = MoveRecord.GetRange(100, 400);
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

            // 还原CellType.EmptyTemp
            //
            for (int y = 0; y < _rectH; y++)
                for (int x = 0; x < _rectW; x++)
                {
                    if (colorData[x, y] == CellType.EmptyTemp)
                        colorData[x, y] = CellType.Undefined;
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

            var refreshBigCell = ApplyJudgeMap(small_map);
            CheckLightMap();
            _gridData.Apply(refreshBigCell);

            // }, "Apply");


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

                CellType[,] new_map = new CellType[_mapEdge, _mapEdge];
                LightType[,] new_light_map = new LightType[_mapEdge, _mapEdge];
                bool[,] new_confirm_map = new bool[_mapEdge, _mapEdge];
                int old_edge = _map.GetLength(0);
                x_min = Math.Max(0, x_min);
                x_max = Math.Min(old_edge - 1, x_max);
                y_min = Math.Max(0, y_min);
                y_max = Math.Min(old_edge - 1, y_max);

                for (int i = y_min; i <= y_max; i++)
                    for (int j = x_min; j <= x_max; j++)
                    {
                        new_map[j + offset.x, i + offset.y] = _map[j, i];
                        new_light_map[j + offset.x, i + offset.y] = _light_map[j, i];
                        new_confirm_map[j + offset.x, i + offset.y] = _confirm_map[j, i];
                    }
                _map = new_map;
                _light_map = new_light_map;
                _confirm_map = new_confirm_map;

                ApplyOffset(offset);

                // offset一定能整除_gridData._scale
                _gridData.Rebuild(old_edge, _mapEdge, offset);
            }


            _w = _xRange.y - _xRange.x + 1;
            _h = _yRange.y - _yRange.x + 1;
        }

        void ApplyOffset(Vector2Int offset)
        {
            _zeroPos += offset;
            _templatePos += offset;
            _judgePos += offset;
            // 改xRange
            _xRange = _xRange + new Vector2Int(offset.x, offset.x);
            _yRange = _yRange + new Vector2Int(offset.y, offset.y);

            // 改Record
            foreach (var id in _pixRecord.Keys.ToList())
            {
                var list = _pixRecord[id];
                var new_list = new List<Vector2Int>();
                foreach (var p in list)
                {
                    new_list.Add(p + offset);
                }
                _pixRecord[id] = new_list;
            }

            _light_range[0] = _light_range[0] + offset;
            _light_range[1] = _light_range[1] + offset;
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
                var xs1 = Mathf.Max(off.x, 0);               //x_start
                var xe1 = Mathf.Min(off.x + jLen, jLen);     //x_end
                var ys1 = Mathf.Max(off.y, 0);               //y_start
                var ye1 = Mathf.Min(off.y + jLen, jLen);     //y_end
                for (int y = ys1; y < ye1; y++)
                    for (int x = xs1; x < xe1; x++)
                    {
                        temp[x - off.x, y - off.y] = _judge_map[x, y];
                    }
                _judge_map = temp;
            }

            Vector2Int offset = _templatePos - _judgePos;
            ApplyEdgeFog(small_map);   // 补充边缘迷雾。

            // 将中间的[158X158]区域写入。这部分是确定的
            int xs = 21, xe = 179, ys = 21, ye = 179;   // x_start/x_end/y_start/y_end

            // 主逻辑：[158X158]范围
            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var px = x + _templatePos.x;
                    var py = y + _templatePos.y;
                    var mData = _map[px, py];
                    if (InEllipse(x - 100, y - 100)) _light_map[px, py] = LightType.Light;

                    // "空地""边界""大格子障碍"是明确状态，"Undefined"和"迷雾"是待定状态可以往下走
                    //
                    bool confirm = _confirm_map[px, py];
                    if (confirm)
                        continue;

                    var sData = small_map[x, y];

                    var jx = x + offset.x;
                    var jy = y + offset.y;
                    var jData = _judge_map[jx, jy];
                    jData.Access = true;
                    _judge_map[jx, jy] = jData;

                    if (sData == CellType.Fog || sData == CellType.FogArea)
                    {
                        // 在亮地范围内，避免"地图图标"的蓝色被误认为迷雾。
                        if (_light_map[px, py] == LightType.Dark) _map[px, py] = CellType.Fog;

                        jData.undefined_num = 0; jData.empty_num = 0; jData.obstacle_edge_num = 0;
                        _judge_map[jx, jy] = jData;
                        continue;
                    }


                    if (mData == CellType.Fog)
                        _map[px, py] = CellType.Undefined;


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
                        jData.undefined_num = 0; jData.empty_num = 0; jData.obstacle_edge_num = 0;
                        _judge_map[jx, jy] = jData;
                        _map[px, py] = result;

                        if (result != CellType.Undefined)
                            _confirm_map[px, py] = true;

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
                        var ori_type = _judge_map[jx, jy].GuessType();
                        var new_type = jData.GuessType();
                        _judge_map[jx, jy] = jData;
                        _map[px, py] = new_type;
                        bool refresh = false;

                        if (ori_type != CellType.Empty && new_type == CellType.Empty)
                            refresh = true;
                        if (ori_type == CellType.Empty && new_type != CellType.Empty)
                            refresh = true;

                        if (refresh)
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

                }

            return refreshBigCell;
        }

        // 补充边缘迷雾
        // 补充迷雾：[160X160]的最外层厚度1px像素。若(与空地相连)&& ，则标为迷雾
        // _light_map ：视野刷子区域可能比 _map区域外扩1px
        void ApplyEdgeFog(CellType[,] small_map)
        {
            Vector2Int offset = _templatePos - _judgePos;

            if (EdgeFogList == null)
            {
                EdgeFogList = new List<(Vector2Int, Vector2Int)>();

                int xs = 21, xe = 179, ys = 21, ye = 179;
                for (int y = ys; y < ye; y++)
                {
                    EdgeFogList.Add((new Vector2Int(20, y), new Vector2Int(1, 0)));
                    EdgeFogList.Add((new Vector2Int(179, y), new Vector2Int(-1, 0)));

                }
                for (int x = xs; x < xe; x++)
                {
                    EdgeFogList.Add((new Vector2Int(x, 20), new Vector2Int(0, 1)));
                    EdgeFogList.Add((new Vector2Int(x, 179), new Vector2Int(0, -1)));
                }
            }

            foreach (var tuple in EdgeFogList)
            {
                var p = tuple.Item1;
                var relation = tuple.Item2;
                var mx = p.x + _templatePos.x;
                var my = p.y + _templatePos.y;
                var jx = p.x + offset.x;
                var jy = p.y + offset.y;
                var jData = _judge_map[jx, jy];
                var inside = small_map[p.x + relation.x, p.y + relation.y];
                // 本像素没访问过，small_map邻近像素是空地
                if ((inside == CellType.Empty || inside == CellType.ObstacleEdge) && !jData.Access)
                {
                    _map[mx, my] = CellType.Fog;
                    jData.Access = true;
                    _judge_map[jx, jy] = jData;
                }
            }

        }

        #endregion


        #region light_map
        void CheckLightMap()
        {
            _light_count++;
            if (_light_count < 5) return;
            _light_count = 0;

            var len = MoveRecord.Count;
            if (len < 9) return;

            var center = _templatePos + new Vector2Int(100, 100);
            int l_min_x = int.MaxValue;
            int l_max_x = 0;
            int l_min_y = int.MaxValue;
            int l_max_y = 0;
            Vector2Int[] center_list = new Vector2Int[5];

            for (int i = 0; i < 9; i++)
            {
                var move = MoveRecord[len - 1 - i];
                center = center - move;
                if (i >= 4)
                {
                    center_list[i - 4] = center;
                    l_min_x = Mathf.Min(l_min_x, center.x);
                    l_max_x = Mathf.Max(l_max_x, center.x);
                    l_min_y = Mathf.Min(l_min_y, center.y);
                    l_max_y = Mathf.Max(l_max_y, center.y);
                }

            }

            l_min_x -= ellipseRadius.x;
            l_max_x += ellipseRadius.x;
            l_min_y -= ellipseRadius.y;
            l_max_y += ellipseRadius.y;
            _light_range[0] = new Vector2Int(l_min_x - 2, l_min_y - 2);
            _light_range[1] = new Vector2Int(l_max_x + 2, l_max_y + 2);


            // debug
            // _light_map[l_min_x, l_min_y] = LightType.ObstacleEdgeOfLight;
            // _light_map[l_min_x, l_max_y] = LightType.ObstacleEdgeOfLight;
            // _light_map[l_max_x, l_min_y] = LightType.ObstacleEdgeOfLight;
            // _light_map[l_max_x, l_max_y] = LightType.ObstacleEdgeOfLight;
            var fourDir = Utils.FourDirList;
            var light_start = _light_range[0];
            var light_end = _light_range[1];

            var eList = new List<Vector2Int>();  // 边缘列表
            for (int i = 0; i < 5; i++)
            {
                center_list[i] = center_list[i] - light_start;
            }


            var tMap = _light_cal_map;  //target
            var tLen = tMap.GetLength(0);
            var max_y = light_end.y - light_start.y;
            var max_x = light_end.x - light_start.x;
            if (max_y > 195 || max_x > 195)
            {
                DU.LogError($"[CheckLightMap] 长宽 {max_x} {max_y}");
                return;
            }

            // 清理；写入光照， 不能直接搬，要5次椭圆
            //
            for (int y = 0; y < tLen; y++)
                for (int x = 0; x < tLen; x++)
                {
                    tMap[x, y] = LightType.Dark;
                }
            for (int y = 0; y < max_y; y++)
                for (int x = 0; x < max_x; x++)
                {
                    if (InEllipse(x - center_list[0].x, y - center_list[0].y)
                    || InEllipse(x - center_list[1].x, y - center_list[1].y)
                    || InEllipse(x - center_list[2].x, y - center_list[2].y)
                    || InEllipse(x - center_list[3].x, y - center_list[3].y)
                    || InEllipse(x - center_list[4].x, y - center_list[4].y)
                    )
                        tMap[x, y] = LightType.Fresh;
                }


            // 计算并标记Undef && 边框
            //
            for (int y = 1; y <= max_y; y++)
                for (int x = 1; x <= max_x; x++)
                {
                    var lData = tMap[x, y];
                    var px = x + light_start.x;
                    var py = y + light_start.y;
                    if (lData == LightType.Dark)
                    {
                        foreach (Vector2Int off in fourDir)
                        {
                            var mData = _map[px + off.x, py + off.y];
                            if (tMap[x + off.x, y + off.y] == LightType.Fresh
                                 && mData == CellType.Undefined)
                            {
                                eList.Add(new Vector2Int(x + off.x, y + off.y));
                                tMap[x + off.x, y + off.y] = LightType.FreshEdge;
                            }
                        }
                    }
                }


            // 遍历，分类Undef
            //
            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];
            foreach (var pos in eList)
            {
                if (tMap[pos.x, pos.y] == LightType.FreshEdge)
                {
                    var result = LightMapTraversal(pos, out int edge_count);
                    if (edge_count > 30)
                    {
                        // DU.LogWarning($"edge_count：{edge_count}");
                        foreach (var tp in result)
                        {
                            var p = tp + light_start;
                            if (!_confirm_map[p.x, p.y])
                            {
                                record.Add(p);
                                _map[p.x, p.y] = CellType.ObstacleEdge;
                                _confirm_map[p.x, p.y] = true;
                            }
                        }
                    }

                }
            }

        }

        List<Vector2Int> LightMapTraversal(Vector2Int first, out int edge_count)
        {
            var tMap = _light_cal_map;  //target
            var light_start = _light_range[0];

            edge_count = 0;
            var count = 0;

            _stack[count++] = first;
            tMap[first.x, first.y] = LightType.FreshVisited;

            var result = new List<Vector2Int>();
            var fourDir = Utils.FourDirList;

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;

                foreach (Vector2Int off in fourDir)
                {
                    Vector2Int np = new Vector2Int(x + off.x, y + off.y);
                    var lData = tMap[np.x, np.y];
                    if (lData == LightType.FreshEdge)
                        edge_count++;
                    if (lData == LightType.Fresh || lData == LightType.FreshEdge)
                    {
                        var px = np.x + light_start.x;
                        var py = np.y + light_start.y;
                        var data = _map[px, py];
                        // 暗边界 当做Undefined
                        //
                        if (data == CellType.Undefined || (!_confirm_map[px, py] && data == CellType.ObstacleEdge))
                        {
                            tMap[np.x, np.y] = LightType.FreshVisited;
                            _stack[count++] = np;
                        }
                        else if (data == CellType.Empty || data == CellType.ObstacleByBig)
                            result.Add(pop);
                    }
                }

            }
            return result;
        }

        #endregion

        #region 寻路
        public void StartAStarByIndex(int target_index)
        {
            var p = GetTargetPos(target_index);

            var distance = mapDistanceThreshold;

            if (p.x - distance < _xRange.x) _xRange.x = p.x - distance;
            if (p.x + distance > _xRange.y) _xRange.y = p.x + distance;
            if (p.y - distance < _yRange.x) _yRange.x = p.y - distance;
            if (p.y + distance > _yRange.y) _yRange.y = p.y + distance;
            CheckRebuild(out _, out _);

            p = GetTargetPos(target_index);
            StartAStarBigGridAbs(_templatePos + new Vector2Int(100, 100), p);
        }


        public void StartAStarBigGrid(Vector2Int start, Vector2Int target)
        {
            var offset = new Vector2Int(_xRange.x, _yRange.x);
            StartAStarBigGridAbs(start + offset, target + offset);
        }
        // 绝对坐标 absolute
        public void StartAStarBigGridAbs(Vector2Int find_start, Vector2Int find_target)
        {
            _find_start = find_start;
            _find_target = find_target;
            _find_in_empty = _map[_find_target.x, _find_target.y] == CellType.Empty;

            if (_find_in_empty)
            {
                _find_aStar_target = _find_target;
                _gridData.StartAStar(_find_start, _find_aStar_target);
            }
            else
            {
                // 迷雾实时寻路
                // 1.先找所在地与目标的连线和边界的交点K  （Bresenham）
                // - 交点K可以是"空地" 或 "迷雾" 或 "边界"。"空地"跳到第4步，"迷雾"跳到第3步。
                // 2.从K点开始"边界"连通性递归，按距离顺序，直到遇到"迷雾"，记为点T
                // 3.从T点开始"迷雾"连通性递归，按距离顺序，直到遇到"空地"，记为点E
                // 4.所在地到点E的寻路。
                _find_aStar_target = Utils.DefaultV2I;      //_find_aStar_target 是作为寻路成功否的标识
                _pixRecord[PixRecordType.AreaOfFindNearestFog].Clear();

                Vector2Int pointK = BresenhamGetCross(_find_start, _find_target);
                if (pointK == Utils.DefaultV2I)
                {
                    DU.LogWarning("[BresenhamGetCross] 无法找到交点K");
                    return;
                }


                CellType typeK = _map[pointK.x, pointK.y];
                if (typeK == CellType.Empty)
                {
                    _find_aStar_target = pointK;
                }
                else
                {
                    Vector2Int pointT = default;
                    if (typeK == CellType.ObstacleEdge)
                    {
                        pointT = EdgeTraversal(pointK);
                        if (pointT == Utils.DefaultV2I)
                        {
                            DU.LogWarning("[EdgeTraversal] 无法找到邻近迷雾T");
                            return;
                        }
                    }
                    else if (typeK == CellType.Fog)
                    {
                        pointT = pointK;
                    }

                    _find_aStar_target = FogTraversal(pointT);
                    if (_find_aStar_target == Utils.DefaultV2I)
                    {
                        DU.LogWarning("[FogTraversal] 无法找到邻近空地E");
                        return;
                    }
                }


                // 寻路
                _gridData.StartAStar(_find_start, _find_aStar_target);

            }

        }


        #region 寻路-迷雾情况

        // Bresenham获取交点
        Vector2Int BresenhamGetCross(Vector2Int start, Vector2Int target)
        {
            if (MathF.Abs(start.x - target.x) > MathF.Abs(start.y - target.y))
                DrawLineH(start, target);
            else
                DrawLineV(start, target);

            var record = _pixRecord[PixRecordType.LineOfFindPath];
            var len = record.Count();
            if (record[0] == target)
                for (int i = 0; i < len; i++)
                {
                    var p = record[i];
                    var data = _map[p.x, p.y];
                    if (data != CellType.Undefined && data != CellType.ObstacleByBig)
                        return p;
                }
            else
                for (int i = len - 1; i >= 0; i--)
                {
                    var p = record[i];
                    var data = _map[p.x, p.y];
                    if (data != CellType.Undefined && data != CellType.ObstacleByBig)
                        return p;
                }

            return Utils.DefaultV2I;
        }

        // 水平
        void DrawLineH(Vector2Int start, Vector2Int target)
        {
            var record = _pixRecord[PixRecordType.LineOfFindPath];
            record.Clear();

            int x0, y0, x1, y1;
            if (start.x < target.x)
            {
                x0 = start.x; y0 = start.y; x1 = target.x; y1 = target.y;
            }
            else
            {
                x0 = target.x; y0 = target.y; x1 = start.x; y1 = start.y;
            }
            int dx = x1 - x0, dy = y1 - y0;
            int dir = dy < 0 ? -1 : 1;
            dy *= dir;

            int D = 2 * dy - dx;
            int y = y0;
            for (int x = x0; x <= x1; x++)
            {
                // 嵌入的像素逻辑
                record.Add(new Vector2Int(x, y));

                if (D > 0)
                {
                    y = y + dir;
                    D = D - 2 * dx;
                    record.Add(new Vector2Int(x, y));   // 补充上拐点，想要个连续直线
                }
                D = D + 2 * dy;
            }

        }
        // 竖直
        void DrawLineV(Vector2Int start, Vector2Int target)
        {
            var record = _pixRecord[PixRecordType.LineOfFindPath];
            record.Clear();

            int x0, y0, x1, y1;
            if (start.y < target.y)
            {
                x0 = start.x; y0 = start.y; x1 = target.x; y1 = target.y;
            }
            else
            {
                x0 = target.x; y0 = target.y; x1 = start.x; y1 = start.y;
            }

            int dx = x1 - x0, dy = y1 - y0;
            int dir = dx < 0 ? -1 : 1;
            dx *= dir;

            int D = 2 * dx - dy;
            int x = x0;
            for (int y = y0; y <= y1; y++)
            {
                // 嵌入的像素逻辑
                record.Add(new Vector2Int(x, y));

                if (D > 0)
                {
                    x = x + dir;
                    D = D - 2 * dy;
                    record.Add(new Vector2Int(x, y));   // 补充上拐点，想要个连续直线
                }
                D = D + 2 * dx;
            }

        }

        // 得8方向。参照_map
        Vector2Int EdgeTraversal(Vector2Int start)
        {

            var eightDir = Utils.EightDirList;
            var record = _pixRecord[PixRecordType.AreaOfFindNearestFog];
            record.Clear();

            Action revertAction = () =>
            {
                foreach (var p1 in record)
                    _map[p1.x, p1.y] = CellType.ObstacleEdge;
            };

            var list1 = _EdgeTraversal_list1;
            var list2 = _EdgeTraversal_list2;
            int count1 = 0;
            int count2 = 0;

            list2[count2++] = start;


            while (count2 > 0)
            {
                var temp = list1;
                list1 = list2;
                list2 = temp;       //互换列表

                count1 = count2;
                count2 = 0;

                while (count1 > 0)
                {
                    var pop = list1[--count1];

                    int x = pop.x;
                    int y = pop.y;


                    foreach (Vector2Int off in eightDir)
                    {
                        Vector2Int p = new Vector2Int(x + off.x, y + off.y);
                        var data = _map[p.x, p.y];
                        if (data == CellType.Fog)
                        {
                            revertAction(); // 还原
                            return p;
                        }
                        if (data == CellType.ObstacleEdge)
                        {
                            _map[p.x, p.y] = CellType.ObstacleEdgeTemp;
                            record.Add(p);
                            list2[count2++] = p;
                        }
                    }
                }
            }
            revertAction(); // 还原
            return Utils.DefaultV2I;
        }

        Vector2Int FogTraversal(Vector2Int start)
        {
            var area = Utils.TwoDistanceArea;
            var restore_list = new List<Vector2Int>();

            Action revertAction = () =>
            {
                foreach (var p1 in restore_list)
                    _map[p1.x, p1.y] = CellType.Fog; // 还原
            };

            var list1 = _EdgeTraversal_list1;
            var list2 = _EdgeTraversal_list2;
            int count1 = 0;
            int count2 = 0;

            list2[count2++] = start;


            while (count2 > 0)
            {
                var temp = list1;
                list1 = list2;
                list2 = temp;       //互换列表

                count1 = count2;
                count2 = 0;

                while (count1 > 0)
                {
                    var pop = list1[--count1];

                    int x = pop.x;
                    int y = pop.y;


                    foreach (Vector2Int off in area)
                    {
                        Vector2Int p = new Vector2Int(x + off.x, y + off.y);
                        var data = _map[p.x, p.y];
                        if (data == CellType.Empty)
                        {
                            revertAction();
                            return p;
                        }
                        if (data == CellType.Fog)
                        {
                            _map[p.x, p.y] = CellType.FogArea;
                            restore_list.Add(p);
                            list2[count2++] = p;
                        }
                    }
                }
            }

            revertAction();     // 还原
            return Utils.DefaultV2I;
        }

        #endregion
        #endregion

        #region Save
        readonly Color32 color_gray = new Color32(80, 80, 80, 255);         // 灰色
        readonly Color32 color_white = new Color32(240, 240, 240, 255);     // 
        readonly Color32 color_dark = new Color32(20, 20, 20, 255);         // 
        readonly Color32 color_blue = new Color32(0, 0, 255, 255);          // 
        readonly Color32 color_orange = new Color32(240, 140, 0, 255);      // 
        readonly Color32 color_red = new Color32(255, 0, 0, 255);           // 
        readonly Color32 color_green = new Color32(20, 255, 20, 255);       // 
        readonly Color32 color_yellow = new Color32(240, 240, 0, 255);       // 


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
            var l = GetPrintResult();
            var str = "";
            foreach (var s in l)
                str += $"{s}  ";
            DU.LogWarning(str);
        }
        public List<string> GetPrintResult()
        {
            var result = new List<string>();

            int big_cell_active = _gridData.GetActiveCount();
            int big_cell_total = _gridData.BuildCellTimes;
            int big_cell_multi = _gridData.MultiEmptyCellCount;

            result.Add($"大格子重建次数 {big_cell_total - big_cell_active}；构造次数 {big_cell_total}");
            if (big_cell_multi > 0)
            {
                result.Add($"多侧空地数 {big_cell_multi}");
            }

            if (_gridData.ObstacleFill2EmptyCount > 0)
            {
                result.Add($"障碍填充转空地 {_gridData.ObstacleFill2EmptyCount}个 ");
            }

            return result;
        }


        public Color32[] GetImageMap0()
        {
            Color32[] bytes = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _map[x + _xRange.x, y + _yRange.x];
                    var confirm = _confirm_map[x + _xRange.x, y + _yRange.x];
                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次
                    Color32 color = color_gray;
                    if (pix == CellType.Empty)
                        if (confirm) color = color_dark; else color = new Color32(90, 50, 50, 255);

                    if (pix == CellType.ObstacleEdge)
                        if (confirm) color = color_white; else color = new Color32(240, 180, 180, 255);

                    if (pix == CellType.Fog) color = color_blue;
                    if (pix == CellType.ObstacleByBig) color = color_orange;

                    bytes[index] = color;
                }

            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];
            foreach (var p in record)
            {
                int x = p.x - _xRange.x, y = p.y - _yRange.x, index = (_h - 1 - y) * _w + x;
                bytes[index] = color_green;
            }

            return bytes;
        }

        public Color32[] GetImageLightMap()
        {
            Color32[] bytes = new Color32[_w * _h];
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    var pix = _light_map[x + _xRange.x, y + _yRange.x];
                    int index = (_h - 1 - y) * _w + x;                // 反转y轴，因为之前翻转过一次
                    Color32 color = color_gray;
                    if (pix == LightType.Light) color = new Color32(200, 200, 200, 255);

                    bytes[index] = color;
                }

            if (_light_count == 0 && MoveRecord.Count >= 9)
            {
                var light_start = _light_range[0];
                var tLen = _light_cal_map.GetLength(0);

                for (int y = 0; y < tLen; y++)
                    for (int x = 0; x < tLen; x++)
                    {
                        var pix = _light_cal_map[x, y];
                        int tx = x + light_start.x - _xRange.x;
                        int ty = y + light_start.y - _yRange.x;
                        int index = (_h - 1 - ty) * _w + tx;

                        if (pix == LightType.Fresh) bytes[index] = new Color32(130, 180, 130, 255);
                        if (pix == LightType.FreshEdge) bytes[index] = color_blue;
                        if (pix == LightType.FreshVisited) bytes[index] = new Color32(130, 130, 130, 255);
                    }

            }
            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];
            foreach (var p in record)
            {
                int x = p.x - _xRange.x, y = p.y - _yRange.x, index = (_h - 1 - y) * _w + x;
                bytes[index] = color_green;
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
                    if (x == 72 && y == 133)
                    {
                        var a = 0;
                    }
                    var pix = _small_map[x, y];
                    int index = (len - 1 - y) * len + x;                // 反转y轴，因为之前翻转过一次  

                    Color32 color = color_gray;
                    if (pix == CellType.Empty) color = color_dark;
                    if (pix == CellType.ObstacleEdge) color = color_white;
                    if (pix == CellType.Fog) color = color_blue;
                    if (pix == CellType.FogArea) color = new Color32(0, 0, 180, 255);

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

                    Color32 color = color_gray;
                    if (px >= _judgePos.x && px < _judgePos.x + jLen
                        && py >= _judgePos.y && py < _judgePos.y + jLen)
                    {
                        if (pix == CellType.Fog)
                            color = color_blue;
                        else
                        {
                            var jData = _judge_map[px - _judgePos.x, py - _judgePos.y];
                            // var r = jData.CheckResult();
                            if (pix == CellType.Empty) color = color_dark;
                            if (pix == CellType.ObstacleEdge) color = color_white;
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
                    bytes[index] = color_red;
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

                    Color32 color = color_gray;
                    if (pix == CellType.Empty) color = color_dark;
                    if (pix == CellType.ObstacleEdge) color = color_white;
                    if (pix == CellType.ObstacleByBig) color = color_orange;

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

        #region SaveAStar
        public Color32[] GetImageGridAStar()
        {
            Color32[] colors = GetImageGrid();

            // 迷雾分析流程 —— 展示
            if (!_find_in_empty)
            {
                var record = _pixRecord[PixRecordType.LineOfFindPath];
                foreach (var p in record)
                {
                    int x = p.x - _xRange.x, y = p.y - _yRange.x, index = (_h - 1 - y) * _w + x;
                    colors[index] = color_red;
                }
            }


            if (_find_aStar_target == Utils.DefaultV2I)
                return colors;

            // 迷雾分析流程 —— 展示
            if (!_find_in_empty)
            {
                var record = _pixRecord[PixRecordType.AreaOfFindNearestFog];
                foreach (var p in record)
                {
                    int x = p.x - _xRange.x, y = p.y - _yRange.x, index = (_h - 1 - y) * _w + x;
                    colors[index] = color_yellow;
                }
            }


            var grid = _gridData._grid;
            var target_c = _gridData._target_c;
            BigCell cell = grid[target_c.x, target_c.y];

            //两格之间的寻路, 以目标像素点为最开始的起点。
            SmallCellFinder finder = new SmallCellFinder();
            Vector2Int start = _find_aStar_target;        //从终点倒着寻找起点
            Vector2Int target = default;
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
                            int index = GetColorsIndex(px, py);
                            if (IU.Equal(colors[index], new Color32(20, 60, 20, 255))
                            || IU.Equal(colors[index], color_dark))
                                colors[index] = yellow;
                        }
                }

            if (!_gridData.success)
                return colors;

            var next_cp = cell.ParentPos;
            // 循环粒度：从cell_find_start走到 当前cell与next_cell的边界位置
            while (next_cp != Utils.DefaultV2I)
            {
                var next_cell = grid[next_cp.x, next_cp.y];
                var cx = cell.x;
                var cy = cell.y;
                var nextCX = next_cell.x;
                var nextCY = next_cell.y;
                // 终点在哪里
                var zero_pos = new Vector2Int(cx * 5, cy * 5);
                var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5);
                var next_map = GetByRegion(nextCX * 5, nextCY * 5, 5, 5);

                GridData.GetConnectPixel(map, next_map, new Vector2Int(cx, cy)
                , new Vector2Int(next_cell.x, next_cell.y), out var rA, out var rB);


                // 用5X5去寻路，从start走到A再走一步到B。
                var result = finder.BeginAStar(map, start - zero_pos, rA);

                // 涂颜色
                foreach (var _ in result)
                {
                    var pos = _ + zero_pos;
                    int index = GetColorsIndex(pos.x, pos.y);
                    colors[index] = color_green;
                }

                cell = next_cell;
                next_cp = cell.ParentPos;

                //下个起点在哪里
                start = rB + zero_pos;
            }

            {
                // 终点还要一次。
                var zero_pos = new Vector2Int(cell.x * 5, cell.y * 5);
                var map = GetByRegion(zero_pos.x, zero_pos.y, 5, 5);

                try
                {


                    var result = finder.BeginAStar(map, start - zero_pos, _find_start - zero_pos);
                    // 涂颜色
                    foreach (var _ in result)
                    {
                        var pos = _ + zero_pos;
                        int index = GetColorsIndex(pos.x, pos.y);
                        colors[index] = color_green;
                    }
                }
                catch (Exception)
                {
                    var a = 0;
                }

            }




            return colors;
        }

        #endregion
        public bool[,] GetByRegion(int fromX, int fromY, int w, int h)
        {
            bool[,] result = new bool[w, h];
            int x_end = fromX + w;
            int y_end = fromY + h;
            for (int y = fromY; y < y_end; y++)
                for (int x = fromX; x < x_end; x++)
                    result[x - fromX, y - fromY] = _map[x, y] == CellType.Empty;
            return result;
        }


        public void SaveAStar(string path)
        {
            var target_pos = _gridData._target_c;
            if (target_pos == Utils.DefaultV2I)
                return;


            var colors = GetImageGridAStar();
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
        }

        #region JudgeCell

        public struct JudgeCell
        {
            public bool Access;
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


            public CellType GuessType()
            {
                CellType type;
                if (obstacle_edge_num >= empty_num && obstacle_edge_num >= undefined_num)
                    type = CellType.ObstacleEdge;
                else if (empty_num >= undefined_num)
                    type = CellType.Empty;
                else
                    type = CellType.Undefined;

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


        public enum LightType : byte
        {
            Dark = 0,                   // 暗地                         灰色
            Light = 1,                  // 亮地。                       白色
            Fresh = 2,                  // "新鲜"亮地。                    绿色
            ObstacleEdgeOfLight = 3,    // 由亮地推算的边界,一般代表"地图图标"     紫色
            FreshEdge = 4,              // "新鲜"边界
            FreshVisited = 5,              // "新鲜"边界

        }

        public enum PixRecordType : byte
        {
            ObstacleEdgeOfLight = 0,        //光照地图，推算的补充边界
            LineOfFindPath = 1,        //光照地图，推算的补充边界
            AreaOfFindNearestFog = 2,        //光照地图，推算的补充边界
        }
    }
}