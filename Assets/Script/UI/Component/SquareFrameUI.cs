
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

        public void SetData(float score, Rect rect, float width = 2)
        {
            textUI.text = DU.FloatFormat(score,2);

            RectTransform trans = (RectTransform)transform;
            // 4个边框UI的位置依赖于此节点
            trans.anchorMin = new Vector2(0, 1);
            trans.anchorMax = new Vector2(0, 1);
            trans.pivot = new Vector2(0, 1);
            trans.sizeDelta = new Vector2(rect.width, rect.height);
            trans.anchoredPosition = new Vector2(rect.x, rect.y);

            topUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width);
            bottomUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width);
            leftUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rightUI.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            // bottomUI.sizeDelta = new Vector2(0, width);  // 或者这样写

        }

    }
}