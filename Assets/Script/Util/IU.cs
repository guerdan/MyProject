
using System.Collections.Generic;
using OpenCvSharp;
using Mathf = UnityEngine.Mathf;
namespace Script.Util
{
    /// <summary>
    /// Image-Utils。负责封装OpenCV接口
    /// </summary>
    public static class IU
    {
        public struct MatchResult
        {
            public OpenCvSharp.Rect Rect; // 匹配区域
            public float Score; // 匹配分数
        }

        /// <summary>
        /// 抠图匹配,使用掩码匹配  
        /// 耗时
        /// CCorrNormed--33ms--无mask是14ms    
        /// CCoeffNormed--80ms--无mask是16ms    能优化？
        /// OpenCvSharp 4.x 及以上版本,已经支持在CCoeffNormed下传入mask
        /// </summary>
        /// <param name="inputPath"></param>
        /// <param name="templatePath"></param>
        /// <returns></returns>
        public static Mat MatchTemplate1(string inputPath, string templatePath, out Mat matI, out Mat matT)
        {
            Mat result = new Mat();
            matI = Cv2.ImRead(inputPath);
            matT = Cv2.ImRead(templatePath, ImreadModes.Unchanged);
            if (matI.Width < matT.Width || matI.Height < matT.Height)
            {
                DU.MessageBox("模板图片尺寸不能大于原图！");
                return result;
            }

            Mat mask = new Mat(); // 掩码，能剪裁模版区域
            if (matT.Channels() == 4)
            {
                var channels = Cv2.Split(matT);
                mask = channels[3]; // alpha通道作为mask
                Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, matT); // 只保留BGR
            }

            //用mask比不用的耗时要大。 12ms 变 55ms
            Cv2.MatchTemplate(matI, matT, result, TemplateMatchModes.CCoeffNormed);
            return result;
        }


        public static List<MatchResult> Draw1(Mat result, int wT, int hT, float threshold = 0.9f)
        {
            List<MatchResult> matchResults = new List<MatchResult>();
            int hR, wR;
            hR = result.Rows; //继续优化了10ms 
            wR = result.Cols;

            byte[,] cull = new byte[hR, wR];    //优化了10ms. 从Mat(拆装箱)转[,]


            // 筛选所有结果，找出所有大于阈值的位置
            for (int y = 0; y < hR; y++)
            {
                for (int x = 0; x < wR; x++)
                {
                    if (cull[y, x] > 0)
                        continue; // 如果这个位置已经被标记过，跳过

                    float score = result.At<float>(y, x);

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
                                    float tscore = result.At<float>(y + i, x + j);
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
                        var rect = new OpenCvSharp.Rect(tx, ty, wT, hT);
                        var r = new MatchResult
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



        // 拷贝上半部分
        // OpenCvSharp.Rect roi = new OpenCvSharp.Rect(0, 0, matIOld.Width, matIOld.Height / 2);
        // Mat matI = new Mat(matIOld, roi).Clone(); 
    }
}