
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
            if (_data is MouseOperNode mouseNode)
            {
                Icon.localScale = new Vector3(mouseNode.ClickType == 0 ? 1 : -1, 1, 1);
                Icon.anchoredPosition = new Vector2(mouseNode.ClickType == 0 ? -3 : 3, 5);
            }
            // 接收难民
            else if (_data is MapCaptureNode mapCaptureNode)
            {

            }
        }
    }
}