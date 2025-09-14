
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class PicMatchFloat : BasePanel
    {
        [SerializeField] private RectTransform NInput;
        [SerializeField] private RectTransform NTemplate;
        [SerializeField] private ImageLoadComp IInput;
        [SerializeField] private ImageLoadComp ITemplate;
        [SerializeField] private float threshold = 0.9f;

        private List<SquareFrameUI> frameUIList = new List<SquareFrameUI>();
        void Awake()
        {

        }


        public override void SetData(object data)
        {
            //有两个图片地址
            if (data is not List<string> list || list.Count != 2)
            {
                DU.MessageBox($"{PanelDefine.Name} SetData类型错误");
                return;
            }

            string inputPath = list[0];
            string templatePath = list[1];
            inputPath = Path.Combine(Application.streamingAssetsPath, inputPath);
            templatePath = Path.Combine(Application.streamingAssetsPath, templatePath);


            Action reSize = () =>
            {
                float w0 = NInput.sizeDelta.x;
                float h0 = NInput.sizeDelta.y;
                float w1 = NTemplate.sizeDelta.x;
                float h1 = NTemplate.sizeDelta.y;
                ((RectTransform)transform).sizeDelta = new Vector2(w0 + 360, h0 + 130);
            };

            IInput.SetData(inputPath);
            ITemplate.SetData(templatePath);

            reSize();

            DU.StartTimer();
            // 匹配结果
            var matI = IU.GetMat(inputPath);
            var matT = IU.GetMat(templatePath, true);
            Mat result = IU.MatchTemplate1(matI, matT);
            DU.Log(DU.StopTimer($"MatchTemplate"));


            DU.StartTimer();

            var result_list = IU.FindResult(result, matT.Width, matT.Height, threshold);

            AssetManager.Inst.LoadAssetAsync<GameObject>(PathUtil.SquareFrameUIPath, (go) =>
            {
                Utils.RefreshItemListByCount(frameUIList, result_list.Count, go, NInput, (item, index) =>
                {
                    var matchResult = result_list[index];
                    item.SetData(matchResult.Score, matchResult.Rect);
                });
            }, this);
            DU.Log(DU.StopTimer($"筛选"));


            // result_list.ForEach(matchResult =>
            // {
            //     var rect = matchResult.Rect;
            //     var r = new OpenCvSharp.Rect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            //     // 在原图上画出匹配区域
            //     Cv2.Rectangle(matI, r, Scalar.Red, 2);
            //     // 在左上角标注分数
            //     Cv2.PutText(matI, DU.FloatFormat(matchResult.Score, 2), new OpenCvSharp.Point(r.X, r.Y + 15),
            //         HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);

            // });


            // 找到最佳匹配位置
            // Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

            // Cv2.ImShow("Match Result", matI);
            _bitmap = WU.CaptureWindow(new CVRect(0, 0, Screen.width, Screen.height));


            // Cv2.ImShow("Match Result", cap0);
        }

        Bitmap _bitmap;


        public override void Close()
        {
            base.Close();
            Mat cap0 = IU.BitmapToMat(_bitmap);
            Cv2.ImShow("Match Result", cap0);

        }

    }
}