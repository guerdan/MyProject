using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Script.Model.Auto;
using Unity.VisualScripting;
using UnityEngine;


namespace Script.Util
{
    #region CVRect

    /// <summary>
    /// 约定：基于 x轴朝右|y轴朝上的坐标系。(x,y)描述矩形的左下角坐标，(w,h)描述宽高
    /// 当CVRect参数传到OpenCV接口 或 CaptureWindow()时，会转换成:基于 x轴朝右|y轴朝下的坐标系
    /// </summary>
    public struct CVRect
    {
        public int x;
        public int y;
        public int w;
        public int h;

        public Vector2Int LeftDown => new Vector2Int(x, y);
        public Vector2Int RightUp => new Vector2Int(x + w, y + h);

        public CVRect(int x, int y, int w, int h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public static CVRect ConvertV4(Vector4 v)
        {
            return new CVRect((int)v.x, (int)v.y, (int)v.z, (int)v.w);
        }
        public static CVRect ConvertV4Bigger(Vector4 v)
        {
            return new CVRect((int)Math.Floor(v.x), (int)Math.Floor(v.y)
            , (int)Math.Ceiling(v.z), (int)Math.Ceiling(v.w));
        }


        /// <summary>
        /// 像素：获取中心点坐标，当长宽都为奇数时，最准确
        /// </summary>
        public Vector2 GetCenterPixel()
        {
            return new Vector2(x + (w / 2), y + (h / 2));
        }

        public Vector4 ToVector4()
        {
            return new Vector4(x, y, w, h);
        }

        // 重写 Equals 方法
        public override bool Equals(object obj)
        {
            if (obj is CVRect other)
            {
                return x == other.x && y == other.y && w == other.w && h == other.h;
            }
            return false;
        }

        // 重写 GetHashCode 方法
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ w.GetHashCode() ^ h.GetHashCode();
        }

        // 定义 == 运算符
        public static bool operator ==(CVRect left, CVRect right)
        {
            return left.Equals(right);
        }

        // 定义 != 运算符
        public static bool operator !=(CVRect left, CVRect right)
        {
            return !(left == right);
        }

    }

    #endregion

    public class CVMatchResult
    {
        public CVRect Rect;     // 匹配区域
        public float Score;     // 匹配分数
        public int UIType;      // 如何渲染.0-绿框，1-红框，2-蓝框，3-详情绿框
    }

    #region IU

    /// <summary>
    /// Image-Utils。负责封装OpenCV接口
    /// OpenCv 中 MatType 与 C# 数据类型对应关系:
    /// https://blog.csdn.net/lijunhe1991/article/details/147753141
    /// </summary>
    public static class IU
    {

        public static string PicPath = @"D:\unityProject\MyProject\TestResource\图";

        // 从IU坐标系 转换成 脚本坐标系并且以左下角为起点
        // **对称接口，用两次就是还原。
        public static CVRect ReverseYAndChange(CVRect source, int screenH = -1)
        {
            screenH = screenH < 0 ? Utils.ScreenHeight : screenH;
            //化简 screenH - 1 - source.y - source.h + 1 => screenH - source.y - source.h
            return new CVRect(source.x, screenH - source.y - source.h, source.w, source.h);
        }

        /// <summary>
        /// 等优化，用缓存
        /// </summary>
        public static Mat GetMat(string path, bool use_alpha = false)
        {
            var mode = use_alpha ? ImreadModes.Unchanged : ImreadModes.Color; // Color 强制3通道BGR

            byte[] data = File.ReadAllBytes(path);
            Mat mat = Cv2.ImDecode(data, mode);
            return mat;
        }

        public static void SaveMat(Mat mat, string filePath)
        {
            if (mat == null || mat.Empty())
            {
                throw new ArgumentException("Mat is null or empty!");
            }

            // 中文路径直接保存会失败。所以用File接口
            byte[] data = mat.ToBytes();
            File.WriteAllBytes(filePath, data);
        }

        public static Color32[] MatToColor32(Mat mat)
        {
            Vec3b[] data;
            mat.GetArray(out data);
            Color32[] colors = new Color32[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var pixel = data[i];
                byte blue = pixel.Item0;
                byte green = pixel.Item1;
                byte red = pixel.Item2;
                colors[i] = new Color32(red, green, blue, 255);
            }
            return colors;
        }
        public static Color32[] MatToColor32(Mat mat, CVRect region)
        {
            Vec3b[] data;
            mat.GetArray(out data);
            var total = region.w * region.h;
            Color32[] colors = new Color32[total];
            for (int i = 0; i < total; i++)
            {
                var pixel = data[i];
                byte blue = pixel.Item0;
                byte green = pixel.Item1;
                byte red = pixel.Item2;
                colors[i] = new Color32(red, green, blue, 255);
            }
            return colors;
        }

        public static byte[] Color32ToByte(Color32[] colors)
        {
            byte[] bl = new byte[colors.Length * 4];
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                bl[i * 4] = color.b;
                bl[i * 4 + 1] = color.g;
                bl[i * 4 + 2] = color.r;
                bl[i * 4 + 3] = color.a;
            }
            return bl;
        }
        public static byte[] Color32ToByteWithoutAlpha(Color32[] colors)
        {
            byte[] bl = new byte[colors.Length * 3];
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                bl[i * 3] = color.b;
                bl[i * 3 + 1] = color.g;
                bl[i * 3 + 2] = color.r;
            }
            return bl;
        }


        public static void Color32ReverseYAxis(Color32[] pixels, int w)
        {
            int h = pixels.Length / w;
            int half_h = h / 2;
            for (int i = 0; i < half_h; i++)
                for (int j = 0; j < w; j++)
                {
                    var index = i * w + j;
                    var reverse = (h - 1 - i) * w + j;
                    var temp = pixels[index];
                    pixels[index] = pixels[reverse];
                    pixels[reverse] = temp;
                }

        }

        /// <summary>
        /// mono下是jit—in-time模式，动态编译，所以内联没用(目前测的)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Color32Equal(Color32 c0, Color32 c1)
        {
            return c0.a == c1.a && c0.r == c1.r && c0.g == c1.g && c0.b == c1.b;
        }

        #region MatchTemplate
        /// <summary>
        /// 规定：use_mask && （matT有alpha通道)，才能使用模版匹配的掩码模式
        /// 
        /// 耗时
        /// CCorrNormed--33ms--无mask是14ms    
        /// CCoeffNormed--80ms--无mask是16ms     
        /// 所以建议使用CCorrNormed,从耗时上讲, 阈值应该在0.95以上
        /// </summary>
        public static Mat MatchTemplate1(Mat matI, Mat matT, bool auto_use_mask = true
                                        , TemplateMatchModes mode = TemplateMatchModes.CCorrNormed)
        {

            if (matI.Width < matT.Width || matI.Height < matT.Height)
            {
                // DU.MessageBox("模板图片尺寸不能大于原图！");
                DU.LogWarning("模板图片尺寸不能大于原图！");
                return null;
            }

            Mat result = new Mat();
            if (auto_use_mask && matT.Channels() == 4)
            {
                var channels = Cv2.Split(matT);
                using (Mat mask = channels[3]) // alpha通道作为mask。掩码，能剪裁模版区域
                {
                    using (Mat matTemplate = new Mat())
                    {
                        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, matTemplate); //用mask比不用的耗时要大。 12ms 变 55ms
                        Cv2.MatchTemplate(matI, matTemplate, result, mode, mask);
                    }
                }
            }
            else
            {
                // 遇到通道数对不上的情况，就构造通道数一致的mat。
                // 一般有些本地的模版图有alpha通道。
                if (matT.Channels() != matI.Channels())
                {
                    var channels = Cv2.Split(matT);
                    using (Mat matTemplate = new Mat())
                    {
                        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, matTemplate); // 只保留BGR
                        Cv2.MatchTemplate(matI, matTemplate, result, mode);
                    }
                }
                else
                {
                    Cv2.MatchTemplate(matI, matT, result, mode);
                }
            }

            return result;
        }

        /// <summary>
        /// mask为单通道Mat，接口会屏蔽值为0的像素，而录取非0的像素
        /// </summary>
        public static Mat MatchTemplateCustomMask(Mat matI, Mat matT, Mat mask)
        {
            if (matI.Width < matT.Width || matI.Height < matT.Height)
            {
                DU.MessageBox("模板图片尺寸不能大于原图！");
                return null;
            }

            Mat result = new Mat();
            //用mask比不用的耗时要大。 12ms 变 55ms
            Cv2.MatchTemplate(matI, matT, result, TemplateMatchModes.SqDiffNormed, mask);

            return result;
        }

        #endregion
        #region FindResult

        /// <summary>
        /// max_score_r ：当没有达到阈值时，顺带返回一个最大值结果
        /// </summary>
        public static List<CVMatchResult> FindResult(Mat result, int wT, int hT, float threshold, out CVMatchResult max_score_r)
        {
            List<CVMatchResult> matchResults = new List<CVMatchResult>();
            int hR, wR;
            hR = result.Rows; //继续优化了10ms 
            wR = result.Cols;
            int screen_height = hR + hT - 1;

            byte[,] cull = new byte[hR, wR];    //优化了10ms. 从Mat(拆装箱)转[,]
            result.GetArray(out float[] scores);
            float max_score = 0;
            int max_score_x = 0;
            int max_score_y = 0;

            // 筛选所有结果，找出所有大于阈值的位置
            for (int y = 0; y < hR; y++)
            {
                for (int x = 0; x < wR; x++)
                {
                    if (cull[y, x] > 0)
                        continue; // 如果这个位置已经被标记过，跳过

                    float score = scores[y * wR + x];

                    var tx = x;
                    var ty = y;
                    if (score > max_score)
                    {
                        max_score = score;
                        max_score_x = x;
                        max_score_y = y;
                    }

                    if (score >= threshold)
                    {
                        // 提前遍历： 下方的宽[-wT,wT]高[0,hT]矩形，并找出最大的
                        int i_max = Math.Min(hT, hR - y);
                        for (int i = 0; i < i_max; i++)  //行增
                        {
                            int j_min = Math.Max(-wT, -x);
                            int j_max = Math.Min(wT, wR - x);
                            for (int j = j_min; j < j_max; j++) //列增
                            {
                                cull[y + i, x + j] = 1;
                                float tscore = scores[(y + i) * wR + x + j];
                                if (tscore > score)
                                {
                                    tx = x + j;
                                    ty = y + i;
                                    score = tscore;
                                }

                            }
                        }

                        // 得到匹配结果
                        var rect = new CVRect(tx, ty, wT, hT);
                        rect = ReverseYAndChange(rect, screen_height);

                        var r = new CVMatchResult
                        {
                            Rect = rect,
                            Score = score
                        };
                        matchResults.Add(r);
                    }
                }
            }

            var rect1 = new CVRect(max_score_x, max_score_y, wT, hT);
            max_score_r = new CVMatchResult
            {
                Rect = rect1,
                Score = max_score
            };

            return matchResults;
        }

        // 越小越好
        public static List<CVMatchResult> FindResultMin(Mat result, int wT, int hT, float threshold, out float min)
        {
            List<CVMatchResult> matchResults = new List<CVMatchResult>();
            int hR, wR;
            hR = result.Rows; //继续优化了10ms 
            wR = result.Cols;

            byte[,] cull = new byte[hR, wR];    //优化了10ms. 从Mat(拆装箱)转[,]
            result.GetArray(out float[] scores);

            min = 100;
            // 筛选所有结果，找出所有大于阈值的位置
            for (int y = 0; y < hR; y++)
            {
                for (int x = 0; x < wR; x++)
                {
                    if (cull[y, x] > 0)
                        continue; // 如果这个位置已经被标记过，跳过

                    float score = scores[y * wR + x];

                    var tx = x;
                    var ty = y;

                    if (score < min)
                        min = score;

                    if (score <= threshold)
                    {
                        // 提前遍历： 下方的宽[-wT,wT]高[0,hT]矩形，并找出最大的
                        int i_max = Math.Min(hT, hR - y);
                        for (int i = 0; i < i_max; i++)  //行增
                        {
                            int j_min = Math.Max(-wT, -x);
                            int j_max = Math.Min(wT, wR - x);
                            for (int j = j_min; j < j_max; j++) //列增
                            {
                                cull[y + i, x + j] = 1;
                                float tscore = scores[(y + i) * wR + x + j];
                                if (tscore < score)
                                {
                                    tx = x + j;
                                    ty = y + i;
                                    score = tscore;
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

        public static void PrintScore(Mat result, string message)
        {
            result.GetArray(out float[] scores);
            Array.Sort(scores, (a, b) => b.CompareTo(a));

            var str = $"第1名:{scores[0]}; ";
            if (scores.Length > 1)
                str += $"第2名:{scores[1]}; ";
            if (scores.Length > 2)
                str += $"第3名:{scores[2]}; ";

            DU.LogWarning($"[{message}] {str} ");
        }


        #endregion


        #region Bitmap

        /// <summary>
        /// Bitmap 转 Mat；目前2ms,  ；保存读取方案60ms；逐像素读/赋值2000ms
        /// </summary>
        public static void BitmapToMat(Bitmap bitmap, out Mat result, out Vec4b[] data)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;

            // 锁定 Bitmap 的像素数据
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // 将像素数据复制到字节数组
            Mat source = Mat.FromPixelData(h, w, MatType.CV_8UC4, bitmapData.Scan0); // 4通道

            source.GetArray(out data);

            // Color32[] colors = new Color32[data.Length];         // ——转换的话，耗时30ms
            // for (var i=0; i< data.Length; i++)
            // {
            //     var pixel = data[i];
            //     byte blue = pixel.Item0;
            //     byte green = pixel.Item1;
            //     byte red = pixel.Item2;
            //     byte alpha = pixel.Item3;
            //     colors[i] = new Color32(red,green,blue,alpha);
            // }

            Cv2.CvtColor(source, source, ColorConversionCodes.BGRA2BGR);
            bitmap.UnlockBits(bitmapData);

            result = source;

            // OpenCvSharp.Extensions依赖于System.Drawing.Common.dll, 但该包不被Unity平台支持
            // Unity平台是 .Net Framework 用的是System.Drawing.dll
            //
            // return OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);

        }

        /// <summary>
        /// 返回 rgba格式的像素数组。bitmap.PixelFormat 必须是 Format32bppArgb
        /// </summary>
        public static Color32[] BitmapToColor32(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            // 锁定 Bitmap 的像素数据
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            int h_stride = bitmapData.Stride;
            int stride = h_stride / w;
            // 获取像素数组
            int byteCount = h_stride * h;
            byte[] pixels = new byte[byteCount];

            // 将像素数据复制到字节数组
            Marshal.Copy(bitmapData.Scan0, pixels, 0, byteCount);

            // 解锁 Bitmap
            bitmap.UnlockBits(bitmapData);

            int len = w * h;
            Color32[] colors = new Color32[len];

            // bitmapData.Stride在设计上被补齐到4的倍数字节，所以当stride = 3时要舍弃"用于补齐"的字节
            //
            if (stride == 3)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int start = y * h_stride + x * stride;
                        colors[y * w + x] =
                            new Color32(pixels[start + 2], pixels[start + 1], pixels[start + 0], 255);
                    }

            }
            else
            {
                for (int i = 0; i < len; i++)
                {
                    colors[i] = new Color32(pixels[i * 4 + 2], pixels[i * 4 + 1], pixels[i * 4 + 0], pixels[i * 4 + 3]);
                }
            }



            return colors;
        }


        public static void SaveBitmap(Bitmap bitmap, string path)
        {
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            bitmap.Save(path, ImageFormat.Png);
        }
        public static void SaveColor32(Color32[] colors, int w, string path)
        {
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int h = colors.Length / w;
            byte[] bytes = Color32ToByte(colors);
            using (Mat mat = Mat.FromPixelData(h, w, MatType.CV_8UC4, bytes))
            {
                SaveMat(mat, path);
            }
        }

        /// <summary>
        /// 读取图片，裁剪所需的区域。
        /// </summary>
        public static Bitmap CutOutImage(Bitmap bitmap, CVRect region)
        {

            Rectangle rect = new Rectangle(region.x, region.y, region.w, region.h);
            Bitmap cut_bitmap = bitmap.Clone(rect, bitmap.PixelFormat);
            return cut_bitmap;
        }

        #endregion

        #region Vec3b

        static Dictionary<string, Vec3bImage> _Vec3bImageCache = new Dictionary<string, Vec3bImage>();
        public static Vec3bImage GetVec3BImage(string path, bool use_cache = true)
        {
            if (use_cache)
            {
                if (!_Vec3bImageCache.TryGetValue(path, out var result))
                {
                    using (var mat = GetMat(path))
                    {
                        mat.GetArray<Vec3b>(out var colors);
                        result = new Vec3bImage(colors, mat.Width, mat.Height);
                        _Vec3bImageCache[path] = result;
                    }
                }
                return result;
            }
            else
            {
                using (var mat = GetMat(path))
                {
                    mat.GetArray<Vec3b>(out var colors);
                    var result = new Vec3bImage(colors, mat.Width, mat.Height);
                    return result;
                }
            }

        }


        #endregion

    }

    #endregion



}