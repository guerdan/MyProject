using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class KeyboardNodeUI : ProcessNodeUI
    {
        [Header("内容")]
        [SerializeField] private Text Key;      // 图标
        public override void RefreshContent()
        {
            KeyBoardOperNode data = _data as KeyBoardOperNode;
            Key.text = data.Key;
        }
    }
}