
using Script.Model.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class MouseNodeUI : ProcessNodeUI
    {
        [Header("扩展内容")]
        [SerializeField] private RectTransform IconRT;    // 图标
        [SerializeField] private Sprite Sprite_MouseClick;        // 图标
        [SerializeField] private Sprite Sprite_MouseMove;        // 图标
        [SerializeField] private Sprite Sprite_StopScript;        // 图标


        [SerializeField] private Text Title;                      // 图标


        public override void RefreshContent()
        {
            base.RefreshContent();
            if (_data.NodeType == NodeType.MouseOper)
            {
                Utils.SetActive(IconRT, true);
                Utils.SetActive(Title, false);

                var node = _data as MouseOperNode;
                var type = node.ClickType;
                IconRT.GetComponent<Image>().sprite = type == 2 ? Sprite_MouseMove : Sprite_MouseClick;
                IconRT.localScale = new Vector3(node.ClickType == 0 ? 1 : -1, 1, 1);
                IconRT.anchoredPosition = new Vector2(node.ClickType == 0 ? -3 : 3, 5);
                IconRT.sizeDelta = new Vector2(27.5f, 40.25f);
            }
            else if (_data.NodeType == NodeType.StopScript)
            {
                Utils.SetActive(IconRT, true);
                Utils.SetActive(Title, false);
                IconRT.GetComponent<Image>().sprite = Sprite_StopScript;
                IconRT.localScale = Vector3.one;
                IconRT.anchoredPosition = new Vector2(0, 3);
                IconRT.sizeDelta = new Vector2(25.6f, 30.8f);
            }
            else if (_data.NodeType == NodeType.KeyBoardOper)
            {
                Utils.SetActive(IconRT, false);
                Utils.SetActive(Title, true);

                var node = _data as KeyBoardOperNode;
                Title.text = node.Key;
            }
            else if (_data.NodeType == NodeType.WaitOper)
            {
                Utils.SetActive(IconRT, false);
                Utils.SetActive(Title, true);
                Title.text = "待";
            }

        }
    }
}