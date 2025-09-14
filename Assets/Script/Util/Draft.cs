
using System.Collections.Generic;
using OpenCvSharp;
using static Script.Util.IU;
using Mathf = UnityEngine.Mathf;

namespace Script.Util
{
    public static class Draft
    {
         
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

    }
}