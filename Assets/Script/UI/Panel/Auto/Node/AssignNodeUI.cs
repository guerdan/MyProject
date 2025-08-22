using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class AssignNodeUI : ProcessNodeUI
    {
        [Header("内容")]
        [SerializeField] private Text Formula;      // 图标
        public override void RefreshContent()
        {
            AssignOperNode data = _data as AssignOperNode;
            Formula.text = data.Formula;
        }
    }
}