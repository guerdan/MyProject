
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// 矩形框图形，用于标注图形匹配结果的UI
    /// </summary>
    public class SquareFrameUI : MonoBehaviour
    {
        [SerializeField] private RectTransform topUI;
        [SerializeField] private RectTransform bottomUI;
        [SerializeField] private RectTransform leftUI;
        [SerializeField] private RectTransform rightUI;
        [SerializeField] private Text textUI;

        public void SetData(string text, CVRect rect, Color color = default, int text_size = 40, float border_width = 2)
        {
            textUI.text = text;
            RectTransform trans = (RectTransform)transform;
            // 4个边框UI的位置依赖于此节点
            trans.anchorMin = new Vector2(0, 1);
            trans.anchorMax = new Vector2(0, 1);
            trans.pivot = new Vector2(0, 1);
            trans.sizeDelta = new Vector2(rect.w, rect.h);
            trans.anchoredPosition = new Vector2(rect.x, -rect.y);

            topUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, border_width);
            bottomUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, border_width);
            leftUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, border_width);
            rightUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, border_width);
            // bottomUI.sizeDelta = new Vector2(0, width);  // 或者这样写

            color = color == default ? Color.red : color;
            SetColor(color);
            textUI.fontSize = text_size;

        }

        public void SetData(Vector2 pos, Vector2 size, Color color, float border_width = 2)
        {
            RectTransform rT = (RectTransform)transform;
            rT.sizeDelta = size;
            rT.anchoredPosition = pos;

            topUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, border_width);
            bottomUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, border_width);
            leftUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, border_width);
            rightUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, border_width);

            SetColor(color);
        }


        void SetColor(Color color)
        {
            topUI.GetComponent<Image>().color = color;
            bottomUI.GetComponent<Image>().color = color;
            leftUI.GetComponent<Image>().color = color;
            rightUI.GetComponent<Image>().color = color;
            if (textUI) textUI.color = color;
        }

    }
}