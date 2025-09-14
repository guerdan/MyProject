
using Script.Model.Auto;
using UnityEngine;

namespace Script.UI.Panel.Auto.Node
{
    public class MouseNodeUI : ProcessNodeUI
    {
        [Header("内容")]
        [SerializeField] private RectTransform Icon;      // 图标
        public override void RefreshContent()
        {
            MouseOperNode data = _data as MouseOperNode;
            Icon.localScale = new Vector3(data.clickType == 0 ? 1 : -1, 1, 1);
            Icon.anchoredPosition = new Vector2(data.clickType == 0 ? -3 : 3, 5);
        }
    }
}