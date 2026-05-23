using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Script.Framework.Else;
using Script.Util;
using UnityEngine;


namespace Script.Model.Auto
{

    /// <summary>
    /// 地图数据器。没有任何非托管对象。
    /// 从截屏Bitmap中截取小地图开始
    /// 左下角为(0,0)，涉及到模版匹配时，坐标系转换到左上角为(0,0)
    /// </summary>
    public partial class MapData
    {
        readonly int mapInitialEdge = 400;      // 总地图初始边长
        readonly int mapDistanceThreshold = 10; // 有效地图执行居中调整——阈值，距边10。这样map遍历、grid遍历都不会出界
        readonly int mapSizeThreshold = 50;     // 总地图扩容——阈值，边长还差50
        readonly int mapExpandEdge = 400;       // 总地图每次扩容时增加的长度

        readonly int sourceEdge = 220;          // 源图边长
        readonly int templateEdge = 160;        // 模版图边长，一帧最多走17像素，这里有60像素的空间。
        readonly Vector2Int PlayerPos_SmallMap = new Vector2Int(100, 99);

        Vector2Int ellipseRadius;                           // 椭圆轴半径
        Vector3Int ellipseRadiusSquare;                     // 椭圆轴半径平方,平方积
        List<(Vector2Int, Vector2Int)> EdgeFogList;         // 边缘项，属于边缘迷雾需求，
        Vector2Int[] playerIconDraw;                        // 角色图标画法

        int color_set;                                      // 颜色套件

        /// <summary>
        /// 与光照地图需求有关，也可以查看。
        /// 序越大越新，容量100
        /// </summary>
        public List<Vector2Int> MoveRecord;
        public List<Vector2Int> P1PosRecord;
        public List<(int, float)> AccuracyRecord;      //匹配准确率记录
        public int FrameCount { get => _frameCount; }     // 当前帧序
        int _frameCount;

        /// <summary>
        /// 总地图，只含(Undef/Empty/ObstacleEdge/ObstacleByBig/Fog/FogArea)。
        /// x轴向右，y轴向上。Bitmap和OpenCV的接口原本是y轴向下，所以对结果都取反过了.
        /// </summary>
        public PixType[,] _map;
        public LightUnion[,] _light_map;    // 全 光照地图，=视野刷过的区域。
        public bool[,] _confirm_map;        // 全 判定地图，false表示在判定系统中属于未定义、在寻路里属于确定
        public GridData _gridData;
        public int _mapEdge;                // 当前地图的容器边长
        Vector2Int _xRange;                 // 内容x轴范围  (内容: _map中的有效地图)
        Vector2Int _yRange;                 // 内容y轴范围
        Vector2Int _range_zero;             // 内容起点
        int _w;                             // 内容宽  
        int _h;                             // 内容高


        string _id;
        CVRect _rect;                       // 小地图在屏幕中的位置 200 * 200
        int _rectW;                         // 小地图宽
        int _rectH;                         // 小地图高

        Color32[] _small_map_colors;                                // 本帧小地图(原色)
        ConvInfo[] _small_map_conv;                                 // 本帧小地图(卷积后)
        PixType[,] _small_map = new PixType[200, 200];              // 本帧小地图(解析后)
        public JudgePix[,] _judge_map = new JudgePix[300, 300];     // 本帧的judge图
        LightType[,] _light_cal_map = new LightType[200, 200];      // 用于运算的光照地图

        PixType[,] _debug_error_small_map;                          // debug，上个异常的小地图
        PixType[,] _debug_small_map;                                // debug，显示遍历过程

        MapIconData[] _icon_datas;                                  //存着地图图标信息
        Vector2Int _fixed_ref_pos;                                  // 固定参照点 (外界描述位置时的参照点)
        Vector2Int _small_map_pos;                                  // 小地图在总图上的起点坐标 (左下角的相对位置)
        Vector2Int _judge_map_pos;                                  // judge图在总图上的起点坐标
        Vector2Int _move_delta;                                     // 本帧位移

        // 信任P1的中心点
        bool _has_init_empty;                                       // 没有一个confirm && empty

        Dictionary<PixRecordType, List<Vector2Int>> _pixRecord;     // debug用的像素记录

        Vector2Int[] _light_range = new Vector2Int[2];              // 光照：5个移动总光照范围
        int _light_count;                                           // 光照：5个移动后触发光照地图检查


        public bool _find_target_in_empty;                      // 两定点间的寻路 参数


        BigCellPathResult _findFogPath = new BigCellPathResult();       //记录的是对象，所以不受地图扩容影响

        // int _SaveErrorSmallMap_times = 0;                               //保存错误地图次数
        // List<Vector2Int> apply_edge_constant;                           //小地图里会传入到_map的区域的边缘。

        // public List<Vector2Int> _edge_new_empty;                        //用于Debug展示


        // 收集所有错误的次数。0-匹配失败;1-漏缝错误;2-小地图初始空地失败
        public int[] ErrorList;
        public Vector2Int IconErrorPos;

        //// *********  以下变量起到复用作用，无需理解   ********* ////

        (int, int, Color32[]) _colorsBuffer;                            //复用Color32[], 条件：连续两次相同的w、h  
        Vector2Int[] _EdgeTraversal_list1 = new Vector2Int[800];        //方法内部复用
        Vector2Int[] _EdgeTraversal_list2 = new Vector2Int[800];        //方法内部复用

        Vector2Int[] _stack = new Vector2Int[40000];                    //方法内部复用
        SList<Vector2Int> _stack1 = new SList<Vector2Int>(40000);       //方法内部复用
        static Vector2Int[] _stack_static = new Vector2Int[40000];      //方法内部复用

        List<Vector2Int> _reusedList1 = new List<Vector2Int>();         //方法内部复用
        List<Vector2Int> _reusedList2 = new List<Vector2Int>();         //方法内部复用

        //// ************************************************** ////


        /// <summary>
        /// rect: 小地图在屏幕中的位置;
        /// </summary>
        public MapData(string id, CVRect rect)
        {
            _id = id;
            _rect = rect;
            _rectW = rect.w;
            _rectH = rect.h;

            _mapEdge = mapInitialEdge;
            _map = new PixType[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _light_map = new LightUnion[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _confirm_map = new bool[_mapEdge, _mapEdge];   // 1000 * 1000, 1MB
            _small_map_pos = new Vector2Int((_mapEdge - 200) / 2, (_mapEdge - 200) / 2);   // 右上角就是(599,599)
            _fixed_ref_pos = _small_map_pos + PlayerPos_SmallMap;
            _judge_map_pos = _small_map_pos + new Vector2Int(-50, -50);

            if (_gridData == null)
                _gridData = new GridData(this);

            // ellipseRadius = new Vector2Int(48, 38);      // 光照椭圆轴半径最好做成自适应，检测迷雾与角色最近距离去算
            ellipseRadius = new Vector2Int(44, 34);         // 统一改小，因为坡度差变大光照就会变小。
            ellipseRadiusSquare = new Vector3Int(ellipseRadius.x * ellipseRadius.x, ellipseRadius.y * ellipseRadius.y, 0);
            ellipseRadiusSquare = new Vector3Int(ellipseRadiusSquare.x, ellipseRadiusSquare.y
                , ellipseRadiusSquare.x * ellipseRadiusSquare.y);

            MoveRecord = new List<Vector2Int>();
            P1PosRecord = new List<Vector2Int>();
            AccuracyRecord = new List<(int, float)>();
            _pixRecord = new Dictionary<PixRecordType, List<Vector2Int>>();
            _pixRecord[PixRecordType.ObstacleEdgeOfLight] = new List<Vector2Int>();
            _pixRecord[PixRecordType.LineOfFindPath] = new List<Vector2Int>();
            _pixRecord[PixRecordType.AreaOfFindNearestFog] = new List<Vector2Int>();
            _pixRecord[PixRecordType.ObstacleEdgeEndPoint] = new List<Vector2Int>();
            _pixRecord[PixRecordType.ObstacleEdgeGap] = new List<Vector2Int>();
            _pixRecord[PixRecordType.ObstacleEdgeGapTotal] = new List<Vector2Int>();


            playerIconDraw = new Vector2Int[]
            {new Vector2Int(0,0),new Vector2Int(-1,-1),new Vector2Int(-1,1),new Vector2Int(1,-1),new Vector2Int(1,1),};
            _has_init_empty = false;


            InitIconData();
            // InitApplyEdgeList();

            ErrorList = new int[3];
        }


        #region Capture

        public void Capture(Color32[] pixels)
        {
            _frameCount++;

            // 转成像素数组, 再垂直方向反序一下，从下至上，和人观察习惯一致
            IU.Color32ReverseYAxis(pixels, _rectW);

            _small_map_colors = pixels;
            _small_map_conv = ConvolutionColors(_small_map_colors, _rectW, _rectH);

            RestoreMapInfo();

            DU.RunWithTimer(() =>
            {
                ColorToData(_small_map_colors, out _small_map, out var endpoint_list, out var gap_list);
                // 识别图标
                RecognizeIcon(pixels, _small_map, _small_map_conv);
                // 第一帧_small_map必需有边界，这样才能进行下去
                bool is_first = _frameCount == 1;
                if (!is_first)
                {
                    // DU.RunWithTimer(() =>
                    // {
                    // 源图，计算位移
                    using (Mat template = GetTemplateMat(_small_map, templateEdge))
                    using (Mat source = GetSourceMat(sourceEdge))
                    // 匹配结果
                    using (Mat result = IU.MatchTemplateCustomMask(source, template, template))
                    {
                        // 0.8阈值代表 0.6的良品率，即模版和输入60%白色像素匹配上了
                        // 如果被干扰，用很少的边界匹配上，后续不过是Undefined污染。
                        var list = IU.FindResultMin(result, templateEdge, templateEdge, 0.8f, out var min_score);
                        list.Sort((a, b) => a.Score.CompareTo(b.Score));
                        var score = list.Count > 0 ? list[0].Score : 0;

                        // 匹配检测，无法匹配
                        if (list.Count == 0)
                        {
                            ApplyAccuracyRecord(ConvertScore(min_score));
                            SetError(0);
                            return;
                        }
                        else
                        {
                            // if (score > 0.6)
                            // {
                            //     IU.SaveMat(template,  $"{Application.streamingAssetsPath}/Error/match_t.png");
                            //     IU.SaveMat(source,  $"{Application.streamingAssetsPath}/Error/match_s.png");
                            // }
                            ApplyAccuracyRecord(ConvertScore(score));
                        }

                        var rect = list[0].Rect;

                        //计算位移  动了是(5, 8)    不动是(17,17)。最终就是匹配图像走了(-12,-9),人走了(12,9)
                        int offset = (sourceEdge - templateEdge) / 2;
                        _move_delta = new Vector2Int(rect.x - offset, rect.y - offset);

                        // 到这里就说明是有效的
                        _small_map_pos += _move_delta;
                        ApplyMoveRecord(_move_delta);
                    }
                    // }, "MapData.TemplateMatch");

                }
                // _map是否要扩容
                UpdateMapSize(_small_map_pos);
                // 应用图标。红记录点、物品、角色
                ApplyIcon(_small_map, _small_map_pos);
                // 边界缝隙检查
                ApplyObstacleEdgeGap(endpoint_list, gap_list, _small_map_pos);
                // 光照地图
                ApplyLightMap(_small_map, _small_map_pos);
                InitEmpty(_small_map, _small_map_pos);
                Apply(_small_map);

                _debug_error_small_map = null;

            }, "MapData.Capture");
        }

        float ConvertScore(float num)
        {
            var result = 1 / (1 + num * num);
            return result;
        }


        void SetError(int error_index)
        {
            if (_debug_error_small_map == null) _debug_error_small_map = _small_map;
            ErrorList[error_index]++;
        }

        #region ColorToData


        static Vector2Int[] fog_constant = new Vector2Int[]{
                new Vector2Int(-2,0),new Vector2Int(-1,0),new Vector2Int(1,0),new Vector2Int(2,0),
                new Vector2Int(-1,1),new Vector2Int(0,1),new Vector2Int(1,1),
                new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1),
                new Vector2Int(0,2),new Vector2Int(0,-2),
            };

        /// <summary>
        /// 拍摄图 => 地图数据, 核心
        /// </summary>
        public static void ColorToData(Color32[] pixels, out PixType[,] result
                                    , out List<Vector2Int> endpoint_list, out List<Vector2Int> gap_list)
        {
            int color_set = 0;
            int rectW = 200;
            int rectH = 200;

            Color32 min_1th = default;
            Color32 max_1th = default;
            Vector2Int B_to_G_1th = default;
            Vector2Int R_to_G_1th = default;
            Color32 min_2th = default;
            Color32 max_2th = default;
            Color32 min_3th = default;
            Color32 max_3th = default;
            Vector2Int B_to_G_3th = default;

            // 1类边界
            min_1th = new Color32(105, 105, 145, 255);      // (106,107,147)
            max_1th = new Color32(150, 150, 195, 255);
            B_to_G_1th = new Vector2Int(15, 60);
            R_to_G_1th = new Vector2Int(-3, 15);
            // 2类边界
            min_2th = new Color32(70, 70, 87, 255);
            max_2th = max_1th;
            // 1类迷雾, 被筛出的像素为Fog，2像素范围内都为FogArea。
            // 迷雾的作用：1.阻挡2类边界的误判。 2.在"迷雾寻路"里指导下个寻路目标点
            min_3th = new Color32(0, 120, 160, 255);
            max_3th = new Color32(90, 142, 192, 255);
            B_to_G_3th = new Vector2Int(32, 63);

            // 小地图，只含(Undef/ Block/ EmptyTemp/ Fog/ FogArea/ ObstacleEdge/ ObstacleEdgeTemp/ ObstacleEdgeGap)。
            PixType[,] colorData = new PixType[rectW + 4, rectH + 4];    // 省边界遍历，省FogArea

            var x_start = 2;
            var x_end = rectW + 2;
            var y_start = 2;
            var y_end = rectH + 2;
            var first_list = new List<Vector2Int>();

            // 1.处理NPC名字条, 装备条。
            // 计算 Block、TempEmpty
            RecognizeBlock(pixels, colorData, rectW, rectH);

            // 2.处理迷雾
            for (int y = y_start; y < y_end; y++)
                for (int x = x_start; x < x_end; x++)
                {
                    int index = (y - 2) * rectW + x - 2;
                    var color = pixels[index];
                    var data = colorData[x, y];
                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;


                    if (data != PixType.Undefined && data != PixType.FogArea)
                        continue;

                    if (Between(color, min_3th, max_3th)
                        && b - g >= B_to_G_3th.x && b - g <= B_to_G_3th.y)
                    {
                        colorData[x, y] = PixType.Fog;

                        foreach (var offset in fog_constant)
                        {
                            int px = x + offset.x;
                            int py = y + offset.y;
                            if (colorData[px, py] != PixType.Fog)
                                colorData[px, py] = PixType.FogArea;
                        }
                    }
                }

            // 3.处理边界
            for (int y = y_start; y < y_end; y++)
                for (int x = x_start; x < x_end; x++)
                {
                    int index = (y - 2) * rectW + x - 2;
                    var color = pixels[index];
                    var data = colorData[x, y];
                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;

                    if (data != PixType.Undefined)
                        continue;

                    if (Between(color, min_1th, max_1th)
                    && b - g >= B_to_G_1th.x && b - g <= B_to_G_1th.y
                    && r - g >= R_to_G_1th.x && r - g <= R_to_G_1th.y)
                    {
                        first_list.Add(new Vector2Int(x, y));
                        colorData[x, y] = PixType.ObstacleEdge;
                    }
                    else if (Between(color, min_2th, max_2th)
                    && r - g >= R_to_G_1th.x && r - g <= R_to_G_1th.y)
                    {
                        colorData[x, y] = PixType.ObstacleEdgeTemp;
                    }
                }


            // }, "Condition check");

            ColorToDataTraversal_ObstacleEdge(colorData, first_list, out endpoint_list, out gap_list);

            var temp = new PixType[rectW, rectH];
            for (int y = y_start; y < y_end; y++)
                for (int x = x_start; x < x_end; x++)
                {
                    var data = colorData[x, y];
                    if (data != PixType.ObstacleEdgeTemp && data != PixType.ObstacleEdgeGap
                        && data != PixType.Block)
                        temp[x - 2, y - 2] = colorData[x, y];
                }


            result = temp;

        }

        static void RecognizeBlock(Color32[] pixels, PixType[,] colorData, int rectW, int rectH)
        {
            var x_start = 2;
            var x_end = rectW + 2;
            var y_start = 2;
            var y_end = rectH + 2;

            var NPC_min = new Color32(191, 191, 210, 255);
            var NPC_max = new Color32(201, 201, 221, 255);
            List<Vector2Int> NPC_list = new List<Vector2Int>();


            for (int y = y_start; y < y_end; y++)
                for (int x = x_start; x < x_end; x++)
                {
                    int index = (y - 2) * rectW + x - 2;
                    var color = pixels[index];
                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;

                    if (r <= 15 && g <= 15 && b <= 15)      // 文字描边。装备条。正常空地色是在 15,15,15以上
                        colorData[x, y] = PixType.Block;

                    else if (r < 55 && g < 55 && b < 55)    // 候选空地
                        colorData[x, y] = PixType.EmptyTemp;

                    else if ((r == NPC_min.r && g == NPC_min.g && b == NPC_min.b)     // NPC名字条
                          || (r == NPC_max.r && g == NPC_max.g && b == NPC_max.b))
                    {
                        NPC_list.Add(new Vector2Int(x, y));
                    }
                }

            // 图标可能会有多个点满足，我们将其中邻近的多个点集中到一个点


            // 抗干扰
            Vector2Int anchor = default;
            int bounding_box_top = 0;
            int bounding_box_bottom = 0;
            int bounding_box_left = 0;
            int bounding_box_right = 0;
            int NPC_len = NPC_list.Count;
            // NPC名字条 上下不超过15px，水平不限。 然后上下左右Padding(5,8,12,12)
            for (var i = 0; i <= NPC_len; i++)       // 原本数组顺序从下到上，从左到右
            {
                if (anchor != default && (i == NPC_len || NPC_list[i].y - anchor.y > 15))
                {
                    // draw 包围盒
                    bounding_box_top += 5; if (bounding_box_top >= y_end) bounding_box_top = y_end - 1;
                    bounding_box_bottom -= 8; if (bounding_box_bottom < 0) bounding_box_bottom = 0;
                    bounding_box_left -= 12; if (bounding_box_left < 0) bounding_box_left = 0;
                    bounding_box_right += 12; if (bounding_box_right >= x_end) bounding_box_right = x_end - 1;
                    for (int y = bounding_box_bottom; y <= bounding_box_top; y++)
                        for (int x = bounding_box_left; x <= bounding_box_right; x++)
                        {
                            colorData[x, y] = PixType.Block;
                        }
                }
                if (i < NPC_len)
                {
                    var pos = NPC_list[i];
                    if (anchor == default || pos.y - anchor.y > 15)     // 重置流程
                    {
                        anchor = pos;
                        bounding_box_top = pos.y;
                        bounding_box_bottom = pos.y;
                        bounding_box_left = pos.x;
                        bounding_box_right = pos.x;
                    }
                    else
                    {
                        if (pos.x < bounding_box_left)
                            bounding_box_left = pos.x;
                        else if (pos.x > bounding_box_right)
                            bounding_box_right = pos.x;

                        if (pos.y < bounding_box_bottom)
                            bounding_box_bottom = pos.y;
                        else if (pos.y > bounding_box_top)
                            bounding_box_top = pos.y;
                    }
                }
            }

        }

        /// <summary>
        /// 将2类边界转为1类边界
        /// 从ObstacleEdge地块开始遍历所有的ObstacleEdgeTemp，并将其转为ObstacleEdge
        /// 猜想优化点：将colorData扩展为[w+1,h+1]，边缘设置为0，这样就不用每次都判断边界了
        /// </summary>
        public static void ColorToDataTraversal_ObstacleEdge(PixType[,] colorData,
                    List<Vector2Int> first_list, out List<Vector2Int> end_points, out List<Vector2Int> gap_list)
        {
            end_points = new List<Vector2Int>();
            gap_list = new List<Vector2Int>();
            var revert_list = new List<Vector2Int>();


            var count = 0;
            foreach (var pos in first_list)
            {
                _stack_static[count++] = pos;
            }

            while (count > 0)
            {
                var pop = _stack_static[--count];
                int x = pop.x;
                int y = pop.y;

                var sur_obstacle_count = 0;
                var sur_obstacle_index = -1;
                bool continuous = true;

                for (int i = 0; i < 8; i++)
                {
                    var offset = Utils.EightDirList[i];

                    int px = offset.x + x;
                    int py = offset.y + y;
                    var sur_data = colorData[px, py];

                    if (sur_data == PixType.ObstacleEdgeTemp)
                    {
                        colorData[px, py] = PixType.ObstacleEdge;
                        _stack_static[count++] = new Vector2Int(px, py);
                    }

                    if (sur_data == PixType.ObstacleEdge || sur_data == PixType.ObstacleEdgeTemp)
                    {
                        sur_obstacle_count++;
                        var last = sur_obstacle_index;
                        if (last == 0 && i == 7)
                        {
                            // 特殊情况,首尾相连
                        }
                        else if (last != -1 && i - last > 1)
                            continuous = false;

                        sur_obstacle_index = i;
                    }
                }

                if (sur_obstacle_count <= 1 || sur_obstacle_count == 2 && continuous)
                {
                    // 最边上的一圈不要，记得考虑有(2,2)的偏移
                    if (x >= 3 && x <= 200 && y >= 3 && y <= 200)
                        end_points.Add(pop);
                }
            }

            foreach (var p in end_points)
            {
                for (int i = 0; i < 8; i++)
                {
                    var offset = Utils.EightDirList[i];

                    int px = offset.x + p.x;
                    int py = offset.y + p.y;
                    var sur = new Vector2Int(px, py);
                    var sur_data = colorData[px, py];

                    // if (sur_data == PixType.EmptyTemp || sur_data == PixType.Undefined )
                    if (sur_data == PixType.EmptyTemp)
                    {
                        colorData[px, py] = PixType.ObstacleEdgeGap;
                        if (sur_data == PixType.EmptyTemp)
                            revert_list.Add(sur);
                    }
                    else if (sur_data == PixType.ObstacleEdgeGap)   // 连接
                    {
                        colorData[px, py] = PixType.ObstacleEdge;
                        gap_list.Add(sur);
                    }
                }
            }

            // 还原
            foreach (var p in revert_list)
            {
                var data = colorData[p.x, p.y];
                if (data == PixType.ObstacleEdgeGap)
                    colorData[p.x, p.y] = PixType.EmptyTemp;
            }

        }


        Mat GetSourceMat(int length)
        {
            int x_start = (_rectW - length) / 2 + _small_map_pos.x;
            int y_start = (_rectH - length) / 2 + _small_map_pos.y;

            byte[] source_bytes = new byte[length * length];

            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    var map_data = _map[j + x_start, i + y_start];
                    if (map_data == PixType.ObstacleEdge)
                        source_bytes[i * length + j] = 255;

                }

            Mat source = Mat.FromPixelData(length, length, MatType.CV_8UC1, source_bytes);


            // @debug
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

        void ApplyP1Record()
        {
            var p1 = FindPlayerPos(MapIconType.P1, out _);
            var len = P1PosRecord.Count;
            if (len == 0 || len > 0 && P1PosRecord[len - 1] != p1)
                P1PosRecord.Add(p1);

            if (P1PosRecord.Count >= 100)
            {
                P1PosRecord = P1PosRecord.GetRange(50, 50);
            }
        }

        void ApplyAccuracyRecord(float num)
        {
            AccuracyRecord.Add((_frameCount, num));
            if (AccuracyRecord.Count >= 500)
            {
                AccuracyRecord = AccuracyRecord.GetRange(100, 400);
            }

        }

        public float GetCurAccuracyScore()
        {
            if (AccuracyRecord.Count > 0)
            {
                return AccuracyRecord[AccuracyRecord.Count - 1].Item2;
            }
            return 0;
        }
        public float GetAverageAccuracyScore()
        {
            float total = 0;
            foreach (var s in AccuracyRecord)
            {
                total += s.Item2;
            }

            return total / AccuracyRecord.Count;
        }


        #endregion

        #region Recog Icon

        static int check_icon_count;

        /// <summary>
        /// 参数:1.小地图原图，2.识别范围。输出:1.colorData
        /// 
        /// 识别范围 + 小地图原图 => _icon_pos_cache => _map
        /// </summary>
        void RecognizeIcon(Color32[] pixels, PixType[,] small_map, ConvInfo[] pixels_conv)
        {
            var rectW = _rectW;
            var rectH = _rectH;

            for (int i = 0; i < _icon_datas.Length; i++)
            {
                var icon_data = _icon_datas[i];
                if (icon_data == null)
                    continue;
                icon_data.AddList.Clear();
            }

            PixType[,] used = new PixType[rectW, rectH];

            // todo:_动态Icon，在small_map是Undefined可以替换成上帧_map。能够解决玩家堵在通道的问题。
            RecogDynamic(pixels, pixels_conv, _icon_datas_dynamic_1th, used);
            RecogDynamic(pixels, pixels_conv, _icon_datas_dynamic_2th, used);


            // 把P2制造的迷雾 置换为Undefined，防止干扰 扇区排除
            // 可以后续还原。
            var p2_icon_data = GetIconData(MapIconType.P2);
            var recogPos = p2_icon_data.RecogPos;
            var size = p2_icon_data.Size;
            foreach (var pos in p2_icon_data.AddList)
            {
                var start = pos - recogPos;
                int xs = start.x, xe = start.x + size.x, ys = start.y, ye = start.y + size.y;
                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        var sData = small_map[x, y];
                        if (sData == PixType.Fog)
                        {
                            small_map[x, y] = PixType.Undefined;
                            foreach (var off in fog_constant)
                            {
                                if (small_map[x + off.x, y + off.y] == PixType.FogArea)
                                {
                                    small_map[x + off.x, y + off.y] = PixType.Undefined;
                                }
                            }
                        }

                    }
            }

        }



        void RecogStatic(Color32[] pixels, ConvInfo[] pixels_conv
                      , List<MapIconType> type_list, PixType[,] used, Vector2Int small_map_pos)
        {
            // int xs = 21, xe = 179, ys = 21, ye = 179;
        }

        void RecogDynamic(Color32[] pixels, ConvInfo[] pixels_conv, List<MapIconType> type_list,
                            PixType[,] used)
        {
            check_icon_count = 0;

            var rectW = _rectW;


            int xs = 21, xe = 179, ys = 21, ye = 179;   // x_start/x_end/y_start/y_end
            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    int index = y * rectW + x;
                    var color = pixels[index];
                    var data = used[x, y];

                    byte r = color.r;
                    byte g = color.g;
                    byte b = color.b;
                    // 排除项
                    if (data == PixType.Icon || r < 100 && g < 100 && b < 100)
                        continue;

                    check_icon_count++;
                    var conv = pixels_conv[index].Conv;

                    for (int i = 0; i < type_list.Count; i++)
                    {
                        var type = type_list[i];
                        var icon_data = _icon_datas[(int)type];
                        var col_min = icon_data.ColorMin;
                        var col_max = icon_data.ColorMax;
                        var conv_min = icon_data.ConvMin;
                        var conv_max = icon_data.ConvMax;

                        bool fit = conv.r >= conv_min.r && conv.r <= conv_max.r
                                   && conv.g >= conv_min.g && conv.g <= conv_max.g
                                  && conv.b >= conv_min.b && conv.b <= conv_max.b
                                   && color.r >= col_min.r && color.r <= col_max.r
                                       && color.g >= col_min.g && color.g <= col_max.g
                                      && color.b >= col_min.b && color.b <= col_max.b;
                        if (fit)
                        {

                            var p = new Vector2Int(x, y);
                            icon_data.AddList.Add(p);
                            UsedMapApplyIcon(icon_data, p, used);
                        }
                    }
                }

        }

        void UsedMapApplyIcon(MapIconData icon_data, Vector2Int pos, PixType[,] used)
        {
            var recogPos = icon_data.RecogPos;
            var size = icon_data.Size;

            var start = pos - recogPos;
            int xs = start.x, xe = start.x + size.x, ys = start.y, ye = start.y + size.y;

            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    used[x, y] = PixType.Icon;
                }
        }

        void ApplyIcon(PixType[,] small_map, Vector2Int small_map_pos)
        {

            ClearDynamicIcon(small_map_pos);


            for (int i = 0; i < _icon_datas.Length; i++)
            {
                var icon_data = _icon_datas[i];
                var type = (MapIconType)i;

                if (icon_data == null || icon_data.AddList.Count == 0)
                    continue;

                // P1需要额外关照下。地图的火焰会被识别成P1。
                // 如果P1靠边未被识别，火焰被识别。看需不需要限制。事件概率小，做了自由牺牲大。
                if (type == MapIconType.P1 && icon_data.AddList.Count > 1)
                {
                    var P1_his_pos = FindPlayerPosAndHistory(MapIconType.P1, out _);
                    var suggest_P1_pos = P1_his_pos + _move_delta - small_map_pos;

                    RecogUtil.ListFindClosest(icon_data.AddList, suggest_P1_pos, out var result, out _);
                    icon_data.AddList.Clear();
                    icon_data.AddList.Add(result);
                }


                foreach (var p in icon_data.AddList)
                {
                    SmallMapApplyIcon(icon_data, p, small_map, small_map_pos);
                    icon_data.InstList.Add(p + small_map_pos);
                }
            }


            ApplyP1Record();
        }



        // P2会制造迷雾，但原方案无法筛掉迷雾，也不好一棒子打死说没迷雾。毕竟TeamP1可以在迷雾中移动。
        // 换个角度，别的Icon也可以遮挡迷雾，所以P2有权利遮挡迷雾。
        void SmallMapApplyIcon(MapIconData icon_data, Vector2Int pos,
                            PixType[,] small_map, Vector2Int small_map_pos)
        {
            var recogPos = icon_data.RecogPos;
            var size = icon_data.Size;

            var start = pos - recogPos;
            int xs = start.x, xe = start.x + size.x, ys = start.y, ye = start.y + size.y;


            // 给提前了。
            if (icon_data.Type == MapIconType.P2)
            {
                // 为P2加方案，把P2制造的迷雾给消了
                for (int y = ys; y < ye; y++)
                    for (int x = xs; x < xe; x++)
                    {
                        var sData = small_map[x, y];
                        if (sData == PixType.Fog)
                            foreach (var off in fog_constant)
                            {
                                // 与下文处理一致
                                var mData = _map[x + small_map_pos.x + off.x, y + small_map_pos.y + off.y];
                                if (mData == PixType.ObstacleByBig || mData == PixType.Empty)
                                    small_map[x + off.x, y + off.y] = PixType.EmptyTemp;
                                else
                                    small_map[x + off.x, y + off.y] = mData;
                            }
                    }
            }


            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var mData = _map[x + small_map_pos.x, y + small_map_pos.y];
                    if (mData == PixType.ObstacleByBig || mData == PixType.Empty)
                        small_map[x, y] = PixType.EmptyTemp;
                    else
                        small_map[x, y] = mData;

                }

        }



        /// <summary>
        /// 动态Icon不影响_map/_small_map ，只有寻路作用、展示用途
        /// </summary>
        void ClearDynamicIcon(Vector2Int small_map_pos)
        {
            // 先删掉小地图显示区域的所有Icon, 后续再增添在区域内的Icon

            // x_start/x_end/y_start/y_end
            int xs = 21 + small_map_pos.x, xe = 179 + small_map_pos.x
            , ys = 21 + small_map_pos.y, ye = 179 + small_map_pos.y;

            for (int i = 0; i < _icon_datas_dynamic.Count; i++)
            {
                var type = _icon_datas_dynamic[i];
                MapIconData icon_data = _icon_datas[(int)type];
                var insts = icon_data.InstList;

                if (type == MapIconType.P1 || type == MapIconType.P2)       // 人物直接清空
                {
                    insts.Clear();
                }
                else
                {
                    bool needClear = false;
                    for (int j = 0; j < insts.Count; j++)
                    {
                        var pos = insts[j];
                        if (pos.x >= xs && pos.x < xe && pos.y >= ys && pos.y < ye)
                        {
                            insts[j] = Utils.DefaultV2I;
                            needClear = true;
                        }
                    }
                    if (needClear)
                    {
                        var newlist = new List<Vector2Int>();
                        foreach (var k in insts)
                            if (k.x >= 0)
                                newlist.Add(k);
                        icon_data.InstList = newlist;
                    }
                }

            }
        }

        #endregion


        Mat GetTemplateMat(PixType[,] small_map, int length)
        {

            int x_start = (_rectW - length) / 2;
            int y_start = (_rectH - length) / 2;

            byte[] source_bytes = new byte[length * length];

            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    var map_data = small_map[j + x_start, i + y_start];
                    if (map_data == PixType.ObstacleEdge)
                        source_bytes[i * length + j] = 255;
                }

            // 删除新增边界，用16扇区法检测迷雾，有迷雾则丢弃
            int xs = x_start, xe = xs + length, ys = y_start, ye = ys + length;
            var icon_data = GetIconData(MapIconType.P1);
            if (icon_data.AddList.Count > 0)
            {
                var p1_pos = icon_data.AddList[0];
                var sectors = RecogUtil.GetDivinedSectors16();
                for (int i = 0; i < sectors.Count; i++)
                {
                    bool has_fog = false;
                    var sector = sectors[i];
                    for (int j = 0; j < sector.Count; j++)
                    {
                        var p = sector[j] + p1_pos;
                        if (p.x < 0 || p.x >= _rectW || p.y < 0 || p.y >= _rectH)
                            continue;

                        var data = small_map[p.x, p.y];
                        if (data == PixType.Fog || data == PixType.FogArea)
                        {
                            has_fog = true;
                            break;
                        }
                    }

                    if (has_fog)
                        for (int j = 0; j < sector.Count; j++)
                        {
                            var p = sector[j] + p1_pos;
                            if (p.x < xs || p.x >= xe || p.y < ys || p.y >= ye)
                                continue;

                            int index = (p.y - ys) * length + p.x - xs;
                            source_bytes[index] = 0;
                        }
                }

            }

            Mat source = Mat.FromPixelData(length, length, MatType.CV_8UC1, source_bytes);

            // @debug
            // using (Mat flipped = new Mat())
            // {
            //     // 使用 Cv2.Flip 方法，参数 0 表示上下翻转
            //     Cv2.Flip(source, flipped, 0);
            //     var save_path = IU.PicPath + $"/SmallMapDebug/pic_{_frameCount}_{length}.png";
            //     IU.SaveMat(flipped, save_path);
            // }
            return source;
        }

        #region EdgeGap


        void ApplyObstacleEdgeGap(List<Vector2Int> endpoint_list, List<Vector2Int> gap_list
                               , Vector2Int small_map_pos)
        {
            for (int i = 0; i < endpoint_list.Count; i++)
                endpoint_list[i] = endpoint_list[i] + small_map_pos - new Vector2Int(2, 2);
            _pixRecord[PixRecordType.ObstacleEdgeEndPoint] = endpoint_list;


            var gap_list_total_record = _pixRecord[PixRecordType.ObstacleEdgeGapTotal];
            var applys = new List<Vector2Int>();
            var frame = new List<Vector2Int>();

            for (int i = 0; i < gap_list.Count; i++)
            {
                var mPos = gap_list[i] + small_map_pos - new Vector2Int(2, 2);
                var mData = _map[mPos.x, mPos.y];
                frame.Add(mPos);
                if (mData != PixType.ObstacleEdge || !_confirm_map[mPos.x, mPos.y])
                {
                    _map[mPos.x, mPos.y] = PixType.ObstacleEdge;
                    _confirm_map[mPos.x, mPos.y] = true;
                    applys.Add(mPos);
                    gap_list_total_record.Add(mPos);
                }
            }

            _pixRecord[PixRecordType.ObstacleEdgeGap] = frame;
            _gridData.Apply(applys);
            _gridData.ApplyFog(applys);
        }

        #endregion

        #region InitEmpty



        /// <summary>
        /// 测试过没问题。用上一帧的图盖掉角色图标。
        /// 尽量避免漏风——装备条、游戏错误
        /// 降低漏风的损失。那就是取角色旁边的一个空地像素。
        /// </summary>
        void InitEmpty(PixType[,] colorData, Vector2Int small_map_pos)
        {
            // 地图需要初始的empty地块，取P1 Icon为初始的empty地块。
            // 后续靠"已确定的empty地块"的连通性，来扩充empty地块

            var p1_pos = FindPlayerPos(MapIconType.P1, out _);
            if (!_has_init_empty)
            {
                if (p1_pos.x > 0)
                {
                    _has_init_empty = StartInitEmpty(colorData, p1_pos);
                }
                else
                {
                    // 只能等待P1出现， 通知外部去"随机移动P1"


                }
            }


            var record2 = _pixRecord[PixRecordType.ObstacleEdgeGapTotal];

            foreach (var p in record2)
            {
                int xs = small_map_pos.x, ys = small_map_pos.y,
                xe = xs + _rectW, ye = ys + _rectH;

                if (p.x >= xs && p.x < xe && p.y >= ys && p.y < ye)
                {
                    colorData[p.x - xs, p.y - ys] = PixType.ObstacleEdge;
                }
            }

            // 计算小地图的 Empty
            ColorToDataTraversalEmpty(colorData, small_map_pos);


            // 1.还原CellType.EmptyTemp   2.用上一帧的边缘块，检查本帧的情况
            //
            // if (!start_frame)
            // {
            //     var move = _move_delta;
            //     var new_empty_count = 0;
            //     _edge_new_empty = new List<Vector2Int>();

            //     foreach (var pos in apply_edge_constant)
            //     {
            //         var x = pos.x - move.x;
            //         var y = pos.y - move.y;
            //         if (x < 0 || x >= _rectW || y < 0 || y >= _rectH)
            //             continue;
            //         var data = colorData[pos.x - move.x, pos.y - move.y];
            //         var mData = _map[x + small_map_pos.x, y + small_map_pos.y];
            //         if (data == PixType.Empty && mData != PixType.Empty && mData != PixType.ObstacleByBig)
            //         {
            //             _edge_new_empty.Add(new Vector2Int(x, y));
            //             new_empty_count++;
            //         }
            //     }

            //     DU.LogWarning($"第{FrameCount}帧 新EdgeEmpty数量：{new_empty_count}");
            //     if (new_empty_count > 60)
            //     {
            //         SaveErrorSmallMap($"{new_empty_count}");
            //         return false;
            //     }
            // }

            for (int y = 0; y < _rectH; y++)
                for (int x = 0; x < _rectW; x++)
                {
                    var data = colorData[x, y];
                    if (data == PixType.EmptyTemp)
                        colorData[x, y] = PixType.Undefined;
                }
        }

        /// <summary>
        /// 计算总图的初始空地。返回是否成功。根据P1位置以及它周围情况
        /// </summary>
        bool StartInitEmpty(PixType[,] colorData, Vector2Int p1_pos)
        {
            var p1x = p1_pos.x;
            var p1y = p1_pos.y;


            List<Vector2Int> pixList = new List<Vector2Int>();

            bool has_obstacle = false;
            bool has_empty = false;
            PixType[,] temp = new PixType[7, 7];

            int xs = p1x - 3 - _small_map_pos.x, xe = p1x + 3 - _small_map_pos.x
            , ys = p1y - 3 - _small_map_pos.y, ye = p1y + 3 - _small_map_pos.y;
            for (int py = ys; py <= ye; py++)
                for (int px = xs; px <= xe; px++)
                {
                    var data = colorData[px, py];
                    if (data == PixType.ObstacleEdge)
                        has_obstacle = true;
                    else if (data == PixType.EmptyTemp)
                        has_empty = true;
                    temp[px - xs, py - ys] = colorData[px, py];
                }

            if (!has_empty)
                return false;

            if (has_obstacle)
            {
                // P1中心与边界至少会距离1px，P1颜色才不会被污染，从而识别
                // 当然也会有选错的情况，这种情况就是被其他角色包围
                List<Vector2Int> max_empty_list = null;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                    {
                        if (temp[x, y] == PixType.EmptyTemp)
                        {
                            var v2 = new Vector2Int(x, y);
                            var empty_list = TraverseAndTransform(temp, v2, PixType.EmptyTemp, PixType.Undefined);
                            if (max_empty_list == null || empty_list.Count > max_empty_list.Count)
                            {
                                max_empty_list = empty_list;
                            }
                        }
                    }

                foreach (var p in max_empty_list)
                {
                    var p0 = p1_pos + p - new Vector2Int(3, 3);
                    _map[p0.x, p0.y] = PixType.Empty;
                    _confirm_map[p0.x, p0.y] = true;
                    pixList.Add(p0);
                }
            }
            else
                for (int py = p1y - 2; py <= p1y + 2; py++)
                    for (int px = p1x - 2; px <= p1x + 2; px++)
                    {
                        _map[px, py] = PixType.Empty;
                        _confirm_map[px, py] = true;
                        pixList.Add(new Vector2Int(px, py));
                    }


            _gridData.Apply(pixList);    // 最好移到 结尾一并处理。
            return true;
        }


        List<Vector2Int> GetP1ArriveList()
        {
            var result = new List<Vector2Int>();
            var pos_count = P1PosRecord.Count;

            for (int i = pos_count - 2; i >= 0; i--)
            {
                var pos = P1PosRecord[i];
                if (pos.x < 0)
                    continue;
                if (result.Count >= 5)
                    break;

                bool fit = true;
                foreach (var p in result)
                    if (Math.Abs(pos.x - p.x) <= 3 && Math.Abs(pos.y - p.y) <= 3)
                    {
                        fit = false; break;
                    }

                if (fit)
                    result.Add(pos);
            }

            // if (result.Count == 0 && pos_count > 0)
            // {
            //     result.Add(P1PosRecord[pos_count - 1]);
            // }

            return result;

        }


        void ColorToDataTraversalEmpty(PixType[,] colorData, Vector2Int small_map_pos)
        {

            _stack1.Clear();

            // foreach (var pos in first_list)
            // {
            //     _stack[count++] = pos;
            //     colorData[pos.x, pos.y] = PixType.Empty;
            // }
            var list = GetP1ArriveList();

            if (list.Count < 5)
            {
                int empty_count = 0;
                for (int y = 0; y < _rectH; y++)
                    for (int x = 0; x < _rectW; x++)
                    {
                        int px = x + small_map_pos.x, py = y + small_map_pos.y;
                        var data = _map[px, py];
                        if (_confirm_map[px, py] && (data == PixType.Empty || data == PixType.ObstacleByBig))
                        {
                            _stack1.Add(new Vector2Int(x, y));         // 这是等价于函数耗时
                            colorData[x, y] = PixType.Empty;
                            empty_count++;
                        }
                    }
                if (empty_count == 0)
                {
                    TraversalEmptyInit(colorData, small_map_pos, list, _stack1);
                }
            }
            else
            {
                TraversalEmptyInit(colorData, small_map_pos, list, _stack1);
            }


            _debug_small_map = new PixType[200, 200];
            Array.Copy(_small_map, _debug_small_map, 40000);


            // 连通性遍历，范围要控制在无"游离图标"的中心区。因为有些图标会用"淡黑色"覆盖边界，造成漏缝
            int xs = 21, xe = 178, ys = 21, ye = 178;
            while (_stack1.Count > 0)
            {
                var pop = _stack1.Pop();
                int x = pop.x;
                int y = pop.y;


                if (y > ys && colorData[x, y - 1] == PixType.EmptyTemp)
                {
                    colorData[x, y - 1] = PixType.Empty;
                    _stack1.Add(new Vector2Int(x, y - 1));
                }
                if (x > xs && colorData[x - 1, y] == PixType.EmptyTemp)
                {
                    colorData[x - 1, y] = PixType.Empty;
                    _stack1.Add(new Vector2Int(x - 1, y));
                }
                if (y < ye && colorData[x, y + 1] == PixType.EmptyTemp)
                {
                    colorData[x, y + 1] = PixType.Empty;
                    _stack1.Add(new Vector2Int(x, y + 1));
                }
                if (x < xe && colorData[x + 1, y] == PixType.EmptyTemp)
                {
                    colorData[x + 1, y] = PixType.Empty;
                    _stack1.Add(new Vector2Int(x + 1, y));
                }
            }

        }

        void TraversalEmptyInit(PixType[,] colorData, Vector2Int small_map_pos, List<Vector2Int> list, SList<Vector2Int> stack)
        {
            int empty_count = 0;
            foreach (var p in list)
            {
                var sx = p.x - small_map_pos.x;
                var sy = p.y - small_map_pos.y;
                if (sx > 5 && sx < 195 && sy > 5 && sy < 195)
                {
                    for (int y = sy - 2; y <= sy + 2; y++)
                        for (int x = sx - 2; x <= sx + 2; x++)
                        {
                            int px = x + small_map_pos.x, py = y + small_map_pos.y;
                            if (_map[px, py] == PixType.Empty && colorData[x, y] != PixType.ObstacleEdge)
                            {
                                stack[stack.Count++] = new Vector2Int(x, y);
                                colorData[x, y] = PixType.Empty;
                                empty_count++;
                            }
                        }
                }
            }

            if (empty_count == 0)
            {
                SetError(2);
            }
        }

        List<Vector2Int> TraverseAndTransform(PixType[,] map, Vector2Int start, PixType condition, PixType transform_result)
        {
            var result = new List<Vector2Int>();
            result.Add(start);

            var count = 0;
            var x_end = map.GetLength(0) - 1;
            var y_end = map.GetLength(1) - 1;

            map[start.x, start.y] = transform_result;
            _stack[count++] = start;

            while (count > 0)
            {
                var pop = _stack[--count];
                int x = pop.x;
                int y = pop.y;

                if (y > 0 && map[x, y - 1] == condition)
                {
                    map[x, y - 1] = transform_result;
                    var near = new Vector2Int(x, y - 1);
                    _stack[count++] = near;
                    result.Add(near);
                }
                if (x > 0 && map[x - 1, y] == condition)
                {
                    map[x - 1, y] = transform_result;
                    var near = new Vector2Int(x - 1, y);
                    _stack[count++] = near;
                    result.Add(near);
                }
                if (y < y_end && map[x, y + 1] == condition)
                {
                    map[x, y + 1] = transform_result;
                    var near = new Vector2Int(x, y + 1);
                    _stack[count++] = near;
                    result.Add(near);
                }
                if (x < x_end && map[x + 1, y] == condition)
                {
                    map[x + 1, y] = transform_result;
                    var near = new Vector2Int(x + 1, y);
                    _stack[count++] = near;
                    result.Add(near);
                }
            }

            return result;
        }

        // void InitApplyEdgeList()
        // {
        //     apply_edge_constant = new List<Vector2Int>();
        //     int xs = 21, xe = 178, ys = 21, ye = 178;
        //     for (int y = ys; y <= ye; y++)
        //     {
        //         apply_edge_constant.Add(new Vector2Int(xs, y));
        //         apply_edge_constant.Add(new Vector2Int(xe, y));
        //     }
        //     for (int x = xs; x <= xe; x++)
        //     {
        //         apply_edge_constant.Add(new Vector2Int(x, ys));
        //         apply_edge_constant.Add(new Vector2Int(x, ye));
        //     }
        // }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        bool InEllipse(int x, int y)
        {
            var result = x * x * ellipseRadiusSquare.y + y * y * ellipseRadiusSquare.x <= ellipseRadiusSquare.z;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // 告诉编译器要内联
        public static bool Between(Color32 val, Color32 min, Color32 max)
        {
            return val.r >= min.r && val.g >= min.g && val.b >= min.b
                && val.r <= max.r && val.g <= max.g && val.b <= max.b;
        }


        #endregion

        void UpdateMapSize(Vector2Int small_map_pos)
        {
            var x_start = small_map_pos.x;
            var y_start = small_map_pos.y;
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

            _w = _xRange.y - _xRange.x + 1;
            _h = _yRange.y - _yRange.x + 1;
            _range_zero = new Vector2Int(_xRange.x, _yRange.x);
        }


        #region Apply

        /// <summary>
        /// 把一张小地图，更新到大地图中
        /// 大地图中，边界永久保留，非“边界”实时更新
        /// 更新原点
        /// </summary>
        void Apply(PixType[,] small_map)
        {
            // DU.RunWithTimer(() =>
            // {

            ApplyJudgeMap(small_map, out var emptyChanges, out var fogChanges);
            // CheckLightMap();
            _gridData.Apply(emptyChanges);
            _gridData.ApplyFog(fogChanges);

            // }, "Apply");
        }

        public void GetContentAttr(out Vector2Int xRange, out Vector2Int yRange, out int w, out int h)
        {
            xRange = _xRange;
            yRange = _yRange;
            w = _w;
            h = _h;
        }

        #region Rebuild

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

                PixType[,] new_map = new PixType[_mapEdge, _mapEdge];
                LightUnion[,] new_light_map = new LightUnion[_mapEdge, _mapEdge];
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



        }


        void ApplyOffset(Vector2Int offset)
        {
            var bigGrid_off = offset / 5;

            _fixed_ref_pos += offset;
            _small_map_pos += offset;
            _judge_map_pos += offset;
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

            // 改Icon坐标
            for (int i = 0; i < _icon_datas.Length; i++)
            {
                var icon = _icon_datas[i];
                if (icon == null)
                    continue;
                var pos_list = icon.InstList;
                var len = pos_list.Count;
                for (int j = 0; j < len; j++)
                    pos_list[j] += offset;
            }

            for (int i = 0; i < P1PosRecord.Count; i++)
            {
                var pos = P1PosRecord[i];
                P1PosRecord[i] = pos + offset;
            }


            _DoFindFog_pos += offset;

        }

        #endregion

        #endregion

        #region judge_map
        void ApplyJudgeMap(PixType[,] small_map, out List<Vector2Int> emptyChanges,
                            out List<Vector2Int> fogChanges)
        {
            // "迷雾"更新和"空地"更新会被收录。
            emptyChanges = _reusedList1;
            fogChanges = _reusedList2;
            emptyChanges.Clear();
            fogChanges.Clear();

            int s_len = 158;
            int j_len = 300;
            int delta_len = j_len - s_len;


            int xs = 21, xe = 179, ys = 21, ye = 179;   // x_start/x_end/y_start/y_end
            int max_delta = j_len - 1 - xe;
            int min_delta = -xs;


            // 重建机制。策略：超界就居中策略，达到次数平衡，另外5帧位移不超过50px
            if (_small_map_pos.x - _judge_map_pos.x > max_delta || _small_map_pos.x - _judge_map_pos.x < min_delta
            || _small_map_pos.y - _judge_map_pos.y > max_delta || _small_map_pos.y - _judge_map_pos.y < min_delta)
            {
                var old = _judge_map_pos;
                _judge_map_pos = _small_map_pos - new Vector2Int(delta_len / 2, delta_len / 2);
                var off = _judge_map_pos - old;                  //offset

                var jLen = _judge_map.GetLength(0);

                var temp = new JudgePix[jLen, jLen];
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

            Vector2Int offset = _small_map_pos - _judge_map_pos;
            ApplyEdgeFog(small_map, fogChanges);   // 补充边缘迷雾。  <搜寻处于迷雾中的目标>需要

            // 将中间的[158X158]区域写入。这部分是确定的

            // 主逻辑：[158X158]范围
            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var px = x + _small_map_pos.x;
                    var py = y + _small_map_pos.y;
                    var mData = _map[px, py];

                    _light_map[px, py].Access = true;

                    // 待定状态可以往下走。当同个地块被识别为"空地"或"边界"次数足够多，就变为明确状态，
                    //
                    bool confirm = _confirm_map[px, py];
                    if (confirm)
                        continue;

                    var sData = small_map[x, y];
                    var jx = x + offset.x;
                    var jy = y + offset.y;
                    var jData = _judge_map[jx, jy];

                    if (sData == PixType.Fog || sData == PixType.FogArea)
                    {
                        _map[px, py] = PixType.Fog;
                        fogChanges.Add(new Vector2Int(px, py));
                        _judge_map[jx, jy] = default;               // 重置
                        continue;
                    }

                    // 迷雾消失
                    if (mData == PixType.Fog)
                    {
                        _map[px, py] = PixType.Undefined;
                        fogChanges.Add(new Vector2Int(px, py));
                    }


                    // 如果 空地和边界未开始计数，则Undefined无效
                    if (sData == PixType.Undefined && jData.empty_num == 0
                        && jData.obstacle_edge_num == 0)
                        continue;


                    if (sData == PixType.Undefined)
                        jData.undefined_num++;
                    else if (sData == PixType.Empty)
                        jData.empty_num++;
                    else if (sData == PixType.ObstacleEdge)
                        jData.obstacle_edge_num++;


                    if (jData.CheckFull(out PixType result))
                    {
                        // @debug
                        // if (px == 113 + _xRange.x && py == 100 + _yRange.x)
                        // {
                        //     var a = 0;
                        // }
                        jData.undefined_num = 0; jData.empty_num = 0; jData.obstacle_edge_num = 0;
                        _judge_map[jx, jy] = jData;
                        _map[px, py] = result;

                        if (result != PixType.Undefined)
                            _confirm_map[px, py] = true;

                        if (result == PixType.Empty)
                            emptyChanges.Add(new Vector2Int(px, py));

                    }
                    else
                    {
                        var ori_type = _judge_map[jx, jy].GuessType();
                        var new_type = jData.GuessType();
                        _judge_map[jx, jy] = jData;
                        _map[px, py] = new_type;
                        bool refresh = false;

                        if (ori_type != PixType.Empty && new_type == PixType.Empty)
                            refresh = true;
                        if (ori_type == PixType.Empty && new_type != PixType.Empty)
                            refresh = true;

                        if (refresh)
                            emptyChanges.Add(new Vector2Int(px, py));
                    }

                }

            // [158X158]以外区域只写入边界, Undefine不能写入。这部分是不确定的
            for (int y = 0; y < _rectH; y++)
                for (int x = 0; x < _rectW; x++)
                {
                    if (x >= xs && x < xe && y >= ys && y < ye)
                        continue;
                    var px = x + _small_map_pos.x;
                    var py = y + _small_map_pos.y;


                    bool confirm = _confirm_map[px, py];
                    if (confirm)
                        continue;

                    var mData = _map[px,py];
                    var sData = small_map[x, y];
                    // 不改Fog和Empty，Fog会关系到寻路，Empty改了的话就要同时频繁的改_grid
                    if (sData == PixType.ObstacleEdge && mData != PixType.Fog && mData != PixType.Empty)
                    {
                        _map[px,py] = PixType.ObstacleEdge;
                    }
                }

        }

        // 补充边缘迷雾
        // 补充迷雾：[160X160]的最外层厚度1px像素。若(与空地相连)&& ，则标为迷雾
        // _light_map ：视野刷子区域可能比 _map区域外扩1px
        void ApplyEdgeFog(PixType[,] small_map, List<Vector2Int> fogChanges)
        {
            Vector2Int offset = _small_map_pos - _judge_map_pos;

            if (EdgeFogList == null)
            {
                EdgeFogList = new List<(Vector2Int, Vector2Int)>();

                int xs = 21, xe = 178, ys = 21, ye = 178;
                for (int y = ys; y <= ye; y++)
                {
                    EdgeFogList.Add((new Vector2Int(20, y), new Vector2Int(1, 0)));
                    EdgeFogList.Add((new Vector2Int(179, y), new Vector2Int(-1, 0)));

                }
                for (int x = xs; x <= xe; x++)
                {
                    EdgeFogList.Add((new Vector2Int(x, 20), new Vector2Int(0, 1)));
                    EdgeFogList.Add((new Vector2Int(x, 179), new Vector2Int(0, -1)));
                }
            }

            foreach (var tuple in EdgeFogList)
            {
                var p = tuple.Item1;
                var relation = tuple.Item2;
                var mx = p.x + _small_map_pos.x;
                var my = p.y + _small_map_pos.y;
                var inside = small_map[p.x + relation.x, p.y + relation.y];
                var lightData = _light_map[mx, my];
                var confirm = _confirm_map[mx, my];
                // if(外圈像素没访问过，邻近的内圈像素是空地或边界)
                // {外圈像素置为迷雾}
                //
                if ((inside == PixType.Empty || inside == PixType.ObstacleEdge) && !confirm && !lightData.Access)
                {
                    lightData.Access = true;
                    _light_map[mx, my] = lightData;
                    _map[mx, my] = PixType.Fog;
                    fogChanges.Add(new Vector2Int(mx, my));
                }
            }

        }

        #endregion


        #region light_map

        void ApplyLightMap(PixType[,] small_map, Vector2Int small_map_pos)
        {
            var p1_pos = FindPlayerPos(MapIconType.P1, out _);

            if (p1_pos.x > 0)
            {
                int xs = p1_pos.x - ellipseRadius.x, xe = p1_pos.x + ellipseRadius.x,
                    ys = p1_pos.y - ellipseRadius.y, ye = p1_pos.y + ellipseRadius.y;

                for (int y = ys; y <= ye; y++)
                    for (int x = xs; x <= xe; x++)
                    {
                        if (InEllipse(x - p1_pos.x, y - p1_pos.y))
                        {
                            _light_map[x, y].LightType = LightType.Light;
                        }
                    }
            }


            for (int y = 0; y < _rectH; y++)
                for (int x = 0; x < _rectW; x++)
                {
                    var sData = small_map[x, y];
                    int mx = x + small_map_pos.x, my = y + small_map_pos.y;
                    var light = _light_map[mx, my];
                    if (sData == PixType.Fog || sData == PixType.FogArea)
                    {
                        if (light.LightType == LightType.Light)
                        {
                            small_map[x, y] = PixType.Undefined;
                        }
                    }
                }
        }

        /// <summary>
        /// 选5张light图。 p3 p4在后面跟着 p1 p2,p1p2在它们的迷雾中。 p1 p2突然停下堵住通道。这种情况会封住
        /// 甚至在迷雾中爆了一件物品，icon堵住通道，探明后被认定堵死。
        /// 解决办法：排除p3 p4当前的light范围就行。就是不能包含当前的椭圆。
        /// 解决办法2：p1 p2开启功能，p3 p4关闭此功能。 但是对boss和item堵通道没办法。
        /// </summary>
        void CheckLightMap()
        {
            _light_count++;
            if (_light_count < 5) return;
            _light_count = 0;

            var len = P1PosRecord.Count;
            if (len < 9) return;

            int l_min_x = int.MaxValue;
            int l_max_x = 0;
            int l_min_y = int.MaxValue;
            int l_max_y = 0;
            // 5个椭圆中心点，坐标基于（light_start为原点的坐标系）
            List<Vector2Int> center_list = new List<Vector2Int>();

            // for (int i = 0; i < 9; i++)
            // {
            //     var move = MoveRecord[len - 1 - i];
            //     center = center - move;
            //     if (i >= 4)
            //     {
            //         center_list[i - 4] = center;
            //         l_min_x = Mathf.Min(l_min_x, center.x);
            //         l_max_x = Mathf.Max(l_max_x, center.x);
            //         l_min_y = Mathf.Min(l_min_y, center.y);
            //         l_max_y = Mathf.Max(l_max_y, center.y);
            //     }
            // }

            for (int i = P1PosRecord.Count - 5; i >= 0; i--)
            {
                var pos = P1PosRecord[i];
                if (pos.x < 0)
                    continue;
                if (center_list.Count >= 5)
                    break;
                center_list.Add(pos);
                l_min_x = Mathf.Min(l_min_x, pos.x);
                l_max_x = Mathf.Max(l_max_x, pos.x);
                l_min_y = Mathf.Min(l_min_y, pos.y);
                l_max_y = Mathf.Max(l_max_y, pos.y);
            }

            if (center_list.Count < 5)
                return;


            l_min_x -= ellipseRadius.x;
            l_max_x += ellipseRadius.x;
            l_min_y -= ellipseRadius.y;
            l_max_y += ellipseRadius.y;
            _light_range[0] = new Vector2Int(l_min_x - 2, l_min_y - 2);
            _light_range[1] = new Vector2Int(l_max_x + 2, l_max_y + 2);


            var fourDir = Utils.FourDirList;
            var light_start = _light_range[0];
            var light_end = _light_range[1];

            var eList = new List<Vector2Int>();     // 边缘列表
            for (int i = 0; i < 5; i++)             // 统一化
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


            // 计算并标记 光照的边框 并且与Undef相连。
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
                                 && mData == PixType.Undefined)
                            {
                                var pos = new Vector2Int(x + off.x, y + off.y);
                                tMap[pos.x, pos.y] = LightType.FreshEdge;
                                eList.Add(pos);
                            }
                        }
                    }
                }


            // 遍历，分类Undef。
            // 拥有边框像素达到30才能算数。
            //
            foreach (var pos in eList)
            {
                var result = LightMapTraversal(pos, out int edge_count);
                // DU.LogWarning($"edge_count：{edge_count}");
                if (edge_count > 30)
                {
                    DrawLightMapObstacleEdge(result, light_start);
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

                int near_empty_count = 0;

                foreach (Vector2Int off in fourDir)
                {
                    Vector2Int ntp = new Vector2Int(x + off.x, y + off.y);
                    var lData = tMap[ntp.x, ntp.y];
                    if (lData == LightType.FreshEdge)
                        edge_count++;
                    if (lData == LightType.Fresh || lData == LightType.FreshEdge)
                    {
                        var px = ntp.x + light_start.x;
                        var py = ntp.y + light_start.y;
                        var data = _map[px, py];
                        // 暗边界 当做Undefined
                        //
                        if (data == PixType.Undefined || (!_confirm_map[px, py] && data == PixType.ObstacleEdge))
                        {
                            tMap[ntp.x, ntp.y] = LightType.FreshVisited;
                            _stack[count++] = ntp;
                        }
                        else if (data == PixType.Empty || data == PixType.ObstacleByBig)
                        {
                            near_empty_count++;
                        }
                    }
                }

                if (near_empty_count > 0)
                {
                    tMap[x, y] = LightType.TempEdge;
                    result.Add(pop);
                }
            }
            return result;
        }
        Vector2Int[] eightDirOrder = new Vector2Int[]{
                                new Vector2Int(-1, -1),
                                new Vector2Int(-1, 1),
                                new Vector2Int(1, -1),
                                new Vector2Int(1, 1),
                                new Vector2Int(-1, 0),
                                new Vector2Int(0, 1),
                                new Vector2Int(1, 0),
                                new Vector2Int(0, -1),
                            };

        void DrawLightMapObstacleEdge(List<Vector2Int> list, Vector2Int light_start)
        {
            var tMap = _light_cal_map;  //target

            var record = _pixRecord[PixRecordType.ObstacleEdgeOfLight];


            // @debug
            //
            // foreach (var line_tp in list)
            // {
            //     var p = line_tp + light_start;
            //     record.Add(p);
            //     _map[p.x, p.y] = PixType.ObstacleEdge;
            //     _confirm_map[p.x, p.y] = true;
            // }

            // 最靠谱的方案：距离寻路。比连通性寻路更严谨。
            // 还有个方案：遍历TempEdge，8向找边界地块。每次边界地块只保留相距最远的。
            // 还要给"光照障碍"分类, 互相连通(8向)的是同类。每类再首尾相连就是"光照边界"
            //
            foreach (var tp in list)
            {
                if (tMap[tp.x, tp.y] == LightType.TempEdgeVisited)
                    continue;

                List<Vector2Int> end_points = new List<Vector2Int>();
                var count = 0;
                tMap[tp.x, tp.y] = LightType.TempEdgeVisited;
                _stack[count++] = tp;


                while (count > 0)
                {
                    var pop = _stack[--count];
                    int x = pop.x;
                    int y = pop.y;

                    foreach (Vector2Int off in eightDirOrder)
                    {
                        Vector2Int ntp = new Vector2Int(x + off.x, y + off.y);
                        var lData = tMap[ntp.x, ntp.y];
                        if (lData == LightType.TempEdge)
                        {
                            tMap[ntp.x, ntp.y] = LightType.TempEdgeVisited;
                            _stack[count++] = ntp;
                        }

                        var mp = new Vector2Int(ntp.x + light_start.x, ntp.y + light_start.y);
                        var mData = _map[mp.x, mp.y];
                        if (mData == PixType.ObstacleEdge)
                        {
                            if (end_points.Count < 2)
                                end_points.Add(ntp);
                            else
                                DrawLightMapJudge(end_points, ntp);
                        }
                    }

                }

                if (end_points.Count == 0)
                    continue;

                List<Vector2Int> line = null;
                if (end_points.Count > 1)
                    line = GetLineBresenham(end_points[0], end_points[1]);
                else if (end_points.Count == 1)
                    line = new List<Vector2Int>() { end_points[0] };

                if (line.Count < 20)
                {
                    foreach (var line_tp in line)
                    {
                        var p = line_tp + light_start;
                        record.Add(p);
                        _map[p.x, p.y] = PixType.ObstacleEdge;
                        _confirm_map[p.x, p.y] = true;
                    }
                }

            }

        }

        /// <summary>
        /// end_points有2个点，加上p，3个点挑距离最大的2个点，再写入end_points中
        /// </summary>
        void DrawLightMapJudge(List<Vector2Int> end_points, Vector2Int p)
        {
            var p0 = end_points[0];
            var p1 = end_points[1];
            var d = (p1 - p0).sqrMagnitude;
            var d0 = (p - p0).sqrMagnitude;
            var d1 = (p - p1).sqrMagnitude;
            if (d >= d0 && d >= d1)
                return;
            else if (d1 >= d && d1 >= d0)
                end_points[0] = p;
            else if (d0 >= d && d0 >= d1)
                end_points[1] = p;

        }



        #endregion


        // 这时地图UI视角
        public BigCellPathResult StartAStarBigGrid(Vector2Int start, Vector2Int target)
        {
            var offset = new Vector2Int(_xRange.x, _yRange.x);
            return StartAStarBigGridAbs(start + offset, target + offset);
        }

        #region 寻路-目标



        // 绝对坐标 absolute
        public BigCellPathResult StartAStarBigGridAbs(Vector2Int find_start, Vector2Int find_target, float avoid_factor = 1.2f)
        {
            BigCellPathResult path_obj = new BigCellPathResult();
            path_obj.Status = PathFindingResult.Failed;

            if (find_start.x < 0 || find_target.x < 0)
                return path_obj;

            _find_target_in_empty = _map[find_target.x, find_target.y] == PixType.Empty;
            if (_find_target_in_empty)
            {
                path_obj = _gridData.StartAStar(find_start, find_target, avoid_factor);
            }
            else
            {
                // 迷雾实时寻路
                // 1.沿着目标到起点的直线开始遍历，直到遇到非Undefined地块，设为点K  （Bresenham）
                // - 点K可以是"空地" 或 "迷雾" 或 "边界"。
                // - "空地"跳到第4步，"迷雾"跳到第3步，"边界"跳到第2步
                // 2.从K点开始"边界"连通性递归，按距离顺序，直到遇到"迷雾"T
                // 3.从T点开始"迷雾"连通性递归，按距离顺序，直到遇到"空地"E
                // 4.从起点到"空地"E的寻路。
                var change_target = Utils.DefaultV2I;      //_find_aStar_target 是作为寻路成功否的标识
                _pixRecord[PixRecordType.AreaOfFindNearestFog].Clear();

                Vector2Int pointK = GetTileFromLine(find_start, find_target);
                if (pointK == Utils.DefaultV2I)
                {
                    DU.LogWarning("[BresenhamGetCross] 无法找到交点K");
                    return path_obj;
                }


                PixType typeK = _map[pointK.x, pointK.y];
                if (typeK == PixType.Empty)
                {
                    change_target = pointK;
                }
                else
                {
                    Vector2Int pointT = default;
                    if (typeK == PixType.ObstacleEdge)
                    {
                        pointT = EdgeTraversal(pointK);
                        if (pointT == Utils.DefaultV2I)
                        {
                            DU.LogWarning("[EdgeTraversal] 无法找到邻近Fog T");
                            return path_obj;
                        }
                    }
                    else if (typeK == PixType.Fog)
                    {
                        pointT = pointK;
                    }

                    change_target = FogTraversal(pointT);
                    if (change_target == Utils.DefaultV2I)
                    {
                        DU.LogWarning("[FogTraversal] 无法找到邻近 empty E");
                        return path_obj;
                    }
                }


                // 寻路
                path_obj = _gridData.StartAStar(find_start, change_target, avoid_factor);
            }

            return path_obj;

        }


        /// <summary>
        /// 玩家 => 玩家 寻路
        /// </summary>

        public BigCellPathResult PlayerToPlayerAStar(Vector2Int from, Vector2Int to, float avoid_factor)
        {
            // 两个点位，把Player图标置为Empty
            if (from.x > 0)
                SimModifyMap(from);
            // 如果to完全在迷雾中，那么就不置为Empty，
            if (to.x > 0 && PlayerIconHasEmpty(to))
                SimModifyMap(to);

            var PathObj = StartAStarBigGridAbs(from, to, avoid_factor);
            return PathObj;
        }

        /// <summary>
        /// 玩家 => 地块 寻路
        /// </summary>
        public BigCellPathResult PlayerToTileAStar(Vector2Int from, Vector2Int to, float avoid_factor)
        {
            // 两个点位，把Player图标置为Empty
            if (from.x > 0)
                SimModifyMap(from);

            var PathObj = StartAStarBigGridAbs(from, to, avoid_factor);
            return PathObj;
        }


        /// <summary>
        /// 玩家 => 找最近迷雾。能识别出P1 => 必定能转为空地 => 必定能寻路
        /// </summary>
        public BigCellPathResult PlayerFindFog(Vector2Int from)
        {
            // 两个点位，把Player图标置为Empty
            if (from.x > 0)
                SimModifyMap_NotStrict(from);

            var PathObj = _gridData.StartFindFog(from);
            return PathObj;
        }

        /// <summary>
        /// 模拟修改地图，下帧前复原。对目标周围N*N区域进行Undefined置为Empty
        /// 参数：中心坐标, N*N区域
        /// </summary>
        void SimModifyMap(Vector2Int center, int delta = 5)
        {
            PixType[,] origin = new PixType[delta, delta];

            int radius = delta / 2;
            var xs = center.x - radius;
            var ys = center.y - radius;
            var xe = xs + delta;
            var ye = ys + delta;

            List<Vector2Int> pixList = new List<Vector2Int>();

            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var data = _map[x, y];
                    origin[x - xs, y - ys] = data;

                    if (data == PixType.Undefined)
                    {
                        _map[x, y] = PixType.Empty;
                        pixList.Add(new Vector2Int(x, y));
                    }
                }
            _sim_modify.Add((new Vector2Int(xs, ys), origin));
            _gridData.Apply(pixList);
        }


        /// <summary>
        /// 模拟修改地图，下帧前复原。对目标周围N*N区域进行Undefined置为Empty
        /// 参数：中心坐标, N*N区域
        /// </summary>
        void SimModifyMap_NotStrict(Vector2Int center, int delta = 5)
        {
            PixType[,] origin = new PixType[delta, delta];

            int radius = delta / 2;
            var xs = center.x - radius;
            var ys = center.y - radius;
            var xe = xs + delta;
            var ye = ys + delta;

            List<Vector2Int> pixList = new List<Vector2Int>();

            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var data = _map[x, y];
                    origin[x - xs, y - ys] = data;
                    _map[x, y] = PixType.Empty;
                    pixList.Add(new Vector2Int(x, y));
                }
            _sim_modify.Add((new Vector2Int(xs, ys), origin));
            _gridData.Apply(pixList);
        }


        /// <summary>
        /// 小地图中心点坐标
        /// </summary>
        public Vector2Int GetCenterPos()
        {
            return _small_map_pos + PlayerPos_SmallMap;
        }
        public Vector2Int GetPlayerPosEmpty()
        {
            var player = GetCenterPos();

            foreach (var off in Utils.Order5X5)
            {
                var p = player + off;
                var data = _map[p.x, p.y];
                var cell = _gridData._grid[p.x / 5, p.y / 5];
                if (data == PixType.Empty && cell != null && cell.Direction != 0)
                    return p;
            }

            return Utils.DefaultV2I;
        }


        // 指定连线倒着找指定地块
        Vector2Int GetTileFromLine(Vector2Int start, Vector2Int target)
        {
            var record = GetLineBresenham(start, target);
            _pixRecord[PixRecordType.LineOfFindPath] = record;      // 存一下,for debug

            var len = record.Count();
            for (int i = len - 1; i >= 0; i--)
            {
                var p = record[i];
                var data = _map[p.x, p.y];
                if (data != PixType.Undefined && data != PixType.ObstacleByBig)
                    return p;
            }

            return Utils.DefaultV2I;
        }

        /// <summary>
        /// 用Bresenham方法，返回连线，顺序: start => target
        /// </summary>
        List<Vector2Int> GetLineBresenham(Vector2Int start, Vector2Int target)
        {
            if (MathF.Abs(start.x - target.x) > MathF.Abs(start.y - target.y))
                return DrawLineH(start, target);
            else
                return DrawLineV(start, target);
        }

        // 水平
        List<Vector2Int> DrawLineH(Vector2Int start, Vector2Int target)
        {
            var record = new List<Vector2Int>();
            bool need_reverse;

            int x0, y0, x1, y1;
            if (start.x < target.x)
            {
                x0 = start.x; y0 = start.y; x1 = target.x; y1 = target.y;
                need_reverse = false;
            }
            else
            {
                x0 = target.x; y0 = target.y; x1 = start.x; y1 = start.y;
                need_reverse = true;
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

            // 最后个点，有可能是有问题的
            var last = record[record.Count - 1];
            if (last.x != x1 || last.y != y1)
            {
                record.RemoveAt(record.Count - 1);
            }


            if (need_reverse)
                record.Reverse();

            return record;
        }
        // 竖直
        List<Vector2Int> DrawLineV(Vector2Int start, Vector2Int target)
        {
            var record = new List<Vector2Int>();
            bool need_reverse;

            int x0, y0, x1, y1;
            if (start.y < target.y)
            {
                x0 = start.x; y0 = start.y; x1 = target.x; y1 = target.y;
                need_reverse = false;
            }
            else
            {
                x0 = target.x; y0 = target.y; x1 = start.x; y1 = start.y;
                need_reverse = true;
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

            // 最后个点，有可能是有问题的
            var last = record[record.Count - 1];
            if (last.x != x1 || last.y != y1)
            {
                record.RemoveAt(record.Count - 1);
            }

            if (need_reverse)
                record.Reverse();

            return record;
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
                    _map[p1.x, p1.y] = PixType.ObstacleEdge;
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
                        if (data == PixType.Fog)
                        {
                            revertAction(); // 还原
                            return p;
                        }
                        if (data == PixType.ObstacleEdge)
                        {
                            _map[p.x, p.y] = PixType.ObstacleEdgeTemp;
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
                    _map[p1.x, p1.y] = PixType.Fog; // 还原
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
                        if (data == PixType.Empty)
                        {
                            revertAction();
                            return p;
                        }
                        if (data == PixType.Fog)
                        {
                            _map[p.x, p.y] = PixType.FogArea;
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


        #region 寻路-最近迷雾


        int _DoFindFog_frame;               // 发起找迷雾的帧序
        Vector2Int _DoFindFog_pos;          // 发起找迷雾的起始点

        /// <summary>
        /// 找最近迷雾。
        /// refresh: 每次调用是否都重新(用当前位置)找最近的迷雾
        /// </summary>
        public BigCellPathResult FindNearestFogAStar(MapIconType executor, bool refresh_target, float avoid_factor)
        {
            var start_pos = FindPlayerPosAndHistory(executor, out _);
            if (start_pos.x < 0)
            {
                _findFogPath.Status = PathFindingResult.StartPosFail;
                return _findFogPath;
            }

            var lastStatus = _findFogPath.Status;
            Vector2Int target = Utils.DefaultV2I;
            // 查看上个迷雾消失没
            if (lastStatus == PathFindingResult.Success)
            {
                var cell = _findFogPath.GetLastCell();
                if (cell.HasFog)
                {
                    target = new Vector2Int(cell.x * 5, cell.y * 5);
                }
            }

            // 每次找新迷雾后，过5帧再确认一下
            if (FrameCount == _DoFindFog_frame + 5)
            {
                var second = PlayerFindFog(_DoFindFog_pos);
                if (second.Status == PathFindingResult.Success)
                {
                    // 比对检查
                    BigCell suggest_cell = second.GetLastCell();
                    var suggest_cell_pos = new Vector2Int(suggest_cell.x * 5, suggest_cell.y * 5);
                    if (suggest_cell_pos != target)
                    {
                        target = suggest_cell_pos;
                    }
                }
            }

            // 失败说明：尝试找过了，但没找到任何目标
            if (lastStatus == PathFindingResult.NoTarget && target.x < 0)
            {
                return _findFogPath;
            }


            // 寻找最近迷雾 或 继续找上个迷雾
            if (refresh_target || target.x < 0)
            {
                FindNearestFog(start_pos);
            }
            else
            {
                _findFogPath = PlayerToTileAStar(start_pos, target, avoid_factor);

                // 寻不到就重新定目标
                if (_findFogPath.Status == PathFindingResult.Failed)
                    FindNearestFog(start_pos);
            }

            return _findFogPath;
        }

        void FindNearestFog(Vector2Int start_pos)
        {
            _findFogPath = PlayerFindFog(start_pos);
            _DoFindFog_frame = FrameCount;
            _DoFindFog_pos = start_pos;
        }

        /// <summary>
        /// GetPathFindingDirection。执行寻路后，获取当前前进的方向
        /// </summary>
        public Vector2Int PathResultGetDirection(BigCellPathResult path_result)
        {

            SmallCellFinder finder = new SmallCellFinder();
            var Path = path_result.Path;

            var path0 = finder.GetPixPath(_map, Path[0]);
            Vector2Int from = path0[0];
            Vector2Int to = default;
            List<Vector2Int> to_list = new List<Vector2Int>();

            // 第一个大格子
            for (int i = 1; i < path0.Count; i++)
                to_list.Add(path0[i]);

            if (Path.Count > 1)
            {
                var node = Path[1];
                var path = finder.GetPixPath(_map, node);
                for (int i = 0; i < path.Count; i++)
                    to_list.Add(path[i]);
            }

            if (Path.Count > 2)
            {
                var node = Path[2];
                var path = finder.GetPixPath(_map, node);
                for (int i = 0; i < path.Count; i++)
                    to_list.Add(path[i]);
            }

            // 从远到近，选一个from-to连线间无障碍的to
            for (int i = to_list.Count - 1; i >= 0; i--)
            {
                to = to_list[i];
                var line = GetLineBresenham(from, to);
                bool has_obstacle = false;
                foreach (var p in line)
                {
                    var data = _map[p.x, p.y];
                    if (data != PixType.Empty)
                    {
                        has_obstacle = true;
                        break;
                    }
                }

                if (!has_obstacle)
                    break;
            }

            // 偏好斜着走。 1/2为阈值   26.5°   37°   26.5° 
            // 起始就是360°/8 = 45°，每个方向分到45° 也就是要知道tan22.5°
            // 但是要映射到整数分子/分母，这样才能操作
            Vector2Int normal_dir = default;
            Vector2Int dir = to - from;
            int x = dir.x;
            int y = dir.y;
            if (x == 0 && y == 0)
                normal_dir = new Vector2Int(0, 0);
            else
            {
                int x_abs = x > 0 ? x : -x;
                int y_abs = y > 0 ? y : -y;
                bool x_bigger = x_abs > y_abs;
                var a = x_bigger ? x_abs : y_abs;
                var b = x_bigger ? y_abs : x_abs;
                float rate = (float)b / a;
                if (rate <= 0.5f)
                {
                    if (x_bigger)
                        normal_dir = new Vector2Int(x > 0 ? 1 : -1, 0);
                    else
                        normal_dir = new Vector2Int(0, y > 0 ? 1 : -1);

                }
                else
                    normal_dir = new Vector2Int(x > 0 ? 1 : -1, y > 0 ? 1 : -1);

            }



            return normal_dir;
        }



        public string GetPathFindingDebugStr(PathFindingResult result)
        {
            if (result == PathFindingResult.Undefined)
                return $"未知";
            else if (result == PathFindingResult.Failed)
                return $"失败";
            else if (result == PathFindingResult.Success)
                return $"成功";
            else if (result == PathFindingResult.StartPosFail)
                return $"初始位置无空地";
            else if (result == PathFindingResult.StartPosFail)
                return $"没有目标";
            else
                return "";
        }

        #endregion





        #region 寻路-玩家


        /// <summary>
        /// 1.如果当前帧目标不见了, "目标"用上一个"有效地点"
        /// 2.如果当前帧自己不见了, "自己"用上一个"有效地点"(用原方向不行)
        /// 3.如果两者距离在7像素以下停止
        /// </summary>
        public Vector2Int GetDir_FollowTarget(MapIconType executor, MapIconType target, float distance
                                          , float avoid_factor, out bool from_recog, out bool target_recog
                                          , out BigCellPathResult path)
        {

            Vector2Int dir = Vector2Int.zero;
            path = new BigCellPathResult();

            var from_pos = FindPlayerPosAndHistory(executor, out bool use1);
            var target_pos = FindPlayerPosAndHistory(target, out bool use2);
            from_recog = !use1;
            target_recog = !use2;

            if (from_pos.x <= 0 || target_pos.x <= 0)      // 开局一直无法识别的情况
                return dir;

            // 距离条件
            if ((from_pos - target_pos).sqrMagnitude > distance * distance)
            {
                path = PlayerToPlayerAStar(from_pos, target_pos, avoid_factor);
                if (path.Status == PathFindingResult.Success)
                {
                    dir = PathResultGetDirection(path);
                }
            }

            return dir;
        }

        int elseP_max_count = 0;
        public bool CheckPlayerDistance(MapIconType executor, float max_distance)
        {
            if (max_distance <= 0)
                return true;

            var exe_pos = FindPlayerPosAndHistory(executor, out _);
            var P1_pos = FindPlayerPosAndHistory(MapIconType.P1, out _);
            var list1 = GetIconData(MapIconType.P2).InstList;
            var list2 = GetIconData(MapIconType.ElseP).InstList;

            // ElseP是绿色，比较独一
            elseP_max_count = Math.Max(list2.Count, elseP_max_count);
            elseP_max_count = Math.Min(2, elseP_max_count);
            if (list2.Count < elseP_max_count)
                return false;

            List<Vector2Int> list = new List<Vector2Int>();
            list.Add(P1_pos);
            list.AddRange(list1);
            list.AddRange(list2);
            foreach (var p in list)
            {
                var delta = p - exe_pos;
                if (delta.magnitude > max_distance)
                {
                    return false;
                }
            }

            return true;
        }

        public Vector2Int FindPlayerPosAndHistory(MapIconType type, out bool use_history)
        {
            use_history = false;
            if (type == default)
                return default;

            var pos = FindPlayerPos(type, out var history);
            if (pos.x < 0)
            {
                pos = history;
                use_history = true;
                SaveIconErrorSmallMap(_small_map_pos);
            }

            return pos;
        }

        void SaveIconErrorSmallMap(Vector2Int pos)
        {
            if (!AutoScriptManager.Inst.MapIconDebugStatus)
                return;

            if (IconErrorPos == pos)
                return;

            IconErrorPos = pos;
            var folder_path = $"{Application.streamingAssetsPath}/Error";
            var path = $"{folder_path}/{DU.GetTimeString()}{FrameCount}.png";

            var c = new Color32[_small_map_colors.Length];
            Array.Copy(_small_map_colors, c, _small_map_colors.Length);
            IU.Color32ReverseYAxis(c, _rectW);
            IU.SaveColor32(c, _rectW, path);
        }


        /// <summary>
        /// P1/P2/P3/P4, 在_map中的位置。.x > 0才是有效值
        /// </summary>
        public Vector2Int FindPlayerPos(MapIconType type, out Vector2Int his_pos)
        {
            var source_type = type == MapIconType.TeamP1 ? MapIconType.P2 : type;

            var inst_list = _icon_datas[(int)source_type].InstList;
            var history_player_index = GetIndexOfPlayer(type);
            his_pos = _history_player_pos[history_player_index];

            if (inst_list.Count == 0)
                return Utils.DefaultV2I;


            var player_pos = Utils.DefaultV2I;

            if (type == MapIconType.P2 || type == MapIconType.TeamP1)
            {
                var P1_pos = FindPlayerPos(MapIconType.P1, out var P1_his_pos);
                bool P1_success = P1_pos.x > 0;
                if (!P1_success)
                    P1_pos = P1_his_pos + _move_delta;

                var center = GetCenterPos();
                var P1_pos_center_symmetry = center * 2 - P1_pos;

                if (inst_list.Count == 1)
                {
                    var pos = inst_list[0];
                    var distance = (pos - P1_pos_center_symmetry).magnitude;
                    bool isP2 = (P1_success && distance < 2.5f) || (!P1_success && distance < 10f);

                    if (type == MapIconType.P2 && isP2)
                        player_pos = pos;
                    else if (type == MapIconType.TeamP1 && !isP2)
                        player_pos = pos;
                }
                else
                {   // 2个候选时，最近和最远就可以
                    if (type == MapIconType.P2)
                    {
                        RecogUtil.ListFindClosest(inst_list, P1_pos_center_symmetry, out var min_pos, out var distance);
                        player_pos = min_pos;
                    }
                    else
                    {
                        RecogUtil.ListFindFarthest(inst_list, P1_pos_center_symmetry, out var max_pos, out var distance);
                        player_pos = max_pos;
                    }
                }
            }
            else
            {
                player_pos = inst_list[0];
            }

            if (player_pos.x > 0)
                _history_player_pos[history_player_index] = player_pos;

            return player_pos;

        }


        /// <summary>
        /// 对图像进行卷积。舍弃边缘一格，置为黑
        /// </summary>
        ConvInfo[] ConvolutionColors(Color32[] origin, int w, int h)
        {
            ConvInfo[] result = new ConvInfo[origin.Length];
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int sum_r = 0, sum_g = 0, sum_b = 0;
                    for (int m = y - 1; m <= y + 1; m++)
                        for (int n = x - 1; n <= x + 1; n++)
                        {
                            var c = origin[m * w + n];
                            sum_r += c.r; sum_g += c.g; sum_b += c.b;
                        }
                    ConvInfo item = default;
                    item.Conv = new Color32((byte)(sum_r / 9), (byte)(sum_g / 9), (byte)(sum_b / 9), 255);
                    item.G2R = (float)item.Conv.g / item.Conv.r;
                    item.B2G = (float)item.Conv.b / item.Conv.g;
                    result[y * w + x] = item;
                }

            return result;
        }

        public Vector2Int SmallMapPos => _small_map_pos;

        Vector2Int[] _history_player_pos;
        int GetIndexOfPlayer(MapIconType type)
        {
            var order = new List<MapIconType>()
            {
                MapIconType.P1,MapIconType.P2,MapIconType.TeamP1,MapIconType.ElseP
            };
            if (_history_player_pos == null)
                _history_player_pos = new Vector2Int[order.Count];

            var index = order.IndexOf(type);
            return index;
        }



        #endregion


        #region 图标

        List<MapIconType> _icon_datas_dynamic;
        List<MapIconType> _icon_datas_dynamic_1th;
        List<MapIconType> _icon_datas_dynamic_2th;
        List<MapIconType> _icon_datas_static;

        public void InitIconData()
        {
            int enumCount = Enum.GetValues(typeof(MapIconType)).Length;
            _icon_datas = new MapIconData[enumCount];

            //conv_min.r = 155 改为 159
            _icon_datas[(int)MapIconType.P1] = new MapIconData(false,
                new Color32(156, 71 - 3, 21 - 3, 255), new Color32(205 + 3, 103 + 3, 35 + 3, 255),
                new Color32(153, 87 - 3, 27 - 3, 255), new Color32(183 + 3, 102 + 3, 41 + 3, 255),
                new Vector2Int(2, 2), new Vector2Int(5, 5), new Color32(255, 125, 0, 255)
            );
            _icon_datas[(int)MapIconType.P2] = new MapIconData(false,
                new Color32(20, 22, 159, 255), new Color32(46 + 3, 69 + 3, 187 + 3, 255),
                new Color32(24, 65, 139, 255), new Color32(32 + 3, 89 + 3, 167 + 3, 255),
                new Vector2Int(2, 2), new Vector2Int(5, 5), new Color32(45, 134, 255, 255)
            );
            _icon_datas[(int)MapIconType.TeamP1] = new MapIconData(false,
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Vector2Int(2, 2), new Vector2Int(5, 5), new Color32(255, 0, 0, 255)
            );
            _icon_datas[(int)MapIconType.ElseP] = new MapIconData(false,
                new Color32(36, 118, 17, 255), new Color32(62, 151, 37, 255),
                new Color32(44, 125, 23, 255), new Color32(57, 152, 35, 255),
               new Vector2Int(2, 2), new Vector2Int(5, 5), new Color32(71, 200, 23, 255)
            );
            _icon_datas[(int)MapIconType.Boss] = new MapIconData(false,
                new Color32(232, 194, 160, 255), new Color32(240, 210, 172, 255),
                new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255),
                new Vector2Int(3, 7), new Vector2Int(7, 10), new Color32(213, 140, 69, 255)
            );
            _icon_datas[(int)MapIconType.Item] = new MapIconData(false,
                new Color32(251, 25, 47, 255), new Color32(252, 45, 67, 255),
                new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255),
                new Vector2Int(3, 4), new Vector2Int(8, 8), new Color32(237, 25, 44, 255)
            );
            _icon_datas[(int)MapIconType.PortalDoor] = new MapIconData(false,
                new Color32(201, 83, 38, 255), new Color32(207, 88, 42, 255),
                new Color32(199, 82, 37, 255), new Color32(206, 88, 40, 255),
                new Vector2Int(3, 2), new Vector2Int(8, 10), new Color32(206, 86, 40, 255)
            );
            _icon_datas[(int)MapIconType.PortalPoint] = new MapIconData(false,
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Vector2Int(0, 0), new Vector2Int(0, 0), new Color32(0, 0, 0, 255)
            );
            _icon_datas[(int)MapIconType.RecordPoint] = new MapIconData(true, default, default, default, default, default, default, default);
            _icon_datas[(int)MapIconType.UnlitRecordPoint] = new MapIconData(true, default, default, default, default, default, default, default);
            _icon_datas[(int)MapIconType.Book] = new MapIconData(true, default, default, default, default, default, default, default);
            for (int i = 0; i < _icon_datas.Length; i++)
            {
                if (_icon_datas[i] != null)
                    _icon_datas[i].Type = (MapIconType)i;
            }


            _icon_datas_dynamic = new List<MapIconType>();
            _icon_datas_static = new List<MapIconType>();
            foreach (var item in _icon_datas)
                if (item != null && item.IsStatic)
                    _icon_datas_static.Add(item.Type);
            foreach (var item in _icon_datas)
                if (item != null && !item.IsStatic)
                    _icon_datas_dynamic.Add(item.Type);


            _icon_datas_dynamic_1th = new List<MapIconType>()
             { MapIconType.Boss , MapIconType.PortalDoor};
            _icon_datas_dynamic_2th = new List<MapIconType>()
            {MapIconType.P1, MapIconType.P2, MapIconType.ElseP,MapIconType.Item};

        }
        public MapIconData GetIconData(MapIconType type)
        {
            return _icon_datas[(int)type];
        }

        public Vector2[] ConvertToShow(List<Vector2Int> list)
        {
            var result = new Vector2[list.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = list[i] - _range_zero;
            }

            return result;
        }
        public Vector2 ConvertToShow(Vector2Int pos)
        {
            if (pos.x <= 0)
                return pos;

            return new Vector2(pos.x - _range_zero.x, pos.y - _range_zero.y);
        }
        public Vector2Int ConvertToData(Vector2 pos)
        {
            return new Vector2Int((int)pos.x + _range_zero.x, (int)pos.y + _range_zero.y);
        }



        #endregion



        public void Dispose()
        {
        }

        #region Private Class

        public struct JudgePix
        {
            public byte undefined_num;
            public byte empty_num;
            public byte obstacle_edge_num;

            public bool CheckFull(out PixType type)
            {
                type = PixType.Undefined;
                if (undefined_num + empty_num + obstacle_edge_num < 5)
                    return false;

                // 因为要确定死所有CheckFull偏保守，而GuessType()偏激进
                if (undefined_num >= empty_num && undefined_num >= obstacle_edge_num)
                    type = PixType.Undefined;
                else if (obstacle_edge_num >= empty_num)
                    type = PixType.ObstacleEdge;
                else
                    type = PixType.Empty;


                return true;
            }


            public PixType GuessType()
            {
                PixType type;
                if (obstacle_edge_num >= empty_num && obstacle_edge_num >= undefined_num)
                    type = PixType.ObstacleEdge;
                else if (empty_num >= undefined_num)
                    type = PixType.Empty;
                else
                    type = PixType.Undefined;

                return type;
            }

            public static bool operator ==(JudgePix left, JudgePix right)
            {
                return left.undefined_num == right.undefined_num && left.empty_num == right.empty_num
                    && left.obstacle_edge_num == right.obstacle_edge_num;
            }

            public static bool operator !=(JudgePix left, JudgePix right)
            {
                return !(left == right);
            }
        }

        public struct ConvInfo
        {
            public Color32 Conv;
            public float G2R;              // G/R值     
            public float B2G;              // G/R值     
        }


        public enum LightType : byte
        {
            Dark = 0,                   // 暗地                         灰色
            Light = 1,                  // 亮地。                       白色
            Fresh = 2,                  // "新"亮地。                    绿色
            ObstacleEdgeOfLight = 3,    // 由亮地推算的边界,一般代表"地图图标"     紫色
            FreshEdge = 4,              // "新"边界
            FreshVisited = 5,           // "新"边界
            TempEdge = 6,               // 候选边界
            TempEdgeVisited = 7,        // 候选边界

        }

        public enum PixRecordType : byte
        {
            ObstacleEdgeOfLight,            //由光照地图判定的边界。责任划分。为了实现迷雾中目标搜索。
                                            //边缘的Undefined改为边界，进而对边界连贯地递归才能找到最近迷雾
                                            //这个边界生成策略，可以换成连线吧。？
            LineOfFindPath,                 //迷雾寻路，打射线
            AreaOfFindNearestFog,          //迷雾寻路，从打射线与边界的交点开始，遍历的区域
            ObstacleEdgeEndPoint,          //边界端点——单帧
            ObstacleEdgeGap,               //边界缝隙——单帧
            ObstacleEdgeGapTotal,          //边界缝隙——总
        }

        public struct LightUnion
        {
            public LightType LightType;
            public bool Access;
        }

        #endregion

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


        public MapData Create(string id, CVRect rect)
        {
            if (!mapDataDic.ContainsKey(id))
                mapDataDic[id] = new MapData(id, rect);

            return Get(id);
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

    public class MapDataConfig
    {

    }


    #endregion


    public enum PixType : byte
    {
        Undefined = 0,          // 默认，此块不会参与功能。          灰色
        Empty = 1,              // 空地。                          黑色
        EmptyTemp = 2,          // 候选空地。                   
        ObstacleEdge = 3,       // 障碍边界                        白色
        ObstacleEdgeTemp = 4,   // 候选障碍边界
        ObstacleByBig = 5,      // 大格子侧标定的障碍（临时障碍）     橙色
        Fog = 6,                // 迷雾边界                         蓝色
        FogArea = 7,            // 迷雾区域，为迷雾边界延伸2格范围

        Block = 8,              // 遮挡物，例如装备条                ？色
        Icon = 9,               // 图标                             ？色
        ObstacleEdgeGap = 4,  // 边界缝隙

    }

    public enum PathFindingResult
    {
        Undefined,              // 未知
        Failed,                 // 失败
        Success,                // 成功
        StartPosFail,           // 初始位置有问题
        NoTarget,               // 没有目标了
    }

   


    #region MapIconType

    /// <summary>
    /// 地图图标类型。
    /// 枚举顺序不能变。做数组索引，会序列化
    /// </summary>
    public enum MapIconType
    {
        Undefined,
        P1,
        P2,
        TeamP1,             // 整个队伍的P1(蓝色)
        ElseP,              // 其他玩家会有多个(都是绿色)
        Boss,
        Item,
        PortalDoor,         // 红色的传送门
        PortalPoint,        // 蓝色的传送点
        RecordPoint,        // 蓝色的记录点
        UnlitRecordPoint,   // 未点亮的记录点
        Book,               // 大书本，容易漏缝
    }

    public class MapIconData
    {
        public MapIconType Type;
        public bool IsStatic;

        // 实例列表，每个实例的中心点
        public List<Vector2Int> InstList;
        /// <summary>
        /// 暂存每帧新增的，坐标是基于小地图坐标系
        /// </summary>
        public List<Vector2Int> AddList;

        public Color32 ColorMin;
        public Color32 ColorMax;
        public Color32 ConvMin;
        public Color32 ConvMax;

        // 识别后要占位
        public Vector2Int RecogPos;
        public Vector2Int Size;
        public Color32 DrawColor;

        public MapIconData(bool isStatic, Color32 colorMin, Color32 colorMax, Color32 convMin, Color32 convMax
                        , Vector2Int recogPos, Vector2Int size, Color32 drawColor)
        {
            IsStatic = isStatic;
            InstList = new List<Vector2Int>();
            AddList = new List<Vector2Int>();
            ColorMin = colorMin;
            ColorMax = colorMax;
            ConvMin = convMin;
            ConvMax = convMax;
            RecogPos = recogPos;
            Size = size;
            DrawColor = drawColor;
        }

    }

    #endregion

}