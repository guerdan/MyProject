
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
        [Header("模版匹配")]
        [SerializeField] private GameObject TemplateMatchGO;
        [SerializeField] private Image TemplateMatchOutlineRed;
        [SerializeField] private Image TemplateMatchOutlineWhite;
        [SerializeField] private GameObject BgIfNeed;

        [Header("鼠标")]
        [SerializeField] private GameObject MouseGO;
        [SerializeField] private Image MouseOutlineRed;
        [SerializeField] private Image MouseOutlineWhite;
        [SerializeField] private RectTransform MouseIconRT;         // 图标
        [SerializeField] private Sprite Sprite_MouseClick;          // 图标
        [SerializeField] private Sprite Sprite_MouseMove;           // 图标
        [SerializeField] private Sprite Sprite_StopScript;          // 图标

        [Header("键盘")]
        [SerializeField] private GameObject KeyBoardGO;
        [SerializeField] private Image KeyBoardOutlineRed;
        [SerializeField] private Image KeyBoardOutlineWhite;
        [SerializeField] private Text KeyBoardKey;              // 按键

        [Header("赋值、条件、监听、触发")]
        [SerializeField] private GameObject AssignGO;
        [SerializeField] private Image AssignOutlineRed;
        [SerializeField] private Image AssignOutlineWhite;
        [SerializeField] private Text AssignTypeText;           // 类型：条件/触发事件/监听事件 
        [SerializeField] private Image AssignTypeBg;            // 图 
        [SerializeField] private Image AssignTypeLine;          // 图 
        [Header("小地图拍摄")]
        [SerializeField] private GameObject MapCaptureGO;
        [SerializeField] private Image MapCaptureOutlineRed;
        [SerializeField] private Image MapCaptureOutlineWhite;

        string _scriptId;
        string _nodeId;
        bool _show_time;
        BaseNodeData _node;
        Image _outlineRed;

        public void SetData(string scriptId, string nodeId, bool show_time)
        {
            Utils.SetActive(this, scriptId != null);
            if (scriptId == null)
                return;

            if (scriptId == _scriptId && nodeId == _nodeId)
                return;

            _scriptId = scriptId;
            _nodeId = nodeId;


            var manager = AutoScriptManager.Inst;
            var script = manager.GetScriptData(scriptId);
            var node = script.NodeDatas[nodeId];

            _node = node;
            _show_time = show_time;
            var nodeType = node.NodeType;

            bool is_TemplateMatch = nodeType == NodeType.TemplateMatchOper;
            bool is_Mouse = nodeType == NodeType.MouseOper || nodeType == NodeType.StopScript;
            bool is_KeyBoard = nodeType == NodeType.KeyBoardOper;
            bool is_AssignSeries = nodeType == NodeType.AssignOper || nodeType == NodeType.ConditionOper
                || nodeType == NodeType.TriggerEvent || nodeType == NodeType.ListenEvent;
            bool is_MapCapture = nodeType == NodeType.MapCapture;

            Utils.SetActive(TemplateMatchGO, is_TemplateMatch);
            Utils.SetActive(MouseGO, is_Mouse);
            Utils.SetActive(KeyBoardGO, is_KeyBoard);
            Utils.SetActive(AssignGO, is_AssignSeries);
            Utils.SetActive(MapCaptureGO, is_MapCapture);

            Image outlineRed = null;
            Image outlineWhite = null;

            if (is_TemplateMatch)
            {
                outlineRed = TemplateMatchOutlineRed;
                outlineWhite = TemplateMatchOutlineWhite;
                Utils.SetActive(BgIfNeed, !show_time);

            }
            else if (is_Mouse)
            {
                outlineRed = MouseOutlineRed;
                outlineWhite = MouseOutlineWhite;
                if (node is MouseOperNode mouseNode)
                {
                    var type = mouseNode.ClickType;
                    MouseIconRT.GetComponent<Image>().sprite = type == 2 ? Sprite_MouseMove : Sprite_MouseClick;
                    MouseIconRT.localScale = new Vector3(mouseNode.ClickType == 0 ? 1 : -1, 1, 1);
                    MouseIconRT.anchoredPosition = new Vector2(mouseNode.ClickType == 0 ? -3 : 3, 5);
                    MouseIconRT.sizeDelta = new Vector2(27.5f, 40.25f);
                }
                else if (node is StopScriptNode stopNode)
                {
                    MouseIconRT.GetComponent<Image>().sprite = Sprite_StopScript;
                    MouseIconRT.localScale = Vector3.one;
                    MouseIconRT.anchoredPosition = new Vector2(0, 3);
                    MouseIconRT.sizeDelta = new Vector2(25.6f, 30.8f);
                }



            }
            else if (is_KeyBoard)
            {
                outlineRed = KeyBoardOutlineRed;
                outlineWhite = KeyBoardOutlineWhite;
                KeyBoardOperNode data = node as KeyBoardOperNode;
                KeyBoardKey.text = data.Key;
            }
            else if (is_AssignSeries)
            {
                outlineRed = AssignOutlineRed;
                outlineWhite = AssignOutlineWhite;

                bool is_Assign = nodeType == NodeType.AssignOper;
                Utils.SetActive(AssignTypeText, !is_Assign);
                Utils.SetActive(AssignTypeBg, !is_Assign);
                Utils.SetActive(AssignTypeLine, !is_Assign);
                if (nodeType == NodeType.ConditionOper)
                {
                    ConditionOperNode data = node as ConditionOperNode;
                    AssignTypeText.text = "条件";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor1;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor1;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor1;
                }

                else if (nodeType == NodeType.TriggerEvent)
                {
                    TriggerEventNode data = node as TriggerEventNode;
                    AssignTypeText.text = "触发";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor2;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor2;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor2;
                }

                else if (nodeType == NodeType.ListenEvent)
                {
                    ListenEventNode data = node as ListenEventNode;
                    AssignTypeText.text = "监听";
                    AssignTypeText.color = AssignNodeUI.TypeTextColor3;
                    AssignTypeBg.color = AssignNodeUI.TypeBgColor3;
                    AssignTypeLine.color = AssignNodeUI.TypeLineColor3;
                }

            }
            else if (is_MapCapture)
            {
                outlineRed = MapCaptureOutlineRed;
                outlineWhite = MapCaptureOutlineWhite;
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
                _outlineRed.fillAmount = _node.Timer / _node.Delay;
            }
        }

    }
}