
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
        float _border_width = 2;

        public void SetData(string text, CVRect rect, Color color = default, int text_size = 40, float border_width = 2)
        {
            textUI.text = text;
            _border_width = border_width;
            RectTransform trans = (RectTransform)transform;
            // 4个边框UI的位置依赖于此节点
            trans.anchorMin = new Vector2(0, 1);
            trans.anchorMax = new Vector2(0, 1);
            trans.pivot = new Vector2(0, 1);
            trans.sizeDelta = new Vector2(rect.w, rect.h);
            trans.anchoredPosition = new Vector2(rect.x, -rect.y);

            topUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _border_width);
            bottomUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _border_width);
            leftUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _border_width);
            rightUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _border_width);
            // bottomUI.sizeDelta = new Vector2(0, width);  

            color = color == default ? Color.red : color;
            SetColor(color);
            textUI.fontSize = text_size;

        }

        public void SetData(Vector2 pos, Vector2 size, Color color, float border_width = 2)
        {
            _border_width = border_width;
            RectTransform rT = (RectTransform)transform;
            rT.sizeDelta = size;
            rT.anchoredPosition = pos;

            topUI.sizeDelta = new Vector2(_border_width * 2, _border_width);
            bottomUI.sizeDelta = new Vector2(_border_width * 2, _border_width);
            leftUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _border_width);
            rightUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _border_width);

            SetColor(color);
        }

        public void ShowBorder(int tag)
        {
            bottomUI.gameObject.SetActive((tag & 1) != 0);

            leftUI.gameObject.SetActive((tag & 2) != 0);
            topUI.gameObject.SetActive((tag & 4) != 0);
            rightUI.gameObject.SetActive((tag & 8) != 0);

            Vector2 temp;
            //左下角
            temp = bottomUI.offsetMin;
            temp.x = (tag & 16) != 0 ? -_border_width : 0;
            bottomUI.offsetMin = temp;

            temp = topUI.offsetMin;
            temp.x = (tag & 32) != 0 ? -_border_width : 0;
            topUI.offsetMin = temp;

            temp = topUI.offsetMax;
            temp.x = (tag & 64) != 0 ? _border_width : 0;
            topUI.offsetMax = temp;

            temp = bottomUI.offsetMax;
            temp.x = (tag & 128) != 0 ? _border_width : 0;
            bottomUI.offsetMax = temp;


            // topUI.offsetMin = (tag & 32) != 0 ? new Vector2(0, 0) : new Vector2(-_border_width, 0);
            // topUI.offsetMax = (tag & 64) != 0 ? new Vector2(0, 0) : new Vector2(-_border_width, 0);
            // bottomUI.offsetMax = (tag & 128) != 0 ? new Vector2(0, 0) : new Vector2(-_border_width, 0);
        }

        /// <summary>
        /// 设置参照原点。=>设置锚点
        /// </summary>
        public void SetAnchor(Vector2 anchor)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
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