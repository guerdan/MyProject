
using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class MouseNodeUI : ProcessNodeUI
    {
        [Header("内容")]
        [SerializeField] private RectTransform IconRT;    // 图标
        [SerializeField] private Sprite Sprite_MouseClick;        // 图标
        [SerializeField] private Sprite Sprite_MouseMove;        // 图标
        [SerializeField] private Sprite Sprite_StopScript;        // 图标

        public override void RefreshContent()
        {
            if (_data is MouseOperNode mouseNode)
            {
                var type = mouseNode.ClickType;
                IconRT.GetComponent<Image>().sprite = type == 2 ? Sprite_MouseMove : Sprite_MouseClick;
                IconRT.localScale = new Vector3(mouseNode.ClickType == 0 ? 1 : -1, 1, 1);
                IconRT.anchoredPosition = new Vector2(mouseNode.ClickType == 0 ? -3 : 3, 5);
                IconRT.sizeDelta = new Vector2(27.5f, 40.25f);
            }
            else if (_data is StopScriptNode stopNode)
            {
                IconRT.GetComponent<Image>().sprite = Sprite_StopScript;
                IconRT.localScale = Vector3.one;
                IconRT.anchoredPosition = new Vector2(0, 3);
                IconRT.sizeDelta = new Vector2(25.6f, 30.8f);
            }

        }
    }
}