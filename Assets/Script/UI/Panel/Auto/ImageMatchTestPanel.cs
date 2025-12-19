using System.Collections;
using System.Collections.Generic;
using OpenCvSharp;
using Script.Framework.UI;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ImageMatchTestPanel : BasePanel
    {
        [SerializeField] private RectTransform Panel;
        [SerializeField] private Button SourceBtn;
        [SerializeField] private Button TemplateBtn;
        [SerializeField] private ImageLoadComp SourceImage;
        [SerializeField] private ImageLoadComp TemplateImage;
        [SerializeField] private Text ResultText;

        string source_path;
        string template_path;

        List<SquareFrameUI> _itemList = new List<SquareFrameUI>();

        void Awake()
        {
            _useScaleAnim = false;
            SourceBtn.onClick.AddListener(OnClickSourceBtn);
            TemplateBtn.onClick.AddListener(OnClickTemplateBtn);

            ResultText.text = "请先选择图片";
        }
        public override void SetData(object data)
        {

        }

        void Refresh()
        {

            SourceImage.SetData(source_path, default, false);
            TemplateImage.SetData(template_path, default, false);
            var s_size = SourceImage.GetSize();
            var t_size = TemplateImage.GetSize();
            var max_h = Mathf.Max(s_size.y, t_size.y);
            var target = new Vector2(242 + s_size.x + t_size.x, max_h + 306);


            // 设置界面宽高
            target = new Vector2(Mathf.Max(target.x, 480), Mathf.Max(target.y, 500));
            Panel.sizeDelta = target;


            // 输出结果,取最高三位的结果
            if (string.IsNullOrEmpty(source_path) || string.IsNullOrEmpty(template_path))
            {
                ResultText.text = "请先选择图片";
                return;
            }
            Mat s_mat = IU.GetMat(source_path, true);
            Mat t_mat = IU.GetMat(template_path, true);
            Mat r_mat;
            List<CVMatchResult> results;

            bool positive = false;
            if (positive)
            {
                r_mat = IU.MatchTemplate1(s_mat, t_mat);
                // 先以0为阈值
                results = IU.FindResult(r_mat, t_mat.Width, t_mat.Height, 0f, out var _);
                results.Sort((a, b) =>
                {
                    if (a.Score != b.Score)
                        return b.Score - a.Score > 0 ? 1 : -1;
                    return 0;
                });
            }
            else
            {
                r_mat = IU.MatchTemplateCustomMask(s_mat, t_mat, t_mat);
                results = IU.FindResultMin(r_mat, t_mat.Width, t_mat.Height, 0.6f);
                results.Sort((a, b) =>
                {
                    if (a.Score != b.Score)
                        return b.Score - a.Score < 0 ? 1 : -1;
                    return 0;
                });
            }


            DU.Log($"匹配到{results.Count}个结果");

            if (results.Count >= 3)
            {
                results = results.GetRange(0, 3);
            }

            string str = "";
            int index = 0;
            if (results.Count > 0)
            {
                index = 0;
                var item = results[index];
                str += $"第{index + 1}名，分数<color='#069D00'>{DU.FloatFormat(item.Score, 2)}</color>"
                + $"，坐标P({(int)item.Rect.x},{(int)item.Rect.y})\n";
            }
            if (results.Count > 1)
            {
                index = 1;
                var item = results[index];
                str += $"第{index + 1}名，分数<color='#069D00'>{DU.FloatFormat(item.Score, 2)}</color>"
                + $"，坐标P({(int)item.Rect.x},{(int)item.Rect.y})\n";
            }
            if (results.Count > 2)
            {
                index = 2;
                var item = results[index];
                str += $"第{index + 1}名，分数<color='#069D00'>{DU.FloatFormat(item.Score, 2)}</color>"
                + $"，坐标P({(int)item.Rect.x},{(int)item.Rect.y})\n";
            }

            ResultText.text = str;

            StartCoroutine(Delay(results, s_size));
        }

        IEnumerator Delay(List<CVMatchResult> results, Vector2 s_size)
        {
            yield return null; // 等一帧，位置设定结束

            var worldPos = SourceImage.Image.transform.position;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPos);

            foreach (var item in results)
            {
                var rect = item.Rect;
                rect.x = (int)(screenPoint.x - s_size.x * 0.5f) + rect.x;
                rect.y = (int)(Screen.height - (screenPoint.y + s_size.y * 0.5f)) + rect.y;
                item.Rect = rect;
            }

            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { results, 1000.0f });
        }

        void OnClickSourceBtn()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            source_path = path;
            Refresh();
        }
        void OnClickTemplateBtn()
        {
            string path = WU.OpenFileDialog("选择图片", Application.streamingAssetsPath, "图片 *.png *.jpg)|*.png;*.jpg");
            if (string.IsNullOrEmpty(path)) return;

            template_path = path;
            Refresh();
        }

        void OnDisable()
        {
            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { null, 0 });
        }

    }
}