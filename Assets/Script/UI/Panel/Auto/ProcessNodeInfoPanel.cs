
using System;
using System.Collections.Generic;
using OpenCvSharp.Dnn;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// todo
    /// TemplateMatch   图片详情窗，此窗点外部就关闭。复制图片，并且加载赋值。
    /// </summary>
    public class ProcessNodeInfoPanel : BasePanel
    {
        [Header("通用")]
        [SerializeField] private InputTextComp NameInput;
        [SerializeField] private Text IdText;
        [SerializeField] private SelectBoxComp TypeBox;
        [SerializeField] private KeywordTipsComp TipsComp;  //提示词组件
        [SerializeField] private InputTextComp DelayInput;
        [SerializeField] private InputTextComp DescriptionInput;
        [SerializeField] private CheckBox IsFirstCheck;


        [SerializeField] private Button UnfoldBtn;
        [SerializeField] private Button FoldBtn;
        [SerializeField] private GameObject RightPanel;



        [Header("模版匹配")]
        [SerializeField] private GameObject TemplateMatchGO;
        [SerializeField] private ImageLoadComp TemplateImage;
        [SerializeField] private Button TemplateImageBtn;
        [SerializeField] private InputTextComp ThresholdInput;
        [SerializeField] private InputTextComp CountInput;
        [SerializeField] private InputTextProComp RegionInput;
        [SerializeField] private CheckBox RegionCheck;

        [Header("鼠标操作")]
        [SerializeField] private GameObject MouseOperGO;
        [SerializeField] private SelectBoxComp MouseOperTypeBox;
        [SerializeField] private InputTextProComp MouseOperPosInput;
        [SerializeField] private CheckBox MouseOperPosCheck;

        [Header("键盘操作")]
        [SerializeField] private GameObject KeyboardOperGO;
        [SerializeField] private InputTextComp KeyboardInput;
        [SerializeField] private CheckBox KeyboardCheck;


        [Header("赋值操作")]
        [SerializeField] private GameObject AssignOperGO;
        [SerializeField] private InputTextProComp AssignInput;
        [SerializeField] private SelectBoxComp VarTypeBox;      // 变量类型
        [SerializeField] private CheckBox AssignCheck;
        [SerializeField] private Text AssignCheckNum;           // 已声明此变量的个数

        [Header("条件判断操作")]
        [SerializeField] private GameObject ConditionOperGO;
        [SerializeField] private InputTextProComp ConditionInput;
        [SerializeField] private CheckBox ConditionCheck;

        [Header("抛出/监听事件操作")]
        [SerializeField] private GameObject EventOperGO;
        [SerializeField] private InputTextComp EventNameInput;
        [SerializeField] private Text EventNum;

        AutoScriptManager manager => AutoScriptManager.Inst;
        BaseNodeData _data;
        NodeType _nodeType;

        DrawProcessPanel _drawPanel;
        AutoScriptData _scriptData;

        RectTransform _target;

        void Awake()
        {
            UnfoldBtn.onClick.AddListener(OnFoldChangeBtnClick);
            FoldBtn.onClick.AddListener(OnFoldChangeBtnClick);
            TemplateImageBtn.onClick.AddListener(OnTemplateImageBtnClick);
        }


        public override void SetData(object list)
        {
            _useScaleAnim = false;
            var dataList = list as List<object>;
            _data = dataList[0] as BaseNodeData;
            _nodeType = _data.NodeType;

            _target = dataList[1] as RectTransform;
            _drawPanel = dataList[2] as DrawProcessPanel;
            _scriptData = _drawPanel._scriptData;
            TipsComp.gameObject.SetActive(false);

            if (display)
                SetPos();

            Refresh();
        }

        public override void BeforeShow()
        {
            SetPos();
            base.BeforeShow();
        }
        // 目的是，显示在_target矩形的周围。优先级下方、右方、左方（界面的下方不合适）
        void SetPos()
        {
            var selfR = (RectTransform)transform;
            var target_size = _target.rect.size;
            var pos = Utils.GetPos(selfR, _target, default);
            bool is_bottom = pos.y - target_size.y / 2 - selfR.rect.height > -Screen.height / 2;
            bool is_right = pos.x + target_size.x + selfR.rect.width < Screen.width / 2;
            if (is_bottom)
            {
                pos.y = pos.y - target_size.y / 2 - selfR.rect.height / 2;
            }
            else if (is_right)
            {
                pos.x = pos.x + target_size.x / 2 + selfR.rect.width / 2;
            }
            else
            {
                pos.x = pos.x - target_size.x / 2 - selfR.rect.width / 2;
            }


            PanelDefine.InitPos = pos;
            RefreshPos();
        }

        void Refresh()
        {
            NameInput.SetData(_data.Name, str => { _data.Name = str; RefreshDrawPanel(); });
            DelayInput.SetData(_data.Delay + "s", OnDelayEndEdit);
            DescriptionInput.SetData(_data.Description, str => _data.Description = str);

            IdText.text = AutoDataUIConfig.GetNodeId(_data);
            List<string> list = AutoDataUIConfig.GetNodeTypeNameList();
            TypeBox.SetData(list, OnSelect, TipsComp);
            TypeBox.SetCurIndex(AutoDataUIConfig.NodeTypes.IndexOf(_nodeType));
            IsFirstCheck.SetData(_scriptData.Config.FirstNode == _data.Id, OnIsFirstCheck);
            RefreshFoldStatus();

            TemplateMatchGO.SetActive(_nodeType == NodeType.TemplateMatchOper);
            MouseOperGO.SetActive(_nodeType == NodeType.MouseOper);
            KeyboardOperGO.SetActive(_nodeType == NodeType.KeyBoardOper);
            AssignOperGO.SetActive(_nodeType == NodeType.AssignOper);
            ConditionOperGO.SetActive(_nodeType == NodeType.ConditionOper);
            EventOperGO.SetActive(_nodeType == NodeType.TriggerEvent || _nodeType == NodeType.ListenEvent);


            if (_nodeType == NodeType.TemplateMatchOper)
            {
                RefreshTemplateMatchPanel();
            }

            else if (_nodeType == NodeType.MouseOper)
            {

                RefreshMouseOperPanel();
            }

            else if (_nodeType == NodeType.KeyBoardOper)
            {
                var data = _data as KeyBoardOperNode;
                Action<string> save_func = (str) =>
                {
                    data.Key = str;
                    RefreshDrawPanel();
                };

                KeyboardInput.SetData(data.Key, save_func);
                KeyboardInput.UseKeywordTips(TipsComp, AutoDataUIConfig.GetKeyboardMatchList);
                KeyboardInput.UseCheckBox(KeyboardCheck, AutoDataUIConfig.IsLegalKeyboardName);
            }

            else if (_nodeType == NodeType.AssignOper)
            {
                RefreshAssignOperPanel();
            }

            else if (_nodeType == NodeType.ConditionOper)
            {
                RefreshConditionOperPanel();
            }

            else if (_nodeType == NodeType.TriggerEvent || _nodeType == NodeType.ListenEvent)
            {
                RefreshEventOperPanel();
            }
        }

        void OnSelect(int index)
        {
            var nodeType = AutoDataUIConfig.NodeTypes[index];
            if (nodeType == _nodeType)
            {
                return;
            }
            _nodeType = nodeType;
            var oldData = _data;
            //更新数据
            manager.DeleteNode(_scriptData, _data.Id);
            _data = manager.CreateNode(_scriptData, nodeType, _data.Pos, _data.Id);

            //给它复制下基本信息
            _data.Name = oldData.Name;
            _data.Description = oldData.Description;

            Refresh();
            RefreshDrawPanel();
        }

        void RefreshDrawPanel()
        {
            _drawPanel.RefreshNode(_data.Id);
        }


        void OnDelayEndEdit(string str)
        {
            if (str.EndsWith("s"))
            {
                str = str.Substring(0, str.Length - 1);
            }
            bool isValid = float.TryParse(str, out float value);
            float minDelay = manager.GetNodeMinDalay(_nodeType);
            if (isValid && value >= minDelay)
            {
                _data.Delay = value;
                DelayInput.SetText(value + "s");
            }
            else
            {
                DelayInput.SetText(minDelay + "s");
            }

            RefreshDrawPanel();
        }

        void OnIsFirstCheck(bool isFirst)
        {
            _scriptData.Config.FirstNode = isFirst ? _data.Id : "";
            RefreshDrawPanel();
        }

        void OnFoldChangeBtnClick()
        {
            manager.InfoPanelFolded = !manager.InfoPanelFolded;
            RefreshFoldStatus();
        }
        void RefreshFoldStatus()
        {
            Utils.SetActive(RightPanel, !manager.InfoPanelFolded);
            Utils.SetActive(UnfoldBtn.gameObject, manager.InfoPanelFolded);
        }


        #region TemplateMatch
        void RefreshTemplateMatchPanel()
        {
            var data = _data as TemplateMatchOperNode;

            ThresholdInput.SetData(data.Threshold.ToString(), str =>
            {
                // 不合法就纠正
                if (float.TryParse(str, out float value) && value >= 0 && value <= 1)
                    data.Threshold = value;
                else
                    data.Threshold = TemplateMatchOperNode.ThresholdDefault;

                ThresholdInput.SetText(data.Threshold.ToString());
            });

            CountInput.SetData(data.Count.ToString(), str =>
            {
                // 不合法就纠正
                if (int.TryParse(str, out int value) && value >= 1)
                    data.Count = value;
                else
                    data.Count = TemplateMatchOperNode.CountDefault;

                CountInput.SetText(data.Count.ToString());
            });


            SetRegionInput();
            SetTemplateImage();

        }

        void SetRegionInput()
        {
            var data = _data as TemplateMatchOperNode;

            Action<string> save_func = (str) =>
           {
               data.RegionExpression = str;
               string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
               RegionInput.SetText(format);

               RefreshDrawPanel();
           };

            string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
            RegionInput.SetData(format, save_func);

            var varRef = _scriptData.GetInEditVarRef();
            RegionInput.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetAssignMatchList(search, varRef); }
            , AutoDataUIConfig.GetAssignKeyword);

            RegionInput.UseCheckBox(RegionCheck, (str) =>
                {
                    str = str.Replace(" ", "");
                    if (string.IsNullOrEmpty(str)) return false;
                    if (str.IndexOf("=") >= 0) return false;

                    return AutoDataUIConfig.ExpressionIsLegal(str);
                });
        }

        void SetTemplateImage()
        {
            var data = _data as TemplateMatchOperNode;
            TemplateImage.SetData(ImageManager.GetFullPath(data.TemplatePath), new Vector2(140, 120), false);
        }

        // 1.可选择已管理的图片  2.可选择本地图片，复制到streaming里进行管理
        void OnTemplateImageBtnClick()
        {
            var data = _data as TemplateMatchOperNode;

            Action<string> save_func = (str) =>
            {
                data.TemplatePath = str;
                SetTemplateImage();
                RefreshDrawPanel();
            };

            var param = new List<object>();
            param.Add(data.TemplatePath);
            param.Add(save_func);

            UIManager.Inst.ShowPanel(PanelEnum.ImageSourcePanel, param);
        }




        #endregion

        #region AssignOper
        void RefreshAssignOperPanel()
        {

            var data = _data as AssignOperNode;

            Action<string> save_func = (str) =>
            {
                data.Formula = str;
                string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
                AssignInput.SetText(format);

                RefreshAssignCheckBox();
                RefreshDrawPanel();
            };
            Action<string> update_func = (str) =>
            {
                RefreshAssignCheckBox();

                var count = 0;
                bool has = TryGetVarInfo(str, out var info);
                if (has)
                {
                    count = info.Nodes.Count;
                    if (info.Nodes.Contains(data))
                    {
                        count--;
                    }
                }

                if (count > 0)
                {
                    VarTypeBox.SetCurIndex(AutoDataUIConfig.VarTypes.IndexOf(info.Type));
                    VarTypeBox.SetLock(true);
                }
                else
                {
                    VarTypeBox.SetLock(false);
                }
            };

            string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
            AssignInput.SetData(format, save_func, update_func);

            var varRef = _scriptData.GetInEditVarRef();
            AssignInput.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetAssignMatchList(search, varRef); }
            , AutoDataUIConfig.GetAssignKeyword);

            VarTypeBox.SetData(AutoDataUIConfig.VarTypeNames, (index) =>
            {
                data.VarType = AutoDataUIConfig.VarTypes[index];

                RefreshAssignCheckBox();
                RefreshDrawPanel();
            }, TipsComp);

            VarTypeBox.SetCurIndex(AutoDataUIConfig.VarTypes.IndexOf(data.VarType));
            // 初始刷一下
            //
            update_func(format);
        }

        void RefreshAssignCheckBox()
        {
            var data = _data as AssignOperNode;
            var text = AssignInput.GetText();
            text = text.Replace(" ", "");
            bool isLegal = false;

            var equal_index = text.IndexOf("=");
            if (equal_index > 0)
            {
                var varNameLower = text.Substring(0, equal_index).ToLower();
                var expression = text.Substring(equal_index + 1);
                isLegal = _scriptData.CheckFormula(varNameLower, expression, data.VarType);
            }

            AssignCheck.SetData(isLegal);
            bool has = TryGetVarInfo(text, out var info);
            if (!has)
            {
                AssignCheckNum.text = "0";
            }
            else
            {
                var count = info.Nodes.Count;
                if (info.Nodes.Contains(data))
                {
                    count--;
                }
                AssignCheckNum.text = count.ToString();
            }
        }
        // 文本到info
        public bool TryGetVarInfo(string text, out FormulaVarInfo info)
        {
            var i = text.IndexOf("=");
            if (i < 0)
            {
                info = new FormulaVarInfo();
                return false;
            }
            var varName = text.Substring(0, i).Trim().ToLower();
            bool has = _scriptData.GetVarInfo(varName, out info);
            return has;
        }
        #endregion

        #region ConditionOper
        void RefreshConditionOperPanel()
        {
            var data = _data as ConditionOperNode;

            Action<string> save_func = (str) =>
            {
                data.Formula = str;
                string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
                ConditionInput.SetText(format);

                RefreshDrawPanel();
            };


            string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
            ConditionInput.SetData(format, save_func);

            var varRef = _scriptData.GetInEditVarRef();
            ConditionInput.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetAssignMatchList(search, varRef); }
            , AutoDataUIConfig.GetAssignKeyword);

            ConditionInput.UseCheckBox(ConditionCheck, RefreshConditionCheckBox);
        }

        bool RefreshConditionCheckBox(string str)
        {
            str = str.Replace(" ", "");
            bool isLegal = AutoDataUIConfig.ConditionIsLegal(str);
            return isLegal;
        }

        #endregion

        #region MouseOper
        void RefreshMouseOperPanel()
        {
            var data = _data as MouseOperNode;
            MouseOperTypeBox.SetData(AutoDataUIConfig.MouseClickTypes,
            (index) =>
            {
                data.clickType = index;
                RefreshDrawPanel();
            }, TipsComp);
            MouseOperTypeBox.SetCurIndex(data.clickType);


            Action<string> save_func = (str) =>
            {
                data.ClickPos = str;
                string format = AutoDataUIConfig.FormulaFormat(data.ClickPos);  //格式化
                MouseOperPosInput.SetText(format);

                RefreshDrawPanel();
            };

            string format = AutoDataUIConfig.FormulaFormat(data.ClickPos);  //格式化
            MouseOperPosInput.SetData(format, save_func);

            var varRef = _scriptData.GetInEditVarRef();
            MouseOperPosInput.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetAssignMatchList(search, varRef); }
            , AutoDataUIConfig.GetAssignKeyword);

            MouseOperPosInput.UseCheckBox(MouseOperPosCheck, (str) =>
                {
                    str = str.Replace(" ", "");
                    if (string.IsNullOrEmpty(str)) return false;
                    if (str.IndexOf("=") >= 0) return false;

                    return AutoDataUIConfig.ExpressionIsLegal(str);
                });
        }

        #endregion
        #region  Trigger/Listen
        void RefreshEventOperPanel()
        {
            bool isTrigger = _nodeType == NodeType.TriggerEvent;
            if (isTrigger)
            {

                var data = _data as TriggerEventNode;
                EventNameInput.SetData(data.EventName, str =>
                {
                    data.EventName = str;
                    EventNameInput.SetText(data.EventName); // 可能会格式化
                    RefreshDrawPanel();
                }, str =>
                {
                    UpdateEventOperPanel();
                });

                UpdateEventOperPanel();
            }
            else
            {
                var data = _data as ListenEventNode;
                EventNameInput.SetData(data.EventName, str =>
                {
                    data.EventName = str;
                    EventNameInput.SetText(data.EventName); // 可能会格式化
                    RefreshDrawPanel();
                }, str =>
                {
                    UpdateEventOperPanel();
                });

                UpdateEventOperPanel();
            }

        }
        void UpdateEventOperPanel()
        {
            var text = EventNameInput.GetText();
            bool isTrigger = _nodeType == NodeType.TriggerEvent;
            if (isTrigger)
            {
                var list = _scriptData.GetListenNodes(text);
                EventNum.text = list.Count.ToString();
            }
            else
            {
                var list = _scriptData.GetEditTriggerNodes(text);
                EventNum.text = list.Count.ToString();
            }
        }

        #endregion
    }
}