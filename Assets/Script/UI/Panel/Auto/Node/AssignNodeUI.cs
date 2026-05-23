using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class AssignNodeUI : ProcessNodeUI
    {
        public static readonly Color TypeTextColor1;
        public static readonly Color TypeBgColor1;
        public static readonly Color TypeLineColor1;
        public static readonly Color TypeTextColor2;
        public static readonly Color TypeBgColor2;
        public static readonly Color TypeLineColor2;
        public static readonly Color TypeTextColor3;
        public static readonly Color TypeBgColor3;
        public static readonly Color TypeLineColor3;
        public static readonly Color TypeBgColor4;
        public static readonly Color TypeLineColor4;

        static AssignNodeUI()
        {
            TypeTextColor1 = Utils.ParseHtmlString("#FFFFFF");
            TypeBgColor1 = Utils.ParseHtmlString("#F1BA02");
            TypeLineColor1 = Utils.ParseHtmlString("#E0AB00");

            TypeTextColor2 = Utils.ParseHtmlString("#E8FFE3");
            TypeBgColor2 = Utils.ParseHtmlString("#4c7543");
            TypeLineColor2 = Utils.ParseHtmlString("#42653b");

            TypeTextColor3 = Utils.ParseHtmlString("#e6f1ff");
            TypeBgColor3 = Utils.ParseHtmlString("#406087");
            TypeLineColor3 = Utils.ParseHtmlString("#395576");

            TypeBgColor4 = Utils.ParseHtmlString("#4F230F");
            TypeLineColor4 = Utils.ParseHtmlString("#B25327");
        }

        [Header("扩展内容")]
        [SerializeField] private Text Formula;      // 公式 
        [SerializeField] private Text TypeText;     // 类型：条件/触发事件/监听事件 
        [SerializeField] private Image TypeBg;      // 图 
        [SerializeField] private Image TypeLine;    // 图 
        [SerializeField] private Image TypeIcon;      // 图 


        public override void RefreshContent()
        {
            base.RefreshContent();
            Utils.SetActive(DelayText, _data.NodeType == NodeType.CaptureOper);

            if (_data.NodeType == NodeType.AssignOper)
            {
                AssignOperNode data = _data as AssignOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.Formula);
                ShowType(false,false);
            }

            else if (_data.NodeType == NodeType.CaptureOper)
            {
                CaptureOperNode data = _data as CaptureOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.RegionsExpression);
                ShowType(true, true);
                TypeBg.color = TypeBgColor4;
                TypeLine.color = TypeLineColor4;
            }

            else if (_data.NodeType == NodeType.ConditionOper)
            {
                ConditionOperNode data = _data as ConditionOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.Formula);
                ShowType(true, false);
                TypeText.text = "条件";
                TypeText.color = TypeTextColor1;
                TypeBg.color = TypeBgColor1;
                TypeLine.color = TypeLineColor1;
            }
            else if (_data.NodeType == NodeType.ForOper)
            {
                ForOperNode data = _data as ForOperNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.Formula);
                ShowType(true, false);
                TypeText.text = "For";
                TypeText.color = TypeTextColor1;
                TypeBg.color = TypeBgColor1;
                TypeLine.color = TypeLineColor1;
            }

            else if (_data.NodeType == NodeType.TriggerEvent)
            {
                TriggerEventNode data = _data as TriggerEventNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.EventName);
                ShowType(true, false);
                TypeText.text = "触发";
                TypeText.color = TypeTextColor2;
                TypeBg.color = TypeBgColor2;
                TypeLine.color = TypeLineColor2;
            }

            else if (_data.NodeType == NodeType.ListenEvent)
            {
                ListenEventNode data = _data as ListenEventNode;
                Formula.text = AutoDataUIConfig.FormulaFormat(data.EventName);
                ShowType(true, false);
                TypeText.text = "监听";
                TypeText.color = TypeTextColor3;
                TypeBg.color = TypeBgColor3;
                TypeLine.color = TypeLineColor3;
            }
        }

        void ShowType(bool show, bool show_icon)
        {
            TypeBg.gameObject.SetActive(show);
            TypeLine.gameObject.SetActive(show);
            TypeText.gameObject.SetActive(show && !show_icon);
            TypeIcon.gameObject.SetActive(show && show_icon);
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

        // void ShowIsCondition(bool isCondition)
        // {
        //     TopCircle.gameObject.SetActive(isCondition);
        //     BottomCircle.gameObject.SetActive(isCondition);
        // }

        // public override void RefreshSlotUI()
        // {
        //     base.RefreshSlotUI();

        //     if (_data is ConditionOperNode)
        //     {
        //         var pos0 = _panel.GetLineEndPos(_data, 1) - selfR.anchoredPosition;
        //         TrueCircleTextR.anchoredPosition = GetTextPos(pos0);

        //         var pos1 = _panel.GetLineEndPos(_data, 2) - selfR.anchoredPosition;
        //         FalseCircleTextR.anchoredPosition = GetTextPos(pos1);
        //         var pos2 = _panel.GetLineEndPos(_data, 0) - selfR.anchoredPosition;
        //         InCircleTextR.anchoredPosition = GetTextPos(pos2, 2);
        //     }
        // }



    }
}