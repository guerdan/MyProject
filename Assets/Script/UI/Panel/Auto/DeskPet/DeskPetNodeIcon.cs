
using System;
using System.Collections.Generic;
using Script.Model.Auto;
using Script.UI.Panel.Auto.Node;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.DeskPet
{

    public class DeskPetNodeIcon : MonoBehaviour
    {
        [Header("TempMatch")]
        [SerializeField] private GameObject TemplateMatchGO;
        [SerializeField] private Image TemplateMatchOutlineRed;
        [SerializeField] private Image TemplateMatchOutlineWhite;
        [SerializeField] private GameObject BgIfNeed;

        [Header("Mouse")]
        [SerializeField] private GameObject MouseGO;
        [SerializeField] private Image MouseOutlineRed;
        [SerializeField] private Image MouseOutlineWhite;
        [SerializeField] private RectTransform MouseIconRT;         // 图标
        [SerializeField] private Sprite Sprite_MouseClick;          // 图标
        [SerializeField] private Sprite Sprite_MouseMove;           // 图标
        [SerializeField] private Sprite Sprite_StopScript;          // 图标

        [SerializeField] private Text MouseTitle;                   // 按键

        [Header("Assign、Con、Lis、Tri")]
        [SerializeField] private GameObject AssignGO;
        [SerializeField] private Image AssignOutlineRed;
        [SerializeField] private Image AssignOutlineWhite;
        [SerializeField] private Image AssignTypeBg;            // 图 
        [SerializeField] private Image AssignTypeLine;          // 图 
        [SerializeField] private Text AssignTypeText;           // 类型：条件/触发事件/监听事件 
        [SerializeField] private Image AssignTypeIcon;           // 类型：截图
        [Header("Map")]
        [SerializeField] private GameObject MapGO;
        [SerializeField] private Image MapOutlineRed;
        [SerializeField] private Image MapOutlineWhite;
        [SerializeField] private Text MapTitle;

        string _scriptId;
        string _nodeId;
        bool _show_time;
        BaseNodeData _node;
        Image _outlineRed;

        public void SetData(string scriptId, string nodeId, bool show_time)
        {
            _scriptId = scriptId;
            Utils.SetActive(this, scriptId != null);
            if (scriptId == null)
                return;

            if (scriptId == _scriptId && nodeId == _nodeId)
                return;

            _nodeId = nodeId;


            var manager = AutoScriptManager.Inst;
            var script = manager.GetScriptData(scriptId);
            var node = script.NodeDatas[nodeId];

            _node = node;
            _show_time = show_time;
            var nodeType = node.NodeType;

            bool is_TemplateMatch = nodeType == NodeType.TemplateMatchOper;
            bool use_MouseGo = nodeType == NodeType.MouseOper || nodeType == NodeType.StopScript
                || nodeType == NodeType.KeyBoardOper || nodeType == NodeType.WaitOper;

            bool is_AssignSeries = nodeType == NodeType.AssignOper || nodeType == NodeType.ConditionOper
                || nodeType == NodeType.ForOper || nodeType == NodeType.TriggerEvent || nodeType == NodeType.ListenEvent
                 || nodeType == NodeType.CaptureOper;
            bool is_MapCapture = nodeType == NodeType.MapCapture || nodeType == NodeType.MapPathFinding
                || nodeType == NodeType.ItemGridRecog;

            Utils.SetActive(TemplateMatchGO, is_TemplateMatch);
            Utils.SetActive(MouseGO, use_MouseGo);
            Utils.SetActive(AssignGO, is_AssignSeries);
            Utils.SetActive(MapGO, is_MapCapture);

            Image outlineRed = null;
            Image outlineWhite = null;

            if (is_TemplateMatch)
            {
                outlineRed = TemplateMatchOutlineRed;
                outlineWhite = TemplateMatchOutlineWhite;
                Utils.SetActive(BgIfNeed, !show_time);

            }
            else if (use_MouseGo)
            {
                outlineRed = MouseOutlineRed;
                outlineWhite = MouseOutlineWhite;
                if (nodeType == NodeType.MouseOper)
                {
                    Utils.SetActive(MouseIconRT, true);
                    Utils.SetActive(MouseTitle, false);
                    var mNode = node as MouseOperNode;
                    var type = mNode.ClickType;
                    MouseIconRT.GetComponent<Image>().sprite = type == 2 ? Sprite_MouseMove : Sprite_MouseClick;
                    MouseIconRT.localScale = new Vector3(mNode.ClickType == 0 ? 1 : -1, 1, 1);
                    MouseIconRT.anchoredPosition = new Vector2(mNode.ClickType == 0 ? -3 : 3, 5);
                    MouseIconRT.sizeDelta = new Vector2(27.5f, 40.25f);
                }
                else if (nodeType == NodeType.StopScript)
                {
                    Utils.SetActive(MouseIconRT, true);
                    Utils.SetActive(MouseTitle, false);
                    MouseIconRT.GetComponent<Image>().sprite = Sprite_StopScript;
                    MouseIconRT.localScale = Vector3.one;
                    MouseIconRT.anchoredPosition = new Vector2(0, 3);
                    MouseIconRT.sizeDelta = new Vector2(25.6f, 30.8f);
                }
                else if (nodeType == NodeType.KeyBoardOper)
                {
                    Utils.SetActive(MouseIconRT, false);
                    Utils.SetActive(MouseTitle, true);
                    KeyBoardOperNode data = node as KeyBoardOperNode;
                    // MouseTitle.text = data.Key;
                    MouseTitle.text = data.Key.Length > 0 ? data.Key.Substring(0, 1) : "";
                }
                else if (nodeType == NodeType.WaitOper)
                {
                    Utils.SetActive(MouseIconRT, false);
                    Utils.SetActive(MouseTitle, true);
                    // MouseTitle.text = "待";
                    MouseTitle.text = "Wa";
                }

            }

            else if (is_AssignSeries)
            {
                outlineRed = AssignOutlineRed;
                outlineWhite = AssignOutlineWhite;

                bool show_type = nodeType != NodeType.AssignOper;
                bool use_type_icon = nodeType == NodeType.CaptureOper;

                Utils.SetActive(AssignTypeBg, show_type);
                Utils.SetActive(AssignTypeLine, show_type);
                Utils.SetActive(AssignTypeText, show_type && !use_type_icon);
                Utils.SetActive(AssignTypeIcon, show_type && use_type_icon);


                if (nodeType == NodeType.ConditionOper)
                {
                    ConditionOperNode data = node as ConditionOperNode;
                    AssignTypeText.text = "C";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor1;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor1;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor1;
                }
                else if (nodeType == NodeType.CaptureOper)
                {
                    ForOperNode data = node as ForOperNode;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor4;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor4;
                }
                else if (nodeType == NodeType.ForOper)
                {
                    ForOperNode data = node as ForOperNode;
                    AssignTypeText.text = "F";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor1;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor1;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor1;
                }

                else if (nodeType == NodeType.TriggerEvent)
                {
                    TriggerEventNode data = node as TriggerEventNode;
                    // AssignTypeText.text = "触发";
                    AssignTypeText.text = "T";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor2;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor2;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor2;
                }

                else if (nodeType == NodeType.ListenEvent)
                {
                    ListenEventNode data = node as ListenEventNode;
                    // AssignTypeText.text = "监听";
                    AssignTypeText.text = "L";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor3;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor3;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor3;
                }

            }
            else if (is_MapCapture)
            {
                outlineRed = MapOutlineRed;
                outlineWhite = MapOutlineWhite;
                if (nodeType == NodeType.MapCapture)
                    // MapTitle.text = "地图识别";
                    MapTitle.text = "M-R";
                else if (nodeType == NodeType.MapPathFinding)
                    // MapTitle.text = "地图寻路";
                    MapTitle.text = "M-F";
                else if (nodeType == NodeType.ItemGridRecog)
                    // MapTitle.text = "物品格识别";
                    MapTitle.text = "GI-R";
            }

            //common logic
            Utils.SetActive(outlineRed, show_time);
            Utils.SetActive(outlineWhite, show_time);
            if (show_time)
            {
                outlineRed.color = Utils.ParseHtmlString("#E72E1D");
                outlineRed.fillAmount = node.Timer / node.Delay;
            }
            _outlineRed = outlineRed;
        }

        void Update()
        {


            if (_node == null)
                return;
            if (_show_time)
            {
                _outlineRed.fillAmount = 0;
                // _outlineRed.fillAmount = _node.Timer / _node.Delay;
            }
        }

    }
}