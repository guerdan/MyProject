using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;
using UnityEngine;


namespace Script.Util
{
    #region CVRect

    /// <summary>
    /// 与openCV 的 Rect 意义相同的结构体。在左上原点的坐标系下，左上角坐标为(x,y),宽高为(w,h)
    /// </summary>
    public struct CVRect
    {
        public float x;
        public float y;
        public float w;
        public float h;

        public Vector2 LeftTop => new Vector2(x, y);
        public Vector2 RightBottom => new Vector2(x + w, y + h);

        public CVRect(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public CVRect(Vector2 left_top, Vector2 right_bottom)
        {
            this.x = left_top.x;
            this.y = left_top.y;
            this.w = right_bottom.x - left_top.x;
            this.h = right_bottom.y - left_top.y;
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
        public int UIType;      // 如何渲染.0-红框，1-绿框，2-蓝框
    }


    /// <summary>
    /// Image-Utils。负责封装OpenCV接口
    /// OpenCv 中 MatType 与 C# 数据类型对应关系:
    /// https://blog.csdn.net/lijunhe1991/article/details/147753141
    /// </summary>
    public static class IU
    {

        // 等优化，用缓存
        public static Mat GetMat(string path, bool use_alpha = false)
        {
            var mode = use_alpha ? ImreadModes.Unchanged : ImreadModes.Color; // Color 强制3通道BGR
            return Cv2.ImRead(path, mode);
        }
        #region MatchTemplate
        /// <summary>
        /// 抠图匹配,使用掩码匹配  
        /// 耗时
        /// CCorrNormed--33ms--无mask是14ms    
        /// CCoeffNormed--80ms--无mask是16ms     
        /// 所以建议使用CCorrNormed,从耗时上讲, 阈值应该在0.95以上
        ///  
        /// OpenCvSharp 4.x 及以上版本,已经支持在CCoeffNormed下传入mask
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="templatePath"></param>
        /// <returns></returns>
        public static Mat MatchTemplate1(Mat matI, Mat matT)
        {
            // 可以做缓存图片来优化
            //

            if (matI.Width < matT.Width || matI.Height < matT.Height)
            {
                DU.MessageBox("模板图片尺寸不能大于原图！");
                return null;
            }

            Mat result = new Mat();
            Mat mask = new Mat(); // 掩码，能剪裁模版区域
            if (matT.Channels() == 4)
            {
                var channels = Cv2.Split(matT);
                mask = channels[3]; // alpha通道作为mask
                Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, matT); // 只保留BGR
            }

            //用mask比不用的耗时要大。 12ms 变 55ms
            Cv2.MatchTemplate(matI, matT, result, TemplateMatchModes.CCorrNormed, mask);

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

            byte[,] cull = new byte[hR, wR];    //优化了10ms. 从Mat(拆装箱)转[,]
            result.GetArray(out float[] scores);
            float max_score = 0;
            float max_score_x = 0;
            float max_score_y = 0;

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
        #endregion


        #region Bitmap

        /// <summary>
        /// Bitmap 转 Mat；目前2ms,  保存读取方案60ms,   逐像素读/赋值2000ms
        /// </summary>
        public static Mat BitmapToMat(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;

            // 锁定 Bitmap 的像素数据
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // 将像素数据复制到字节数组
            Mat source = Mat.FromPixelData(h, w, MatType.CV_8UC4, bitmapData.Scan0); // 4通道
            Cv2.CvtColor(source, source, ColorConversionCodes.BGRA2BGR);

            bitmap.UnlockBits(bitmapData);

            return source;

            // OpenCvSharp.Extensions依赖于System.Drawing.Common.dll, 但该包不被Unity平台支持
            // Unity平台是 .Net Framework 用的是System.Drawing.dll
            //
            // return OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);

        }

        public static void SaveBitmap(Bitmap bitmap, string folder, string name)
        {
            string dir = Path.Combine(Application.streamingAssetsPath, folder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, name + ".png");
            bitmap.Save(path, ImageFormat.Png);
        }

        /// <summary>
        /// 读取图片，裁剪所需的区域。
        /// </summary>
        public static Bitmap CutOutImage(Bitmap bitmap, CVRect region)
        {
            Rectangle rect = new Rectangle((int)region.x, (int)region.y, (int)region.w, (int)region.h);
            Bitmap cut_bitmap = bitmap.Clone(rect, bitmap.PixelFormat);
            return cut_bitmap;
        }

        #endregion
    }
}