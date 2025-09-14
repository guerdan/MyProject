using Script.Model.Auto;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class AssignNodeUI : ProcessNodeUI
    {
        [Header("内容")]
        [SerializeField] private Text Formula;      // 公式 
        [SerializeField] private Text TypeText;     // 类型：条件/触发事件/监听事件 
        [SerializeField] private Image TypeBg;      // 图 
        [SerializeField] private Image TypeLine;    // 图 

        [SerializeField] public CheckBox TopCircle;       // 上圈圈
        [SerializeField] public CheckBox BottomCircle;    // 下圈圈

        public override void RefreshContent()
        {
            if (_data is AssignOperNode)
            {
                AssignOperNode data = _data as AssignOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.Formula);
                ShowType(false);
                ShowIsCondition(false);
            }

            else if (_data is ConditionOperNode)
            {
                ConditionOperNode data = _data as ConditionOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.Formula);
                ShowType(true);
                ShowIsCondition(true);
                TypeText.text = "条件";
                TypeText.color = Utils.ParseHtmlString("#FFF7DC");
                TypeBg.color = Utils.ParseHtmlString("#F1BA02");
                TypeLine.color = Utils.ParseHtmlString("#E0AB00");
            }

            else if (_data is TriggerEventNode)
            {
                TriggerEventNode data = _data as TriggerEventNode;
                Formula.text = data.EventName;
                ShowType(true);
                ShowIsCondition(false);
                TypeText.text = "触发";
                TypeText.color = Utils.ParseHtmlString("#E8FFE3");
                TypeBg.color = Utils.ParseHtmlString("#4c7543");
                TypeLine.color = Utils.ParseHtmlString("#42653b");
            }

            else if (_data is ListenEventNode)
            {
                ListenEventNode data = _data as ListenEventNode;
                Formula.text = data.EventName;
                ShowType(true);
                ShowIsCondition(false);
                TypeText.text = "监听";
                TypeText.color = Utils.ParseHtmlString("#e6f1ff");
                TypeBg.color = Utils.ParseHtmlString("#406087");
                TypeLine.color = Utils.ParseHtmlString("#395576");
            }
        }

        void ShowType(bool show)
        {
            TypeText.gameObject.SetActive(show);
            TypeBg.gameObject.SetActive(show);
            TypeLine.gameObject.SetActive(show);
            var rect = Formula.GetComponent<RectTransform>();
            if (show)
            {
                rect.offsetMin = new Vector2(85, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-35, rect.offsetMax.y);
            }
            else
            {
                rect.offsetMin = new Vector2(20, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-20, rect.offsetMax.y);
            }
        }

        void ShowIsCondition(bool isCondition)
        {
            TopCircle.gameObject.SetActive(isCondition);
            BottomCircle.gameObject.SetActive(isCondition);
        }

        public override void RefreshSelected()
        {
            if (_data is ConditionOperNode)
            {
                bool selected = _data.Id == _panel.MouseSelectedId;
                TopCircle.SetData(selected);
                BottomCircle.SetData(selected);
            }
            else
            {
                base.RefreshSelected();
            }
        }
    }
}