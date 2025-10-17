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

        RectTransform TopCircleR;
        RectTransform TopCircleTextR;
        RectTransform BottomCircleR;
        RectTransform BottomCircleTextR;



        protected void Awake()
        {
            base.Awake();
            TopCircleR = (RectTransform)TopCircle.transform;
            TopCircleTextR = (RectTransform)TopCircle.GetComponentInChildren<Text>().transform;
            BottomCircleR = (RectTransform)BottomCircle.transform;
            BottomCircleTextR = (RectTransform)BottomCircle.GetComponentInChildren<Text>().transform;
        }

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
                bool selected = _id == _panel.MouseSelectedId;
                TopCircle.SetData(selected);
                BottomCircle.SetData(selected);
            }
            else
            {
                base.RefreshSelected();
            }
        }

        public override void RefreshSlotUI()
        {
            if (_data is ConditionOperNode)
            {
                var pos0 = _panel.GetLineEndPos(_data, 1) - selfR.anchoredPosition;
                TopCircleR.anchoredPosition = pos0;
                TopCircleTextR.anchoredPosition = GetTextPos(pos0);

                var pos1 = _panel.GetLineEndPos(_data, 2) - selfR.anchoredPosition;
                BottomCircleR.anchoredPosition = pos1;
                BottomCircleTextR.anchoredPosition = GetTextPos(pos1);
            }
            else
            {
                base.RefreshSlotUI();
            }
        }

        Vector2 GetTextPos(Vector2 source)
        {

            int x = 0;
            int y = 0;


            if (source.y == 0)
                y = 0;
            else if (source.y > 0)
                y = -6;
            else
                y = 6;

            if (source.x >= 0)
                x = -10;
            else
                x = 10;

            return new Vector2(x, y);
        }



    }
}