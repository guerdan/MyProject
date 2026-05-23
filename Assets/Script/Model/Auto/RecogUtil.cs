
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using OpenCvSharp;
using Script.Framework.Else;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public struct Color32Image
    {
        public Color32[] Colors;
        public int W;
        public int H;

        public Color32Image(int w, int h)
        {
            W = w;
            H = h;
            Colors = new Color32[w * h];
        }

        public Color32Image(Color32[] colors, int w, int h)
        {
            Colors = colors;
            W = w;
            H = h;
        }

    }
    public struct Vec4bImage
    {
        public Vec4b[] Colors;
        public int W;
        public int H;
        public Vec4bImage(Vec4b[] colors, int w, int h)
        {
            Colors = colors;
            W = w;
            H = h;
        }
    }
    public struct Vec3bImage
    {
        public Vec3b[] Colors;
        public int W;
        public int H;
        public Vec3bImage(Vec3b[] colors, int w, int h)
        {
            Colors = colors;
            W = w;
            H = h;
        }
    }

    /// <summary>
    /// 约定所表示的范围包括端点值
    /// X_Min = -1时代表不指定
    /// </summary>
    public struct XYRange
    {
        public int X_Min;
        public int X_Max;
        public int Y_Min;
        public int Y_Max;

        public XYRange(int x_min, int x_max, int y_min, int y_max)
        {
            X_Min = x_min; X_Max = x_max; Y_Min = y_min; Y_Max = y_max;
        }
    }


    public class DiffInfo
    {
        public int Id;
        public Color32[] Sample;
        public int Sum;

    }


    public static class RecogUtil
    {
        public static readonly Color32 color_black = new Color32(0, 0, 0, 255);
        public static readonly Color32 color_white = new Color32(255, 255, 255, 255);

        // public static void OneItem()
        // {
        //     var startP = cell.StartP;
        //     // debug
        //     // if (cell.Row == 4 && cell.Column == 5)
        //     // {
        //     //     var k = 1;
        //     // }

        //     string item_debug = "";
        //     string number_debug = "";
        //     string roman_debug = "";
        //     IdentifyItem(img, cell, out string item_id, out float item_score, out var a_diff);
        //     IdentifyFrame(img, startP, out string frame_id, out float frame_score);

        //     var frame_debug = "";
        //     if (frame_id != "0") frame_debug = $"框{frame_id}/ {(item_score >= 1 ? "1" : GetDebugStr(frame_score))}\n";

        //     if (item_id != null)
        //     {
        //         var item = Config.GetItem(item_id);
        //         var numberPos = startP + new Vector2Int(5, 23);
        //         var RomanPos = startP + new Vector2Int(37, 5);
        //         if (cell.Row == 1)
        //         {
        //             numberPos.y -= 1;
        //             RomanPos.y -= 1;
        //         }

        //         IdentifyNumber(img, numberPos, out int number, out float number_score);

        //         item_debug = $"{item.Name}/ {(item_score == 0 ? "0" : GetDebugStr(item_score) + "/ " + GetDebugStr(a_diff))}\n";
        //         number_debug = $"数{number}/ {(number_score == 1 ? "1" : GetDebugStr(number_score))}\n";
        //         // roman_debug = romanNumber > 1 ?
        //         // $"阶{romanNumber}/ {(roman_score == 1 ? "1" : GetDebugStr(roman_score))}\n"
        //         //  : "";
        //     }
        //     var debug_str = item_debug + number_debug + roman_debug + frame_debug;

        //     if (debug_str.EndsWith("\n")) debug_str = debug_str.Substring(0, debug_str.Length - 1);
        //     debug_texts.Add((debug_str, startP + cell.Size / 2));
        // }


        #region Common
        /// <summary>
        /// 计算大包围盒-矩形(涵盖所有的小矩形)
        /// </summary>
        public static CVRect CalBoundingBox(List<CVRect> list)
        {
            // 计算大的包围盒, 有左上角和右下角两点决定矩形。
            Vector2Int left_down = new Vector2Int(-1, -1);
            Vector2Int right_up = new Vector2Int(-1, -1);

            foreach (var r in list)
            {
                Vector2Int lt = r.LeftDown;
                Vector2Int rb = r.RightUp;
                // lt在left_top左上方，则要更新
                if (left_down.x == -1)
                    left_down = lt;
                else
                {
                    if (lt.x < left_down.x)
                        left_down.x = lt.x;
                    if (lt.y < left_down.y)
                        left_down.y = lt.y;
                }

                if (right_up.x == -1)
                    right_up = rb;
                else
                {
                    if (rb.x > right_up.x)
                        right_up.x = rb.x;
                    if (rb.y > right_up.y)
                        right_up.y = rb.y;
                }

            }
            var w = right_up.x - left_down.x;
            var h = right_up.y - left_down.y;
            return new CVRect(left_down.x, left_down.y, w, h);
        }

        /// <summary>
        /// 计算大包围盒-矩形(涵盖所有的小矩形)
        /// 参数 region(left,top,width,height)
        /// </summary>
        public static Vector4 CalBoundingBox(Vector4[] list)
        {
            // 计算大的包围盒, 有左上角和右下角两点决定矩形。
            Vector2 left_down = new Vector2Int(-1, -1);
            Vector2 right_up = new Vector2Int(-1, -1);

            foreach (var r in list)
            {
                Vector2 lt = new Vector2(r.x, r.y);
                Vector2 rb = new Vector2(r.x + r.z, r.y + r.w);
                // lt在left_top左上方，则要更新
                if (left_down.x == -1)
                    left_down = lt;
                else
                {
                    if (lt.x < left_down.x)
                        left_down.x = lt.x;
                    if (lt.y < left_down.y)
                        left_down.y = lt.y;
                }

                if (right_up.x == -1)
                    right_up = rb;
                else
                {
                    if (rb.x > right_up.x)
                        right_up.x = rb.x;
                    if (rb.y > right_up.y)
                        right_up.y = rb.y;
                }

            }
            var w = right_up.x - left_down.x;
            var h = right_up.y - left_down.y;
            return new Vector4(left_down.x, left_down.y, w, h);

        }

        public static void ListFindClosest(List<Vector2Int> list, Vector2Int target
                                        , out Vector2Int result, out float result_distance)
        {

            (Vector2Int, float) min = default;

            foreach (var p in list)
            {
                float sqr = (p - target).sqrMagnitude;
                if (min.Item1 == default || sqr < min.Item2)
                {
                    min = (p, sqr);
                }
            }
            result = min.Item1;
            result_distance = (result - target).magnitude;
        }

        public static void ListFindFarthest(List<Vector2Int> list, Vector2Int target
                                       , out Vector2Int result, out float result_distance)
        {

            (Vector2Int, float) max = default;
            foreach (var p in list)
            {
                float sqr = (p - target).sqrMagnitude;
                if (max.Item1 == default || sqr > max.Item2)
                {
                    max = (p, sqr);
                }
            }
            result = max.Item1;
            result_distance = (result - target).magnitude;
        }

        public static Vector2Int TemplateMatch(Mat source, Mat template)
        {
            int t_width = template.Width;
            int t_height = template.Height;
            using (Mat resultMat = IU.MatchTemplate1(source, template, false))
            {
                var result = IU.FindResult(resultMat, t_width, t_height, 0.5f, out _);

                if (result.Count > 0)
                {
                    result.Sort((a, b) => b.Score.CompareTo(a.Score));
                    var rect = result[0].Rect;
                    return new Vector2Int(rect.x, rect.y);
                }

            }
            return Utils.DefaultV2I;
        }

        /// <summary>
        /// 面向固定像素。template逐像素比较，有任何像素不同就跳过。
        /// 耗时：全图是8ms，传range能加速
        /// 修改：允许1px的误差
        /// </summary>
        public static Vector2Int TemplateMatchVec4b(Vec4bImage source, Vec3bImage template, XYRange range = default)
        {
            int xs = 0;
            int xe = source.W - template.W;
            int ys = 0;
            int ye = source.H - template.H;

            if (range.X_Min > 0) xs = Math.Max(xs, range.X_Min);
            if (range.X_Max > 0) xe = Math.Min(xe, range.X_Max);
            if (range.Y_Min > 0) ys = Math.Max(ys, range.Y_Min);
            if (range.Y_Max > 0) ye = Math.Min(ye, range.Y_Max);

            for (int y = ys; y <= ye; y++)
                for (int x = xs; x <= xe; x++)
                {
                    // int tLen = template.W * template.H;
                    // int tLen = 1;
                    bool same = true;

                    for (int ty = 0; ty < template.H; ty++)
                        for (int tx = 0; tx < template.W; tx++)
                        {
                            var index = (ty + y) * source.W + tx + x;
                            var tColor = template.Colors[ty * template.W + tx];
                            var sColor = source.Colors[index];
                            if (tColor.Item0 < sColor.Item0 - 1 || tColor.Item0 > sColor.Item0 + 1
                            || tColor.Item1 < sColor.Item1 - 1 || tColor.Item1 > sColor.Item1 + 1
                            || tColor.Item2 < sColor.Item2 - 1 || tColor.Item2 > sColor.Item2 + 1)
                            {
                                same = false;
                                goto EndLoop;
                            }
                        }

                EndLoop:
                    if (same)
                        return new Vector2Int(x, y);


                }


            return Utils.DefaultV2I;
        }
        public static Color32 GetAverageColor(Color32Image img, CVRect rect)
        {
            int sum = rect.w * rect.h;
            int sum_r = 0, sum_g = 0, sum_b = 0;
            int xs = rect.x, xe = xs + rect.w, ys = rect.y, ye = ys + rect.h;

            for (int y = ys; y < ye; y++)
                for (int x = xs; x < xe; x++)
                {
                    var c = img.Colors[y * img.W + x];
                    sum_r += c.r; sum_g += c.g; sum_b += c.b;
                }
            var result = new Color32((byte)(sum_r / sum), (byte)(sum_g / sum), (byte)(sum_b / sum), 255);
            return result;
        }

        public static Color32Image GetImg(string path)
        {
            using (var mat = IU.GetMat(path))
            {
                mat.GetArray<Vec3b>(out var matData);
                int w = mat.Width, h = mat.Height;
                Color32[] colors = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var index = (h - y - 1) * w + x;
                        Vec3b data = matData[y * w + x];
                        colors[index] = new Color32(data.Item2, data.Item1, data.Item0, 255);
                    }


                Color32Image img = new Color32Image(colors, w, h);
                return img;
            }
        }



        public static Color32Image GetImg(Color32Image source, CVRect region)
        {
            var result = new Color32Image(region.w, region.h);
            for (int y = 0; y < result.H; y++)
                for (int x = 0; x < result.W; x++)
                {
                    result.Colors[y * result.W + x] = source.Colors[(y + region.y) * source.W + x + region.x];
                }
            return result;
        }

        public static Color32Image FilterImg(Color32Image source, Color32 condition
                                            , CVRect region = default, bool reverse = false)
        {
            Color32Image result;
            if (region.w == 0)
                result = new Color32Image(source.W, source.H);
            else
                result = new Color32Image(region.w, region.h);

            if (reverse)
                for (int y = 0; y < result.H; y++)
                    for (int x = 0; x < result.W; x++)
                    {
                        var color = source.Colors[(y + region.y) * source.W + x + region.x];
                        if (color.r <= condition.r && color.g <= condition.g && color.b <= condition.b)
                            result.Colors[y * result.W + x] = color_black;
                        else
                            result.Colors[y * result.W + x] = color_white;
                    }
            else
                for (int y = 0; y < result.H; y++)
                    for (int x = 0; x < result.W; x++)
                    {
                        var color = source.Colors[(y + region.y) * source.W + x + region.x];
                        if (color.r >= condition.r && color.g >= condition.g && color.b >= condition.b)
                            result.Colors[y * result.W + x] = color_white;
                        else
                            result.Colors[y * result.W + x] = color_black;
                    }
            return result;
        }

        #endregion

        #region Game Item

        /// <summary>
        /// 和游戏的对齐方式相同。先从上到下，再从左到右，进行排序。游戏中入库出库操作是这个顺序。
        /// </summary>
        public static Vector4[] GetRegions(ItemGridPosType pos_type, Vector2 start_pos)
        {
            Vector4[] regions = null;
            switch (pos_type)
            {
                case ItemGridPosType.Body:
                    regions = null;
                    break;
                case ItemGridPosType.Bag:
                    regions = new Vector4[60];
                    Vector2 offset = new Vector2(start_pos.x + 1093, start_pos.y + 111);
                    for (int col = 0; col < 12; col++)
                        for (int row = 0; row < 5; row++)
                        {
                            int index = col * 5 + (4 - row);
                            regions[index] = new Vector4(offset.x + col * 40, offset.y + row * 40, 41, 41);
                        }
                    break;
                case ItemGridPosType.StashPage:
                    regions = null;
                    break;
            }

            return regions;
        }



        public static void IdentifyItem(Color32Image image, out int id, out float score, out float a_diff)
        {
            id = 0;
            score = 0;
            a_diff = 0;


            GameItemCfgManager Config = GameItemCfgManager.Inst;
            var cfgs = Config.MatchList;
            var sample_points = Config.BlurPoints;


            var colors = image.Colors;
            var w = image.W;
            var sample_points_count = sample_points.Count;


            // 中间10X10是黑的就表明为空背包格
            var average = GetAverageColor(image, new CVRect(15, 15, 10, 10));
            if (average.r <= 10 && average.g <= 10 && average.b <= 10)
                return;

            var sampl_result = new Color32[sample_points_count];

            int s = Config.BlurScale;
            int blur_size = s * s;
            for (int i = 0; i < sample_points_count; i++)
            {
                var p = sample_points[i];
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < s; dy++)
                    for (int dx = 0; dx < s; dx++)
                    {
                        var col = colors[(p.y + dy) * w + p.x + dx];
                        r += col.r;
                        g += col.g;
                        b += col.b;
                    }
                var blur_col = new Color32((byte)(r / blur_size), (byte)(g / blur_size), (byte)(b / blur_size), 255);
                sampl_result[i] = blur_col;
            }

            // 全匹配
            // score = float.MaxValue;
            // foreach (var pair in tmpls)
            // {
            //     var c2 = pair.Item2;
            //     CompareTwoImage(sampl_result, c2, out var average_diff, out var diff);
            //     if (diff < score)
            //     {
            //         id = pair.Item1;
            //         score = diff;
            //         a_diff = average_diff;
            //     }
            // }


            // 如同淘汰制比赛一样，冠军最后胜出。
            // threshold 淘汰阈值，当累计差达到阈值时淘汰此匹配项，但至少保留一项  20个点 X 400 
            //
            float thr = 20 * 3 * sample_points_count;
            DiffInfo[] diffs = new DiffInfo[cfgs.Length];    // DiffInfo = (模版序,累计差)
            for (int i = 0; i < cfgs.Length; i++)
            {
                var cfg = cfgs[i];
                diffs[i] = new DiffInfo() { Id = cfg.Id, Sample = cfg.sample, Sum = 0 };
            }


            int remain = cfgs.Length;
            for (int i = 0; i < sample_points_count; i++)
            {
                var index = 0;
                var left = sampl_result[i];
                for (int j = 0; j < remain; j++)
                {
                    DiffInfo t_diff = diffs[j];
                    Color32[] t_colors = t_diff.Sample;

                    Color32 right = t_colors[i];
                    int dr = left.r - right.r, dg = left.g - right.g, db = left.b - right.b;
                    t_diff.Sum = t_diff.Sum + dr * dr + dg * dg + db * db;

                    if (t_diff.Sum < thr)
                    {
                        diffs[index++] = t_diff;
                    }
                }

                if (index <= 1)
                {
                    if (index == 0)     // 保留方差最小的
                        for (int j = 1; j < remain; j++)
                        {
                            if (diffs[j].Sum < diffs[0].Sum)
                                diffs[0] = diffs[j];
                        }
                    break;
                }

                remain = index;
            }
            var result = diffs[0];

            MakeImageUtil.CompareTwoImage(sampl_result, result.Sample, out var average_diff, out var diff);
            id = result.Id;
            score = diff;
            a_diff = average_diff;

            // var startP = cell_startP;
            // var RomanPos = startP + new Vector2Int(37, 5);
            // var RomanPos = new Vector2Int(startP.x + 37, startP.y + size.y - 35);
            // IdentifyRomanNumber(image, RomanPos, out int romanNumber, out float roman_score);
        }

        static Color32 min_col = new Color32(255, 255, 255, 255);
        static Color32 max_col = new Color32(0, 0, 0, 255);


        public static void IdentifyFrame(Color32Image image, bool isCurr, out ItemGridFrame status)
        {
            status = ItemGridFrame.Empty;

            var Config = GameItemCfgManager.Inst;

            var colors = image.Colors;
            int w = image.W, h = image.H;

            var yellow_color = new Color32(231, 206, 108, 255);
            Vector2Int select_off;

            if (isCurr)
            {
                select_off = Config.CurrSFrame;
            }
            else
            {
                select_off = Config.NormSFrame;
            }

            // 是否为选择框
            // 竖直取四个点呈黄色
            int fit_count = 0;
            for (int y = 0; y < 4; y++)
            {
                var index = (y + select_off.y) * w + select_off.x;
                var col = colors[index];
                if (col.r - yellow_color.r >= -10 && col.r - yellow_color.r <= 10
                    && col.g - yellow_color.g >= -10 && col.g - yellow_color.g <= 10
                    && col.b - yellow_color.b >= -20 && col.b - yellow_color.b <= 10)
                    fit_count++;
            }

            if (fit_count >= 4)
            {
                status = ItemGridFrame.Selected;
                return;
            }

            if (!isCurr)
            {
                // 是否为待选框
                // (1,1)至(1,2) 10个点呈-高于(63, 55, 45)   350个样本(68,60,50)/(118,105,80)
                var min_color = new Color32(63, 55, 45, 255);
                var max_color = new Color32(123, 110, 85, 255);
                Vector2Int target_off = Config.NormTFrame;

                fit_count = 0;
                for (int y = 0; y < 2; y++)
                {
                    var index = (y + target_off.y) * w + target_off.y;
                    var col = colors[index];
                    int r2g = col.r - col.g;
                    if (col.r >= min_color.r && col.g >= min_color.g && col.b >= min_color.b
                        && col.r <= max_color.r && col.g <= max_color.g && col.b <= max_color.b
                        && r2g > 3 && r2g < 18)
                        fit_count++;

                    // if (col.r < min_col.r)
                    //     min_col = col;

                    // if (col.r > max_col.r)
                    //     max_col = col;
                }

                if (fit_count >= 2)
                {
                    status = ItemGridFrame.Target;
                    return;
                }
            }


        }

        /// <summary>
        /// 固定位置的识别， 一个通道就行
        /// 输出完整的数，和匹配度最低的分数，方便断点审查
        /// 参数：
        /// isCurr = 是否为通货页格子
        /// </summary>
        public static void IdentifyNumber(Color32Image image, bool isCurr, out int number)
        {
            var Config = GameItemCfgManager.Inst;
            number = -1;
            var colors = image.Colors;
            int w = image.W, h = image.H;

            List<int> result = new List<int>();
            int xs, ys;
            Color32Image[] List = null;

            if (isCurr)
            {
                xs = Config.CurrNumStart.x;
                ys = Config.CurrNumStart.y;
                List = Config.SNumTmpls;
            }
            else
            {
                var black_count = 0;
                for (int y = Config.BNumStart.y; y < Config.SNumStart.y; y++)
                    for (int x = 0; x < 9; x++)
                    {
                        Color32 a = colors[y * w + x + Config.BNumStart.x];
                        if (a.r == 0 && a.g == 0 && a.b == 0)
                            black_count++;
                    }

                if (black_count > 3)
                { xs = Config.BNumStart.x; ys = Config.BNumStart.y; List = Config.BNumTmpls; }
                else
                { xs = Config.SNumStart.x; ys = Config.SNumStart.y; List = Config.SNumTmpls; }
            }

            // 一列一列的来
            while (true)
            {
                int single_num = -1;
                for (int i = 0; i < 10; i++)
                {
                    var template = List[i];
                    bool fit = true;
                    for (int y = 0; y < template.H; y++)
                        for (int x = 0; x < template.W; x++)
                        {
                            Color32 a = colors[(y + ys) * w + x + xs];
                            Color32 b = template.Colors[y * template.W + x];
                            if (b.r == 0 && (a.r > 0 || a.g > 0 || a.b > 0))
                            {
                                fit = false; goto EndLoop;
                            }
                        }

                EndLoop:
                    if (fit)
                    {
                        single_num = i;
                        xs = xs + template.W;
                        break;
                    }

                }
                if (single_num > -1)
                    result.Add(single_num);
                else
                    break;
            }


            if (result.Count == 0)
                return;

            // 拼接字符串，再转为数字
            string s = "";
            float min = 1;
            foreach (var num in result)
            {
                s += num;
            }
            number = int.Parse(s);
            // score = min;
        }


        #endregion


        #region 16分扇区

        private static List<List<Vector2Int>> divinedSectors16;


        /// <summary>
        /// 16等分扇区，返回值以圆心为原点
        /// </summary>
        public static List<List<Vector2Int>> GetDivinedSectors16()
        {
            if (divinedSectors16 == null)
            {
                divinedSectors16 = new List<List<Vector2Int>>();
                for (int i = 0; i < 16; i++)
                    divinedSectors16.Add(new List<Vector2Int>());

                int big_radius = 55;
                int small_radius = 40;
                int big_radius_sqaure = big_radius * big_radius;
                int small_radius_sqaure = small_radius * small_radius;

                //tan22.5° = 根2 - 1 = 0.414

                var rate1 = 0.414f;
                var rate2 = 1f;
                var rate3 = 2.414f;
                for (int y = -big_radius; y <= big_radius; y++)
                    for (int x = -big_radius; x <= big_radius; x++)
                    {
                        int mag = x * x + y * y;         //magnitude
                        if (mag < small_radius_sqaure || mag > big_radius_sqaure)
                            continue;

                        int sector_index = 0;               // 扇区序数，按象限顺序转
                        if (y > 0)
                            if (x > 0)  // 1象限
                            {
                                var rate = (float)y / x;
                                if (rate < rate1)
                                    sector_index = 0;
                                else if (rate < rate2)
                                    sector_index = 1;
                                else if (rate < rate3)
                                    sector_index = 2;
                                else sector_index = 3;
                            }
                            else
                            {
                                var rate = x == 0 ? 10000 : -(float)y / x;
                                if (rate < rate1) sector_index = 7;
                                else if (rate < rate2) sector_index = 6;
                                else if (rate < rate3) sector_index = 5;
                                else sector_index = 4;
                            }
                        else
                            if (x > 0)
                            {
                                var rate = -(float)y / x;
                                if (rate < rate1) sector_index = 15;
                                else if (rate < rate2) sector_index = 14;
                                else if (rate < rate3) sector_index = 13;
                                else sector_index = 12;

                            }
                            else
                            {
                                var rate = x == 0 ? 10000 : (float)y / x;
                                if (rate < rate1) sector_index = 8;
                                else if (rate < rate2) sector_index = 9;
                                else if (rate < rate3) sector_index = 10;
                                else sector_index = 11;
                            }

                        divinedSectors16[sector_index].Add(new Vector2Int(x, y));
                    }

            }

            return divinedSectors16;
        }
        #endregion





        #region 物品浮窗
        static string ItemFloatParse_LT_Path = @"D:\unityProject\MyProject_Resource\ItemAttr\template\item_float_left_top.png";
        static string ItemFloatParse_LT_Path2 = @"D:\unityProject\MyProject_Resource\ItemAttr\template\item_float_left_top2.png";
        static string ItemFloatParse_RT_Path = @"D:\unityProject\MyProject_Resource\ItemAttr\template\item_float_right_top.png";
        static string ItemFloatParse_RT_Path2 = @"D:\unityProject\MyProject_Resource\ItemAttr\template\item_float_right_top2.png";



        #region ItemFloatParse
        /// <summary>
        /// 输入图片, Vec4bImage 是一个OpenCV坐标系(左上角为顶点)的图片。
        /// 最后转Color32[]时，变为数学坐标系
        /// </summary>
        public static List<Color32Image> ItemFloatParse(Vec4bImage source)
        {

            int left_border = 0;
            int right_border = 0;
            int top_border = 0;
            int opencv_bottom = 0;

            // 评估耗时4ms
            // DU.RunWithTimer(() =>
            // {

            var off_LT = ItemFloatParseLT(source);
            if (off_LT.x < 0)
                throw new Exception("找不到 LT");
            left_border = off_LT.x;
            opencv_bottom = off_LT.y;


            var off_RT = ItemFloatParseRT(source, off_LT, out int img_right_top_W);
            if (off_RT.x < 0)
                throw new Exception("找不到 RT");
            right_border = off_RT.x + img_right_top_W - 1;

            // }, "TemplateMatchVec4b");

            // 开始倒转，以OpenCV视角遍历，给数学坐标系的地图赋值
            //
            int float_H = source.H - opencv_bottom;
            int float_W = right_border - left_border + 1;
            Color32[] colors = new Color32[float_W * float_H];
            Color32Image img_all = new Color32Image(colors, float_W, float_H);
            for (int y = opencv_bottom; y < source.H; y++)
                for (int x = left_border; x <= right_border; x++)
                {
                    var index = (source.H - y - 1) * float_W + x - left_border;
                    Vec4b data = source.Colors[y * source.W + x];
                    colors[index] = new Color32(data.Item2, data.Item1, data.Item0, 255);
                }

            // @debug
            // IU.Color32ReverseYAxis(colors, float_W);
            // IU.SaveColor32(colors, float_W, @"D:\unityProject\MyProject_Resource\ItemAttr\template\结果.png");
            // Save(img_all, $"{dir_path}/结果.png");

            // 开始识别逻辑
            // 
            var line1 = new Color32(179, 134, 62, 255);
            var line1_max = new Color32(180, 135, 63, 255);     // 上浮动1
            var line2 = new Color32(126, 92, 40, 255);
            var line2_max = new Color32(131, 97, 45, 255);      // 上浮动5，因为其透明度稍高，被高亮干扰

            var line_x = (float_W - 1) / 2;
            List<int> line_y = new List<int>();

            for (int y = img_all.H - 1; y >= 0; y--)
            {
                Color32 pix = colors[y * img_all.W + line_x];
                if ((pix.r >= line1.r && pix.g >= line1.g && pix.b >= line1.b
                    && pix.r <= line1_max.r && pix.g <= line1_max.g && pix.b <= line1_max.b)
                    || pix.r >= line2.r && pix.g >= line2.g && pix.b >= line2.b
                    && pix.r <= line2_max.r && pix.g <= line2_max.g && pix.b <= line2_max.b)
                {
                    line_y.Add(y);
                    if (line_y.Count >= 3)
                        break;
                }
            }


            if (line_y.Count < 3)
                throw new Exception("有问题，不足3个");


            int total_y = line_y[1] - line_y[2];
            int line_y2 = line_y[2];
            // DU.LogWarning($"高度{total_y}");

            var result = new List<Color32Image>();

            int count_index = 0;
            // 有效颜色。取值效果上，至少笔画要连起来
            Color32 valid_col = new Color32(72, 72, 140, 255);

            int valid_h = 14;
            int ty = line_y[1] - 2;
            while (ty > line_y2)
            {
                // 本行有没有有效颜色
                bool has_valid = CheckRowColor(img_all, ty, valid_col);
                if (has_valid)
                {
                    // 检查有效颜色的连续行数是否超过了 valid_h，说明出bug了
                    //
                    bool has_valid_out = CheckRowColor(img_all, ty - valid_h, valid_col);
                    if (has_valid_out)
                        throw new Exception("有问题，连续行数超过了 valid_h");

                    var img = FilterImg(img_all, valid_col, new CVRect(0, ty - valid_h + 1, img_all.W, valid_h));
                    result.Add(img);
                    count_index++;
                    ty = ty - valid_h - 4;
                }
                else
                    ty--;
            }


            return result;
        }

        #endregion
        public static Vector2Int ItemFloatParseLT(Vec4bImage source)
        {
            Vec3bImage img_left_top = IU.GetVec3BImage(ItemFloatParse_LT_Path);
            var off_LT = TemplateMatchVec4b(source, img_left_top);
            if (off_LT.x < 0)
            {
                Vec3bImage img_left_top2 = IU.GetVec3BImage(ItemFloatParse_LT_Path2);
                off_LT = TemplateMatchVec4b(source, img_left_top2);
            }

            return off_LT;
        }
        public static Vector2Int ItemFloatParseRT(Vec4bImage source, Vector2Int off_LT, out int img_right_top_W)
        {
            Vec3bImage img_right_top = IU.GetVec3BImage(ItemFloatParse_RT_Path);
            var off_RT = TemplateMatchVec4b(source, img_right_top
                , new XYRange(off_LT.x, source.W, off_LT.y, off_LT.y));
            if (off_RT.x < 0)
            {
                Vec3bImage img_right_top2 = IU.GetVec3BImage(ItemFloatParse_RT_Path2);
                off_RT = TemplateMatchVec4b(source, img_right_top2);
            }

            img_right_top_W = img_right_top.W;
            return off_RT;
        }

        public static string RecogAffix(Color32Image img)
        {
            var dataList = SplitTextImg(img, "");
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < dataList.Count;)
            {
                var data = dataList[i];
                WordImgCfg word = AffixCfgManager.Inst.GetWordImgCfg(data);
                if (word == null)
                    throw new Exception($"序{i} 无字符");

                char wordChar = default;
                if (word.CombineCount == 1)
                {
                    wordChar = word.Char;
                    i++;
                }
                else if (word.CombineCount == 2)
                {
                    WordImgCfg combine = AffixCfgManager.Inst.GetWordImgCfg(data + dataList[i + 1]);
                    if (combine == null)
                        throw new Exception($"序{i} 无字符");
                    wordChar = combine.Char;
                    i += 2;
                }

                if (wordChar < '0' || wordChar > '9')
                {
                    builder.Append(wordChar);
                }

            }
            var result = builder.ToString();
            var affixCfg = AffixCfgManager.Inst.GetAffixCfgByContent(result);
            if (affixCfg == null)
                throw new Exception("无法识别");

            return affixCfg.Id;
        }



        public static bool CheckRowColor(Color32Image source, int y, Color32 condition)
        {
            for (int x = 0; x < source.W; x++)
            {
                var pix = source.Colors[y * source.W + x];
                if (pix.r >= condition.r && pix.g >= condition.g && pix.b >= condition.b)
                    return true;
            }

            return false;
        }

        public static void Save(Color32Image img, string path)
        {
            var c = new Color32[img.Colors.Length];
            Array.Copy(img.Colors, c, img.Colors.Length);
            IU.Color32ReverseYAxis(c, img.W);
            IU.SaveColor32(c, img.W, path);
        }

        #region SplitTextImg

        public static List<string> SplitTextImg(Color32Image source, string path = null)
        {
            List<(Color32[], int)> char_imgs = new List<(Color32[], int)>();

            int w = 15, h = 14;
            int char_img_len = w * h;

            int char_index = 0;
            bool record_status = false;         // 状态机
            int black_count = 0;                // 连续黑色的列数，如果超过n列，代表空格 
            bool in_number = false;             // 设定：数字被两个空格包围


            for (int x = 0; x < source.W; x++)
            {
                var has_white = false;
                for (int y = 0; y < h; y++)
                {
                    var s_c = source.Colors[y * source.W + x];
                    if (s_c.r > 0)
                    { has_white = true; break; }
                }

                // 状态机切换
                if (!record_status && has_white)        // 开始记录
                {
                    black_count = 0;

                    if (!in_number)
                    {
                        record_status = true;
                        var colors = new Color32[char_img_len];
                        for (int cy = 0; cy < h; cy++)
                            for (int cx = 0; cx < w; cx++)
                                colors[cy * w + cx] = color_black;
                        char_imgs.Add((colors, x));
                    }
                }
                else if (!record_status && !has_white)        // 记录black_count
                {
                    if (char_imgs.Count > 0)
                    {
                        black_count++;
                        if (black_count >= 5)
                        {
                            black_count = 0;
                            in_number = !in_number;
                        }
                    }
                }
                else if (record_status && !has_white)        // 结束记录
                {
                    record_status = false;
                    char_index++;
                    black_count = 1;
                }

                if (record_status)
                {
                    var tuple = char_imgs[char_index];
                    var colors = tuple.Item1;
                    var x_start = tuple.Item2;
                    for (int y = 0; y < h; y++)
                        colors[y * w + x - x_start] = source.Colors[y * source.W + x];

                    int word_len = x - x_start + 1;
                    bool connect = true;
                    // 检查是否断开了
                    // @设定：固定15px的占位空间，有效图像居中，字与字之间无连笔情况(可证明)
                    // 就一种情况 14px字 与 15px字 连笔了。等报警把
                    if (word_len == w - 1)
                    {
                        connect = false;
                        bool[] column = new bool[h];
                        for (int y = 0; y < h; y++)
                        {
                            var data = colors[y * w + x - x_start];
                            if (data.r > 0)
                            {
                                column[y] = true;
                                if (y - 1 >= 0) column[y - 1] = true;
                                if (y + 1 < h) column[y + 1] = true;
                            }
                        }
                        for (int y = 0; y < h; y++)
                            if (source.Colors[y * source.W + x + 1].r > 0 && column[y])
                            {
                                connect = true;
                                break;
                            }
                    }

                    if (!connect || word_len >= w)
                    {
                        record_status = false;
                        char_index++;
                        black_count = 0;
                    }
                }
            }

            List<string> result = new List<string>();
            for (int i = 0; i < char_imgs.Count; i++)
            {
                var tuple = char_imgs[i];
                var colors = tuple.Item1;
                // colors 的有效内容移动到最下
                var img = new Color32Image(colors, w, h);
                ST_MoveToBottom(img);
                var data = ImgToString(img);
                result.Add(data);

                // 打印
                if (!string.IsNullOrEmpty(path))
                    Save(img, $"{path}_{i}.png");
            }

            return result;
        }


        #endregion
        public static void ST_MoveToBottom(Color32Image img)
        {
            int space = 0;
            for (int y = 0; y < img.H; y++)
            {
                for (int x = 0; x < img.W; x++)
                {
                    var c = img.Colors[y * img.W + x];
                    if (c.r > 0)
                    {
                        space = y;
                        goto EndLoop;
                    }
                }
            }

        EndLoop:
            if (space > 0)
            {
                for (int y = 0; y < img.H - space; y++)
                    for (int x = 0; x < img.W; x++)
                    {
                        img.Colors[y * img.W + x] = img.Colors[(y + space) * img.W + x];
                    }

                for (int y = img.H - space; y < img.H; y++)
                    for (int x = 0; x < img.W; x++)
                    {
                        img.Colors[y * img.W + x] = color_black;
                    }
            }
        }


        /// <summary>
        /// 像素数不能超过 4 * 64 = 256
        /// </summary>
        public static string ImgToString(Color32Image img)
        {
            var colors = img.Colors;
            int b_len = colors.Length % 8 == 0 ? colors.Length / 8 : colors.Length / 8 + 1;
            byte[] bytes = new byte[b_len];

            int index = 0, offset = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                var c = colors[i];
                if (c.r > 0)
                {
                    bytes[index] = (byte)(bytes[index] | (1 << offset));
                }

                offset++;
                if (offset >= 8)
                {
                    offset = 0;
                    index++;
                }

            }
            var str = DU.BytesToBase64(bytes);
            return str;
        }

        #endregion

    }
}

