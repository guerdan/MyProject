
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OpenCvSharp;
using Script.Model.Auto;
using Script.UI.Panel.Auto;
using UnityEngine;

namespace Script.Util
{
    public static class MakeImageUtil
    {

        // 处理游戏物品的左上角数字，将实机截图里的非固定像素给剔除（变全透明）
        public static void DealGameItemNum()
        {
            var pre_path = $"{Application.streamingAssetsPath}/GameItem/Num";

            for (int i = 0; i < 10; i++)
            {
                var path = $"{pre_path}/{i}.png";
                byte[] bytes;
                int w, h;
                using (var bitmap = new Bitmap(path))
                {
                    w = bitmap.Width;
                    h = bitmap.Height;
                    var colors = IU.BitmapToColor32(bitmap);
                    for (int j = 0; j < colors.Length; j++)
                    {
                        var col = colors[j];
                        if (col.r != col.g || col.r != col.b || col.g != col.b)
                        {
                            colors[j] = new Color32(0, 0, 0, 0);
                        }
                    }
                    bytes = IU.Color32ToByte(colors);

                }

                using (Mat mat = Mat.FromPixelData(h, w, MatType.CV_8UC4, bytes))
                {
                    IU.SaveMat(mat, path);
                }
            }
        }

        // 处理游戏物品的右下角的罗马数字，将实机截图里的非固定像素给剔除（变全透明）
        public static void DealGameItemRomanNum()
        {
            var pre_path = $"{Application.streamingAssetsPath}/GameItem/RomanNum";
            int[] list = new int[5] { 1, 4, 5, 9, 10 };

            for (int i = 0; i < 5; i++)
            {
                var num = list[i];
                var path = $"{pre_path}/{num}.png";
                byte[] bytes;
                int w, h;
                using (var bitmap = new Bitmap(path))
                {
                    w = bitmap.Width;
                    h = bitmap.Height;
                    var colors = IU.BitmapToColor32(bitmap);
                    for (int j = 0; j < colors.Length; j++)
                    {
                        var col = colors[j];
                        if (col.r != col.g || col.r != col.b || col.g != col.b || col.a == 0)
                        {
                            colors[j] = new Color32(0, 0, 0, 0);
                        }
                    }
                    bytes = IU.Color32ToByte(colors);

                }

                using (Mat mat = Mat.FromPixelData(h, w, MatType.CV_8UC4, bytes))
                {
                    IU.SaveMat(mat, path);
                }
            }
        }

        public static Dictionary<Vector2Int, List<Vector2Int>> GetGameItemBlurPoints()
        {
            var res = new Dictionary<Vector2Int, List<Vector2Int>>();
            int w = 38;
            var colors = new Color32[38 * 38];
            var color_white = new Color32(255, 255, 255, 255);
            int s = GameItemCfg.Inst.BlurScale;

            res.Add(new Vector2Int(38, 38), null);
            res.Add(new Vector2Int(38, 37), null);
            res.Add(new Vector2Int(37, 38), null);
            res.Add(new Vector2Int(37, 37), null);

            for (int y = 0; y < w; y++)
                for (int x = 0; x < w; x++)
                {
                    var index = y * w + x;
                    if ((x <= 17 && y >= 22) || (x >= 19 && y <= 14))
                    {
                        colors[index] = default;
                    }
                    else
                    {
                        colors[index] = color_white;
                    }
                }

            foreach (var size in res.Keys.ToList())
            {
                var temp = Bilinear(colors, w, size.x, size.y);
                Get2Point(temp, size.x, out var p1, out var p2);
                var points = GetBlurPoints(temp, size.x, s, p1, p2);
                res[size] = points;
            }

            return res;
        }

        // 处理游戏物品
        public static Dictionary<Vector2Int, Dictionary<string, Color32[]>> DealGameItem()
        {
            var record = new Dictionary<Vector2Int, Dictionary<string, Color32[]>>();
            var pre_path = $"{Application.streamingAssetsPath}/GameItem/Item";
            var Config = GameItemCfg.Inst;
            int s = Config.BlurScale;


            foreach (var size in Config.BlurPoints.Keys)
            {
                record[size] = new Dictionary<string, Color32[]>();
            }

            int w = 38;
            foreach (var id in Config.IdCfg.Keys)
            {
                var path = $"{pre_path}/{id}.png";
                if (!File.Exists(path))
                    continue;

                using (var source = new Bitmap(path))   // 源图为40X40
                {
                    Rectangle rect = new Rectangle(1, 1, w, w);
                    using (Bitmap bitmap = source.Clone(rect, source.PixelFormat)) // 裁剪至38X38大小 
                    {
                        var colors = IU.BitmapToColor32(bitmap);
                        IU.Color32ReverseYAxis(colors, w);       // Y轴翻转

                        // 裁剪掉左上角数字，右下角罗马数字。
                        for (int y = 0; y < w; y++)
                            for (int x = 0; x < w; x++)
                            {
                                var index = y * w + x;
                                if ((x <= 17 && y >= 22) || (x >= 19 && y <= 14))
                                {
                                    colors[index] = default;
                                }
                            }


                        foreach (var pair in Config.BlurPoints)
                        {
                            Vector2Int size = pair.Key;
                            var points = pair.Value;
                            Color32[] deal = Bilinear(colors, w, size.x, size.y);
                            var deal_info = GetBlurValue(deal, size.x, s, points);
                            record[size][id] = deal_info;

                            // debug        绘制处理后的图像，保存本地
                            // res = Blur(res, scale_w, res_points);
                            // IU.Color32ReverseYAxis(res, scale_w);
                            // IU.SaveColor32(res, scale_w, $"{pre_path}/{id}_2.png");
                        }

                    }
                }

            }
            return record;
        }


        public static Color32[] Bilinear(Color32[] ori, int w, int r_w, int r_h)
        {
            int h = ori.Length / w;
            Color32[] result = new Color32[r_w * r_h];

            float x_ratio = (float)w / r_w;
            float y_ratio = (float)h / r_h;
            float win_size = x_ratio * y_ratio;

            float gy = 0;
            float gmy = 0;
            float gx = 0;
            float gmx = 0;
            List<(int, float)> y_cover = new List<(int, float)>();

            for (int y = 0; y < r_h; y++)
            {
                y_cover.Clear();
                gy = y * y_ratio;
                gmy = (y + 1) * y_ratio;
                int y1 = (int)gy;
                int y2 = (int)gmy;
                y2 = Math.Min(h - 1, y2);

                y_cover.Add((y1, y1 + 1 - gy));
                for (int i = y1 + 1; i < y2; i++)
                {
                    y_cover.Add((i, 1));
                }

                y_cover.Add((y2, gmy - y2));

                for (int x = 0; x < r_w; x++)
                {

                    gx = x * x_ratio;
                    gmx = (x + 1) * x_ratio;
                    int x1 = (int)gx;
                    int x2 = (int)gmx;
                    x2 = Math.Min(w - 1, x2);

                    float r = 0;
                    float g = 0;
                    float b = 0;
                    bool use_it = true;

                    for (int i = x1; i <= x2; i++)
                    {
                        float cw = 0;
                        if (i == x1)
                            cw = x1 + 1 - gx;
                        else if (i == x2)
                            cw = gmx - x2;
                        else
                            cw = 1;

                        for (int j = 0; j < y_cover.Count; j++)
                        {
                            // 面积比例
                            var item = y_cover[j];
                            var col = ori[item.Item1 * w + i];
                            if (col.a == 0)
                                use_it = false;

                            var rate = item.Item2 * cw / win_size;
                            r += col.r * rate;
                            g += col.g * rate;
                            b += col.b * rate;
                        }
                    }
                    if (use_it)
                        result[y * r_w + x] = new Color32((byte)r, (byte)g, (byte)b, 255);

                }

            }

            return result;

        }

        public static void Get2Point(Color32[] ori, int w, out Vector2Int p1, out Vector2Int p2)
        {
            p1 = Vector2Int.zero;
            p2 = Vector2Int.zero;
            int h = ori.Length / w;
            for (int x = 0; x < w; x++)
                if (ori[x].a == 0)
                {
                    p2.x = x - 1;
                    break;
                }


            for (int y = 0; y < h; y++)
                if (ori[y * w].a == 0)
                {
                    p2.y = y - 1;
                    break;
                }


            for (int x = w - 1; x >= 0; x--)
                if (ori[(h - 1) * w + x].a == 0)
                {
                    p1.x = x + 1;
                    break;
                }


            for (int y = h - 1; y >= 0; y--)
                if (ori[y * w + w - 1].a == 0)
                {
                    p1.y = y + 1;
                    break;
                }

        }


        public static List<Vector2Int> GetBlurPoints(Color32[] ori, int w, int blur_scale, Vector2Int p1, Vector2Int p2)
        {
            int s = blur_scale;              //scale
            int h = ori.Length / w;
            var res = new List<Vector2Int>();

            for (int y = p1.y; y <= h - s; y += s)
                for (int x = p1.x; x <= w - s; x += s)
                {
                    res.Add(new Vector2Int(x, y));
                }

            for (int y = p2.y - 3; y >= 0; y -= s)
                for (int x = p2.x - 3; x >= 0; x -= s)
                {
                    res.Add(new Vector2Int(x, y));
                }

            return res;
        }

        public static Color32[] GetBlurValue(Color32[] ori, int w, int blur_scale, List<Vector2Int> sample_p)
        {
            int s = blur_scale;              //scale
            int size = s * s;
            var res = new Color32[sample_p.Count];

            for (int i = 0; i < sample_p.Count; i++)
            {
                var p = sample_p[i];
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < s; dy++)
                    for (int dx = 0; dx < s; dx++)
                    {
                        var col = ori[(p.y + dy) * w + p.x + dx];
                        r += col.r;
                        g += col.g;
                        b += col.b;
                    }
                var blur_col = new Color32((byte)(r / size), (byte)(g / size), (byte)(b / size), 255);
                res[i] = blur_col;
            }
            return res;
        }


        public static void CompareTwoImage(Color32[] c1, Color32[] c2, out float average_diff, out float average_square_diff)
        {
            int count = c1.Length;
            int diff = 0;
            int square_diff = 0;

            for (int i = 0; i < count; i++)
            {
                var left = c1[i];
                var right = c2[i];

                int dr = left.r - right.r, dg = left.g - right.g, db = left.b - right.b;
                diff = diff + (dr < 0 ? -dr : dr) + (dg < 0 ? -dg : dg) + (db < 0 ? -db : db);
                square_diff = square_diff + dr * dr + dg * dg + db * db;
            }

            average_diff = (float)diff / count;
            average_square_diff = (float)square_diff / count;
        }
    }
}