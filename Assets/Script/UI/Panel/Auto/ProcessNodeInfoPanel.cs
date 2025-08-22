
using System;
using System.Collections.Generic;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Script.UI.Panel.Auto
{
    public class ProcessNodeInfoPanel : BasePanel
    {
        [Header("通用")]
        [SerializeField] private InputField NameInput;
        [SerializeField] private Text IdText;
        [SerializeField] private SelectBoxComp TypeBox;
        [SerializeField] private KeywordTipsComp TipsComp;  //提示词组件
        [SerializeField] private InputField DelayInput;
        [SerializeField] private InputField DescriptionInput;


        [Header("模版匹配")]
        [SerializeField] private GameObject TemplateMatchGO;
        [SerializeField] private Image TemplateImage;
        [SerializeField] private InputField ThresholdInput;
        [SerializeField] private InputField CountInput;
        [SerializeField] private InputField RegionInput;

        [Header("鼠标操作")]
        [SerializeField] private GameObject MouseOperGO;
        [SerializeField] private SelectBoxComp MouseOperTypeBox;


        [Header("键盘操作")]
        [SerializeField] private GameObject KeyboardOperGO;
        [SerializeField] private InputField KeyboardInput;
        [SerializeField] private InputTextComp KeyboardInputText;
        [SerializeField] private CheckBox KeyboardCheck;


        [Header("赋值操作")]
        [SerializeField] private GameObject AssignOperGO;
        [SerializeField] private InputField AssignInput;

        AutoScriptManager manager => AutoScriptManager.Inst;
        BaseNodeData _data;
        NodeType _nodeType;

        DrawProcessPanel _drawPanel;

        RectTransform _target;
        Vector2 _offset;
        void Awake()
        {
            TipsComp.gameObject.SetActive(false);
            NameInput.onEndEdit.AddListener(OnNameEndEdit);
            DelayInput.onEndEdit.AddListener(OnDelayEndEdit);
            DescriptionInput.onEndEdit.AddListener(OnDescriptionEndEdit);
            // KeyboardInput.onValueChanged.AddListener(OnKeyboardValueChanged);
        }


        public override void SetData(object list)
        {
            _useScaleAnim = false;
            var dataList = list as List<object>;
            _data = dataList[0] as BaseNodeData;
            _nodeType = _data.GetNodeType();

            _target = dataList[1] as RectTransform;
            var t1 = dataList[2] as Tuple<float, float>;
            _offset = new Vector2(t1.Item1, t1.Item2);

            _drawPanel = dataList[3] as DrawProcessPanel;

            Refresh();
        }

        public override void BeforeShow()
        {
            var rT = (RectTransform)transform;
            var pos = Utils.GetPos((RectTransform)transform, _target, _offset);
            pos.y = pos.y - rT.rect.height / 2 - 30;
            PanelDefine.InitPos = pos;

            base.BeforeShow();
        }

        void Refresh()
        {
            NameInput.text = _data.Name;
            DelayInput.text = _data.Delay + "s";
            DescriptionInput.text = _data.Description;
            IdText.text = AutoDataShowConfig.GetNodeId(_data);
            List<string> list = AutoDataShowConfig.GetNodeTypeNameList();
            TypeBox.SetData(list, OnSelect, TipsComp);


            TemplateMatchGO.SetActive(false);
            MouseOperGO.SetActive(false);
            KeyboardOperGO.SetActive(false);
            AssignOperGO.SetActive(false);

            if (_nodeType == NodeType.TemplateMatchOper)
            {
                TemplateMatchGO.SetActive(true);

            }
            else if (_nodeType == NodeType.MouseOper)
            {
                MouseOperGO.SetActive(true);
            }
            else if (_nodeType == NodeType.KeyBoardOper)
            {
                var data = _data as KeyBoardOperNode;
                KeyboardOperGO.SetActive(true);
                // KeyboardInput.text = data.Key;

                Action<string> save_func = (str) => { data.Key = str; };

                KeyboardInputText.SetData(data.Key, save_func, OnKeyboardValueChanged);
                KeyboardInputText.OpenKeywordTips(TipsComp, AutoDataShowConfig.GetKeyboardMatchList);
            }
            else if (_nodeType == NodeType.AssignOper)
            {
                AssignOperGO.SetActive(true);
            }
        }


        void OnSelect(int index)
        {
            var nodeType = AutoDataShowConfig.NodeTypes[index];
            if (nodeType == _nodeType)
            {
                return;
            }
            _nodeType = nodeType;
            var oldData = _data;
            //更新数据
            manager.DeleteNode(_data.Id);
            _data = manager.CreateNode(nodeType, _data.Pos, _data.Id);

            //给它复制下基本信息
            _data.Name = oldData.Name;
            _data.Delay = oldData.Delay;
            _data.Description = oldData.Description;

            Refresh();
            _drawPanel.RefreshNode(_data.Id);
        }


        void OnNameEndEdit(string str)
        {
            _data.Name = str;
        }
        void OnDelayEndEdit(string str)
        {
            if (str.EndsWith("s"))
            {
                str = str.Substring(0, str.Length - 1);
            }
            bool isValid = float.TryParse(str, out float value);
            if (isValid)
            {
                _data.Delay = value;
                DelayInput.text = value + "s";
            }
            else
            {
                DelayInput.text = "";
            }
        }

        void OnDescriptionEndEdit(string str)
        {
            _data.Description = str;
        }


        void OnKeyboardValueChanged(string str)
        {
            // var data = _data as KeyBoardOperNode;
            // var match_list = AutoDataShowConfig.GetKeyboardMatchList(str);
            // var rectT = KeyboardInput.GetComponent<RectTransform>();

            // CueWordComp.gameObject.SetActive(match_list.Count > 0);
            // if (match_list.Count > 0)
            // {
            //     CueWordComp.SetData(match_list,
            //     index =>
            //     {
            //         var result = match_list[index];
            //         data.Key = result;
            //         KeyboardInput.text = result;

            //     }, 150);
            //     CueWordComp.SetPos(rectT, new Vector2(0, -rectT.rect.height / 2));
            //     CueWordComp.SetCurIndex(0);
            // }

            bool legal = AutoDataShowConfig.KeyboardName2Enum.TryGetValue(str, out KeyboardEnum key);
            KeyboardCheck.SetData(legal);
        }

    }
}