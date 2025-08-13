
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ProcessNodeLineUI : MonoBehaviour, IPointerDownHandler
    {
        Image img;
        string _id;
        string _from;
        string _to;
        public bool _isTrue;
        DrawProcessPanel _panel;
        Color _normalColor;


        public void Awake()
        {
            img = GetComponent<Image>();
        }

        public void SetData(string from, string to, bool isTrue, DrawProcessPanel panel)
        {
            _from = from;
            _to = to;
            _isTrue = isTrue;
            _panel = panel;
            _id = $"line-{from}-{to}";
        }
        
        
        //只刷新颜色
        public void RefreshForSelected()
        {
            if (_id == _panel.MouseSelectedId)
            {
                img.color = ProcessNodeUI.WhiteColor; // 选中时变白
            }
            else
            {
                img.color = _normalColor; // 恢复正常颜色
            }
        }


        /// <summary>
        /// 线段塑形-画任意线段。image，起点，终点，颜色
        /// </summary>
        public void DrawLine(Vector2 from, Vector2 to, Color color, float thickness = 6)
        {
            img.color = color;
            _normalColor = color;
            var line = GetComponent<RectTransform>();
            // 计算两点之间的中心点
            Vector2 center = (from + to) / 2f;

            // 设置RectTransform的位置为中心点
            line.anchoredPosition = center;

            // 计算两点之间的距离（线段长度）
            float distance = Vector2.Distance(from, to);

            // 如果线段是12px高度
            var scale = thickness / 12;
            line.localScale = Vector3.one * scale;

            // 设置RectTransform的尺寸：长度为两点距离，宽度为线段厚度
            line.sizeDelta = new Vector2(distance / scale, 12);

            // 计算线段的角度（弧度转角度）
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

            // 设置旋转角度
            line.rotation = Quaternion.Euler(0, 0, angle);
        }

        // 选中线段，此线段变白
        public void OnPointerDown(PointerEventData eventData)
        {
            string pre = _panel.MouseSelectedId;
            _panel.MouseSelectedId = _id;
            // 通知刷新旧选中的ui 和新选中的ui
            _panel.RefreshUISelectedStatus(pre);
            _panel.RefreshUISelectedStatus(_panel.MouseSelectedId);

        }

    }
}