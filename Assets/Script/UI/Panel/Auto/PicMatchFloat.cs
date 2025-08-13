
using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private Image IInput;
        [SerializeField] private Image ITemplate;
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

            bool complete0 = false;
            bool complete1 = false;
            Action reSize = () =>
            {
                if (!(complete0 && complete1)) return;
                float w0 = NInput.sizeDelta.x;
                float h0 = NInput.sizeDelta.y;
                float w1 = NTemplate.sizeDelta.x;
                float h1 = NTemplate.sizeDelta.y;
                ((RectTransform)transform).sizeDelta = new Vector2(w0 + 360, h0 + 130);
            };

            StartCoroutine(LoadSpriteInStreaming(inputPath, (spr) =>
            {
                IInput.sprite = spr;
                NInput.sizeDelta = new Vector2(spr.rect.width, spr.rect.height);
                complete0 = true;
                reSize();
            }));
            StartCoroutine(LoadSpriteInStreaming(templatePath, (spr) =>
            {
                ITemplate.sprite = spr;
                NTemplate.sizeDelta = new Vector2(spr.rect.width, spr.rect.height);
                complete1 = true;
                reSize();
            }));


            DU.StartTimer();
            // 匹配结果
            Mat result = IU.MatchTemplate1(inputPath, templatePath, out var matI, out var matT);
            DU.Log(DU.StopTimer($"MatchTemplate"));


            DU.StartTimer();

            var result_list = IU.Draw1(result, matT.Width, matT.Height, threshold);

            AssetManager.Inst.LoadAssetAsync<GameObject>(PathUtil.SquareFrameUIPath, (go) =>
            {
                Utils.RefreshItemListByCount(frameUIList, result_list.Count, go, NInput, (item, index) =>
                {
                    var matchResult = result_list[index];
                    item.SetData(matchResult.Score, Utils.ConvertRect(matchResult.Rect));
                });
            }, this);


            result_list.ForEach(matchResult =>
            {
                var rect = matchResult.Rect;
                // 在原图上画出匹配区域
                Cv2.Rectangle(matI, rect, Scalar.Red, 2);
                // 在左上角标注分数
                Cv2.PutText(matI, DU.FloatFormat(matchResult.Score, 2), new OpenCvSharp.Point(rect.X, rect.Y + 15),
                    HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 2);

            });

            DU.Log(DU.StopTimer($"筛选"));

            // 找到最佳匹配位置
            // Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

            // Cv2.ImShow("Match Result", matI);
        }

        // 资源加载何时释放，托管的没有引用就释放。
        public IEnumerator LoadSpriteInStreaming(string fileName, Action<Sprite> cb)
        {
            byte[] bytes = File.ReadAllBytes(fileName);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            cb(sprite);
            yield break;
        }


    }
}