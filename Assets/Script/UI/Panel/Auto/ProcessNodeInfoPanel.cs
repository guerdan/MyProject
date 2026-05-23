
using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.Dnn;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
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
        [Header("Common")]
        [SerializeField] private InputTextComp NameInput;
        [SerializeField] private Text IdText;
        [SerializeField] private SelectBoxComp TypeBox;
        [SerializeField] private KeywordTipsComp TipsComp;      // 提示词组件
        [SerializeField] private InputTextComp DelayInput;
        [SerializeField] private InputTextComp DescriptionInput;
        [SerializeField] private CheckBox IsFirstCheck;


        [SerializeField] private Button UnfoldBtn;
        [SerializeField] private Button FoldBtn;
        [SerializeField] private GameObject RightPanel;
        [SerializeField] private Button GreenBtn;               // 工具按钮
        [SerializeField] private Button YellowBtn;              // 查看按钮


        [Header("TempMatch")]
        [SerializeField] private GameObject TemplateMatchGO;
        [SerializeField] private ImageLoadComp TemplateImage;
        [SerializeField] private Button TemplateImageBtn;
        [SerializeField] private InputTextComp ThresholdInput;
        [SerializeField] private InputTextComp CountInput;
        [SerializeField] private InputTextProComp RegionInput;
        [SerializeField] private CheckBox SaveCaptureCheck;

        [Header("Mouse")]
        [SerializeField] private GameObject MouseOperGO;
        [SerializeField] private SelectBoxComp MouseOperTypeBox;
        [SerializeField] private InputTextProComp MouseOperPosInput;
        [SerializeField] private InputTextComp MouseHoldTimeInput;

        [Header("Keyboard")]
        [SerializeField] private GameObject KeyboardOperGO;
        [SerializeField] private SelectBoxComp KeyboardTypeBox;
        [SerializeField] private InputTextComp KeyboardInput;
        [SerializeField] private CheckBox KeyboardCheck;
        [SerializeField] private InputTextComp KeyboardHoldTimeInput;
        [SerializeField] private InputTextComp KeyboardPipeInput;

        [Header("Assign")]
        [SerializeField] private GameObject AssignOperGO;
        [SerializeField] private InputTextProComp AssignInput;
        [SerializeField] private SelectBoxComp VarTypeBox;      // 变量类型
        [SerializeField] private Text AssignCheckNum;           // 统计赋值此变量的节点个数,包含自己

        [Header("Condition")]
        [SerializeField] private GameObject ConditionOperGO;
        [SerializeField] private InputTextProComp ConditionInput;

        [Header("Trigger/Listen Event")]
        [SerializeField] private GameObject EventOperGO;
        [SerializeField] private InputTextComp EventNameInput;
        [SerializeField] private Text EventNum;                 // 检索被对方的引用数量，例如监听查看有几个触发

        [Header("MapRecog")]
        [SerializeField] private GameObject MapCaptureGO;
        [SerializeField] private InputTextProComp MapCaptureRegionInput;
        [SerializeField] private InputTextComp MapCaptureIdInput;
        [SerializeField] private InputTextComp MapColorSetInput;

        [Header("CommonParamPanel")]
        [SerializeField] private GameObject CommonParamPanel;              //寻路模块,
        [SerializeField] private SelectBoxComp CommonParamTypeBox;
        [SerializeField] private TextScrollView InParamListComp;
        [SerializeField] private TextScrollView OutParamListComp;

        [SerializeField] private Button InParamBtn;
        [SerializeField] private Button OutParamBtn;
        [SerializeField] private InputTextSV InputTextSV;



        AutoScriptManager manager => AutoScriptManager.Inst;
        BaseNodeData _data;
        NodeType _nodeType;

        DrawProcessPanel _drawPanel;
        AutoScriptData _scriptData;


        // 以下是CommonParamPanel 可以扩展的
        InputTextSVItemData[] _inParam_edit_list;
        InputTextSVItemData[] _outParam_edit_list;
        Action<List<InputTextSVItemData>> _inParam_onEditEnd;
        Action<List<InputTextSVItemData>> _outParam_onEditEnd;

        // 以下是GreenBtn(工具)、YellowBtn(数据) 可以扩展的
        List<string> _greenBtn_options;
        Action<int> _greenBtn_onClick;
        List<string> _yellowBtn_options;

        void Awake()
        {
            UnfoldBtn.onClick.AddListener(OnFoldChangeBtnClick);
            FoldBtn.onClick.AddListener(OnFoldChangeBtnClick);
            TemplateImageBtn.onClick.AddListener(OnTemplateImageBtnClick);
            GreenBtn.onClick.AddListener(OnClickGreenBtn);
            YellowBtn.onClick.AddListener(OnClickYellowBtn);
            InParamBtn.onClick.AddListener(OnClickInParamBtn);
            OutParamBtn.onClick.AddListener(OnClickOutParamBtn);

            TipsComp.gameObject.SetActive(false);
            InputTextSV.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            NameInput.SetText("");
            DescriptionInput.SetText("");
        }


        public override void SetData(object list)
        {
            _useScaleAnim = false;
            var dataList = list as List<object>;
            _data = dataList[0] as BaseNodeData;
            _nodeType = _data.NodeType;
            _drawPanel = dataList[1] as DrawProcessPanel;
            _scriptData = _drawPanel._scriptData;


            Refresh();
        }


        void Refresh()
        {
            NameInput.SetData(_data.GetName(), str => { _data.SetName(str); RefreshDrawPanel(); });
            DelayInput.SetData(_data.Delay + "s", OnDelayEndEdit);
            DescriptionInput.SetData(_data.GetDescription(), str => _data.SetDescription(str));

            IdText.text = $"id: {_data.Id}";
            List<string> list = AutoDataUIConfig.GetNodeTypeNameList();
            TypeBox.SetData(list, OnSelectType, TipsComp);
            TypeBox.SetCurIndex(AutoDataUIConfig.NodeTypes.FindIndex((type) => type.Item1 == _nodeType));
            IsFirstCheck.SetData(_scriptData.Config.FirstNode == _data.Id, OnIsFirstCheck);
            RefreshFoldStatus();


            TemplateMatchGO.SetActive(_nodeType == NodeType.TemplateMatchOper);
            SaveCaptureCheck.transform.parent.gameObject.SetActive(_nodeType == NodeType.CaptureOper);
            MouseOperGO.SetActive(_nodeType == NodeType.MouseOper);
            KeyboardOperGO.SetActive(_nodeType == NodeType.KeyBoardOper);
            AssignOperGO.SetActive(_nodeType == NodeType.AssignOper);
            ConditionOperGO.SetActive(_nodeType == NodeType.ConditionOper || _nodeType == NodeType.ForOper
                                    || _nodeType == NodeType.CaptureOper);
            EventOperGO.SetActive(_nodeType == NodeType.TriggerEvent || _nodeType == NodeType.ListenEvent);
            // MapCaptureGO.SetActive(_nodeType == NodeType.MapCapture);

            _greenBtn_options = null;
            _greenBtn_onClick = null;
            _yellowBtn_options = null;
            GreenBtn.gameObject.SetActive(_nodeType == NodeType.CaptureOper || _nodeType == NodeType.MapCapture);
            YellowBtn.gameObject.SetActive(_nodeType == NodeType.TemplateMatchOper || _nodeType == NodeType.MapCapture);


            bool use_commonParamPanel = _nodeType == NodeType.MapCapture || _nodeType == NodeType.MapPathFinding
                                     || _nodeType == NodeType.ItemGridRecog;
            bool commonParam_hasType = _nodeType == NodeType.MapPathFinding;

            var DelayInputGo = DelayInput.transform.parent;
            var Type_Rect = TypeBox.transform.parent.GetComponent<RectTransform>();
            var CommonParam_Type_Rect = CommonParamTypeBox.transform.parent;
            if (use_commonParamPanel)
            {
                Type_Rect.anchoredPosition = new Vector2(-240, 112);
                Utils.SetActive(DelayInputGo, false);
                Utils.SetActive(CommonParamPanel, true);
                Utils.SetActive(CommonParam_Type_Rect, commonParam_hasType);
            }
            else
            {
                Type_Rect.anchoredPosition = new Vector2(-220, 112);
                Utils.SetActive(DelayInputGo, true);
                Utils.SetActive(CommonParamPanel, false);
                Utils.SetActive(CommonParam_Type_Rect, commonParam_hasType);
            }


            if (_nodeType == NodeType.CaptureOper)
            {
                RefreshCaptureOperPanel();
            }

            else if (_nodeType == NodeType.TemplateMatchOper)
            {
                RefreshTemplateMatchPanel();
            }

            else if (_nodeType == NodeType.MouseOper)
            {
                RefreshMouseOperPanel();
            }

            else if (_nodeType == NodeType.KeyBoardOper)
            {
                RefreshKeyboardOperPanel();
            }

            else if (_nodeType == NodeType.AssignOper)
            {
                RefreshAssignOperPanel();
            }

            else if (_nodeType == NodeType.ConditionOper)
            {
                RefreshConditionOperPanel();
            }
            else if (_nodeType == NodeType.ForOper)
            {
                RefreshForOperPanel();
            }
            else if (_nodeType == NodeType.ItemGridRecog)
            {
                RefreshItemGridRecogPanel();
            }

            else if (_nodeType == NodeType.TriggerEvent || _nodeType == NodeType.ListenEvent)
            {
                RefreshEventOperPanel();
            }

            else if (_nodeType == NodeType.MapCapture)
            {
                RefreshMapCapturePanel();
            }
            else if (_nodeType == NodeType.MapPathFinding)
            {
                RefreshMapPathFindingPanel();
            }
        }

        void OnSelectType(int index)
        {
            var nodeType = AutoDataUIConfig.NodeTypes[index].Item1;
            if (nodeType == _nodeType)
            {
                return;
            }
            _nodeType = nodeType;
            //更新数据
            manager.DeleteNode(_scriptData, _data.Id);
            _data = manager.CreateNode(_scriptData, _data.CanvasId, _data.Pos, nodeType, _data.Id);

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
                _data.Delay = minDelay;
                DelayInput.SetText(minDelay + "s");
            }

            // 这里刷一下, 防止Delay小于holdtime
            if (_data.NodeType == NodeType.MouseOper)
                RefreshMouseOperPanel();
            else if (_data.NodeType == NodeType.KeyBoardOper)
                RefreshKeyboardOperPanel();


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

        bool CheckExpressionIsLegal(string str)
        {
            str = str.Replace(" ", "");
            if (string.IsNullOrEmpty(str)) return false;
            if (str.IndexOf('=') >= 0) return false;

            return AutoDataUIConfig.ExpressionIsLegal(str);
        }

        void OnClickYellowBtn()
        {
            if (_yellowBtn_options == null)
                return;

            Utils.SetActive(TipsComp, true);
            TipsComp.SetData(_yellowBtn_options.ToList(), null, 400);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var targetR = YellowBtn.GetComponent<RectTransform>();
            var offset = new Vector2(-targetR.rect.width / 2, -targetR.rect.height / 2) + new Vector2(0, -5);
            var pos = Utils.GetPos(tipsCompRectT, targetR, offset, true);
            tipsCompRectT.anchoredPosition = pos;
        }

        void OnClickGreenBtn()
        {
            if (_greenBtn_options == null)
                return;

            Utils.SetActive(TipsComp, true);
            TipsComp.SetData(_greenBtn_options.ToList(), _greenBtn_onClick, 400);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var targetR = GreenBtn.GetComponent<RectTransform>();
            var offset = new Vector2(-targetR.rect.width / 2, -targetR.rect.height / 2) + new Vector2(0, -5);
            var pos = Utils.GetPos(tipsCompRectT, targetR, offset, true);
            tipsCompRectT.anchoredPosition = pos;
        }

        #region CaptureOper

        void RefreshCaptureOperPanel()
        {
            var data = _data as CaptureOperNode;
            SaveCaptureCheck.SetData(data.SaveCaptureToLocal, (isOn) =>
            {
                data.SaveCaptureToLocal = isOn;
            });

            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.RegionsExpression = str;
                string format = AutoDataUIConfig.FormulaFormat(data.RegionsExpression);  //格式化
                ConditionInput.SetText(format);

                RefreshDrawPanel();
            };


            string t_format = AutoDataUIConfig.FormulaFormat(data.RegionsExpression);  //格式化
            ConditionInput.SetData(t_format, save_func);
            InputUseKeywordTips(ConditionInput);
            ConditionInput.UseCheckBox(CheckExpressionIsLegal);

            CaptureOperDebug();
        }

        void InputUseKeywordTips(InputTextProComp input)
        {
            var varRef = _scriptData.GetAllTips();
            input.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetAssignMatchList(search, varRef); }
            , AutoDataUIConfig.GetAssignKeyword);

        }

        void CaptureOperDebug()
        {
            _greenBtn_options = new List<string>()
            {
                "显示截图范围",
            };

            _greenBtn_onClick = (index) =>
            {
                if (index == 0)     //"显示截图范围"
                {
                    if (_scriptData.IsEnd)
                        return;
                    var data = _data as CaptureOperNode;
                    var Regions = _scriptData.FormulaGetResultV4L(data.RegionsExpression);
                    var draw = new List<CVMatchResult>();

                    for (int i = 0; i < Regions.Length; i++)
                    {
                        draw.Add(new CVMatchResult() { Rect = CVRect.ConvertV4Bigger(Regions[i]), UIType = 3 });
                    }

                    UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { draw, 3.0f });
                }
            };

        }

        #endregion

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

            TemplateMatchDebug();
        }

        void SetRegionInput()
        {
            var data = _data as TemplateMatchOperNode;

            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.RegionExpression = str;
                string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
                RegionInput.SetText(format);

                RefreshDrawPanel();
            };

            string format = AutoDataUIConfig.FormulaFormat(data.RegionExpression);  //格式化
            RegionInput.SetData(format, save_func);
            InputUseKeywordTips(RegionInput);
            RegionInput.UseCheckBox(CheckExpressionIsLegal);
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

        void TemplateMatchDebug()
        {
            var data = _data as TemplateMatchOperNode;
            _yellowBtn_options = new List<string>();
            if (data.Meet_min_score <= 1)
            {
                _yellowBtn_options.Add($"<color='#069D00'>匹配中的最小值：{data.Meet_min_score}</color>");
            }

            if (data.Unmeet_max_score >= 0)
            {
                _yellowBtn_options.Add($"<color='#df3106ff'>未匹配上的最大值：{data.Unmeet_max_score}</color>");
            }

        }


        #endregion

        #region AssignOper
        void RefreshAssignOperPanel()
        {

            var data = _data as AssignOperNode;

            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
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
                bool has = TryGetVarInfo(str, out var info, out bool has_bracket);
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
                    var type = AutoDataUIConfig.ConvertVarType(info.Type, has_bracket);
                    VarTypeBox.SetCurIndex(AutoDataUIConfig.VarTypes.IndexOf(type));
                    VarTypeBox.SetLock(true);
                }
                else
                {
                    VarTypeBox.SetLock(false);
                }
            };

            string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
            AssignInput.SetData(format, save_func, update_func);
            InputUseKeywordTips(AssignInput);

            VarTypeBox.SetData(AutoDataUIConfig.VarTypeNames, (index) =>
            {
                data.VarShowType = AutoDataUIConfig.VarTypes[index];

                RefreshAssignCheckBox();
                RefreshDrawPanel();
            }, TipsComp);

            VarTypeBox.SetCurIndex(AutoDataUIConfig.VarTypes.IndexOf(data.VarShowType));
            // 初始刷一下
            //
            update_func(format);
        }

        void RefreshAssignCheckBox()
        {
            var data = _data as AssignOperNode;
            var text = AssignInput.GetText();
            text = text.Replace(" ", "");
            bool isLegal = _scriptData.CheckFormulaLegal(text, data.VarShowType);

            AssignInput.ValidCheck.SetData(isLegal);
            bool has = TryGetVarInfo(text, out var info, out _);
            if (!has)
            {
                AssignCheckNum.text = "0";
            }
            else
            {
                var count = info.Nodes.Count;               // 包含本次
                AssignCheckNum.text = count.ToString();
            }
        }
        // 文本到info
        public bool TryGetVarInfo(string text, out FormulaVarInfo_Edit info, out bool has_bracket)
        {
            has_bracket = false;
            var i = text.IndexOf('=');
            if (i < 0)
            {
                info = new FormulaVarInfo_Edit();
                return false;
            }
            var varName = text.Substring(0, i).Trim();
            int slice_i = varName.IndexOf('[');
            has_bracket = slice_i > -1;
            if (has_bracket)
                varName = varName.Substring(0, slice_i);
            bool has = _scriptData.GetFormulaVarInfo_Edit(varName, out info);
            return has;
        }
        #endregion

        #region ConditionOper
        void RefreshConditionOperPanel()
        {
            var data = _data as ConditionOperNode;

            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.Formula = str;
                string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
                ConditionInput.SetText(format);

                RefreshDrawPanel();
            };


            string t_format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
            ConditionInput.SetData(t_format, save_func);
            InputUseKeywordTips(ConditionInput);
            ConditionInput.UseCheckBox(CheckConditionIsLegal);
        }

        bool CheckConditionIsLegal(string str)
        {
            str = str.Replace(" ", "");
            bool isLegal = AutoDataUIConfig.ConditionIsLegal(str);
            return isLegal;
        }

        #endregion
        #region ForOper
        void RefreshForOperPanel()
        {
            var data = _data as ForOperNode;

            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.Formula = str;
                string format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
                ConditionInput.SetText(format);

                RefreshDrawPanel();
            };


            string t_format = AutoDataUIConfig.FormulaFormat(data.Formula);  //格式化
            ConditionInput.SetData(t_format, save_func);
            InputUseKeywordTips(ConditionInput);
            ConditionInput.UseCheckBox(RefreshForCheckBox);
        }

        bool RefreshForCheckBox(string str)
        {
            str = str.Replace(" ", "");
            bool isLegal = _scriptData.CheckForExpressionLegal(str);
            return isLegal;
        }

        #endregion

        #region MouseOper
        void RefreshMouseOperPanel()
        {
            var data = _data as MouseOperNode;
            data.HoldTime = data.HoldTime;

            MouseOperTypeBox.SetData(AutoDataUIConfig.MouseClickTypes,
            (index) =>
            {
                data.ClickType = index;
                RefreshDrawPanel();
            }, TipsComp);
            MouseOperTypeBox.SetCurIndex(data.ClickType);


            Action<string> save_func = (str) =>
            {
                str = str.Replace(" ", "");
                data.ClickPos = str;
                string format = AutoDataUIConfig.FormulaFormat(data.ClickPos);  //格式化
                MouseOperPosInput.SetText(format);

                RefreshDrawPanel();
            };

            string format = AutoDataUIConfig.FormulaFormat(data.ClickPos);  //格式化
            MouseOperPosInput.SetData(format, save_func);
            InputUseKeywordTips(MouseOperPosInput);
            MouseOperPosInput.UseCheckBox(CheckExpressionIsLegal);

            MouseHoldTimeInput.SetData(data.HoldTime + "s", (str) =>
            {
                if (str.EndsWith("s"))
                    str = str.Substring(0, str.Length - 1);
                if (float.TryParse(str, out float value))
                {
                    data.HoldTime = value;
                    MouseHoldTimeInput.SetText(data.HoldTime + "s");
                }
            });
        }



        #endregion

        #region KeyboardOper
        void RefreshKeyboardOperPanel()
        {
            var data = _data as KeyBoardOperNode;
            data.HoldTime = data.HoldTime;


            List<string> list = AutoDataUIConfig.GetKeyboardOperTypeNameList();
            KeyboardTypeBox.SetData(list, (index) =>
            {
                data.Type = (KeyBoardOperType)index;
                Utils.SetActive(KeyboardHoldTimeInput.transform.parent, data.Type == KeyBoardOperType.FullPress);
            }, TipsComp);

            KeyboardTypeBox.SetCurIndex((int)data.Type);

            Action<string> save_func = (str) =>
            {
                data.Key = str;
                RefreshDrawPanel();
            };

            KeyboardInput.SetData(data.Key, save_func);
            KeyboardInput.UseKeywordTips(TipsComp, AutoDataUIConfig.GetKeyboardMatchList);
            KeyboardInput.UseCheckBox(KeyboardCheck, AutoDataUIConfig.IsLegalKeyboardName);
            KeyboardHoldTimeInput.SetData(data.HoldTime + "s", (str) =>
            {
                if (str.EndsWith("s"))
                    str = str.Substring(0, str.Length - 1);
                if (float.TryParse(str, out float value))
                {
                    data.HoldTime = value;
                    KeyboardHoldTimeInput.SetText(data.HoldTime + "s");
                }

            });
            KeyboardPipeInput.SetData(data.Pipe,
            (str) => { data.Pipe = str; });
        }

        #endregion

        #region  Trigger/Listen
        void RefreshEventOperPanel()
        {
            bool isTrigger = _nodeType == NodeType.TriggerEvent;
            if (isTrigger)
            {

                var data = _data as TriggerEventNode;
                EventNameInput.SetData(AutoDataUIConfig.FormulaFormat(data.EventName),
                str =>
                {
                    str = str.Replace(" ", "");
                    data.EventName = str;
                    string format = AutoDataUIConfig.FormulaFormat(data.EventName);  //格式化
                    EventNameInput.SetText(format); // 可能会格式化
                    RefreshDrawPanel();
                }, str =>
                {
                    UpdateEventOperPanel();
                });


            }
            else
            {
                var data = _data as ListenEventNode;
                EventNameInput.SetData(AutoDataUIConfig.FormulaFormat(data.EventName),
                str =>
                {
                    str = str.Replace(" ", "");
                    data.EventName = str;
                    string format = AutoDataUIConfig.FormulaFormat(data.EventName);
                    EventNameInput.SetText(format); // 可能会格式化
                    RefreshDrawPanel();
                }, str =>
                {
                    UpdateEventOperPanel();
                });

            }
            var datas = _scriptData.Edit_TriggerNodes;
            EventNameInput.UseKeywordTips(TipsComp
            , (search) => { return AutoDataUIConfig.GetEventMatchList(search, datas); });

            UpdateEventOperPanel();
        }
        void UpdateEventOperPanel()
        {
            var text = EventNameInput.GetText();
            text = text.Replace(" ", "");
            var tokens = AutoDataUIConfig.TokenizeFormat(text);
            var result = 0;

            if (tokens.Count > 0)
            {
                bool isTrigger = _nodeType == NodeType.TriggerEvent;
                if (isTrigger)
                {
                    var list = _scriptData.GetListenNodes(tokens[0]);
                    result = list.Count;
                }
                else
                {
                    var list = _scriptData.GetEditTriggerNodes(tokens[0]);
                    result = list.Count;
                }
            }

            EventNum.text = result.ToString();

        }
        #endregion

        #region MapCapture
        void RefreshMapCapturePanel()
        {
            var data = _data as MapCaptureNode;


            _inParam_edit_list = new InputTextSVItemData[]
            {
                new InputTextSVItemData("Map_Id:", data.MapId),
                new InputTextSVItemData("Region_Expression:", AutoDataUIConfig.FormulaFormat(data.RegionExpression)),
            };
            _outParam_edit_list = new InputTextSVItemData[]
            {
                new InputTextSVItemData("P1_Pos:", data.P1Pos),
                new InputTextSVItemData("P2_Pos:", data.P2Pos),
                new InputTextSVItemData("Item_Pos[]:", data.ItemPosList),
                new InputTextSVItemData("Boss_Pos[]:", data.BossPosList),
            };

            _inParam_onEditEnd = (_) =>
            {
                data.MapId = _inParam_edit_list[0].Content;
                data.RegionExpression = _inParam_edit_list[1].DoFormulaFormat();
                RefreshParamContent();
            };
            _outParam_onEditEnd = (_) =>
            {
                data.UnloadData();
                data.P1Pos = _outParam_edit_list[0].Content;
                data.P2Pos = _outParam_edit_list[1].Content;
                data.ItemPosList = _outParam_edit_list[2].Content;
                data.BossPosList = _outParam_edit_list[3].Content;
                data.LoadData();
                RefreshParamContent();
            };

            RefreshParamContent();

            MapCaptureDebug();
        }

        void MapCaptureDebug()
        {
            var data = _data as MapCaptureNode;
            var mapData = MapDataManager.Inst.Get(data.MapId);
            if (mapData == null)
                return;

            var list = mapData.AccuracyRecord;
            List<string> options = new List<string>();
            float sum = 0;
            // 5个一行 ,倒着 遍历
            if (list.Count > 0)
            {
                var l = new List<float>();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var tuple = list[i];
                    sum += tuple.Item2;
                    l.Add(tuple.Item2);

                    int index = tuple.Item1;
                    int delta = index % 5 - 1;
                    if (delta == 0)
                    {
                        var str = $"{tuple.Item1}帧 — ";

                        for (int j = l.Count - 1; j >= 0; j--)
                            str += $"{l[j].ToString("F2")}  ";       // 注意：两位小数不能省略哦

                        options.Add(str);
                        l.Clear();
                    }
                }

            }

            var average = sum / list.Count;
            options.Insert(0, $"平均准确率 — {average.ToString("F3")}");
            options.InsertRange(0, mapData.GetPrintResult());
            _yellowBtn_options = options;


            _greenBtn_options = new List<string>()
            {
                "Show Tool",
            };

            _greenBtn_onClick = (index) =>
            {
                if (index == 0)     //"显示截图范围"
                {
                    var data = _data as MapCaptureNode;
                    UIManager.Inst.ShowPanel(PanelEnum.ImageCompareTestPanel, new string[] { data.MapId, _drawPanel._id });
                }
            };

        }

        #endregion


        #region CommonParamPanel

        void RefreshParamContent()
        {
            if (_inParam_edit_list == null) return;
            var inShow = new string[_inParam_edit_list.Length];
            for (int i = 0; i < _inParam_edit_list.Length; i++)
            {
                var edit_info = _inParam_edit_list[i];
                inShow[i] = $"{edit_info.Title} {edit_info.Content}";
            }

            InParamListComp.SetData(inShow, spacing: 6);

            var outShow = new string[_outParam_edit_list.Length];
            for (int i = 0; i < _outParam_edit_list.Length; i++)
            {
                var edit_info = _outParam_edit_list[i];
                outShow[i] = $"{edit_info.Title} {edit_info.Content}";
            }

            OutParamListComp.SetData(outShow, spacing: 6);

        }

        void OnClickInParamBtn()
        {
            Utils.SetActive(InputTextSV, true);
            InputTextSV.SetData(_inParam_edit_list.ToList(), _inParam_onEditEnd, 500, 7);

            var rectT = InputTextSV.GetComponent<RectTransform>();
            var targetR = InParamBtn.GetComponent<RectTransform>();
            var offset = new Vector2(targetR.rect.width / 2, targetR.rect.height / 2) + new Vector2(5, -5);
            var pos = Utils.GetPos(rectT, targetR, offset, true);
            rectT.anchoredPosition = pos;
        }
        void OnClickOutParamBtn()
        {
            Utils.SetActive(InputTextSV, true);
            InputTextSV.SetData(_outParam_edit_list.ToList(), _outParam_onEditEnd, 500, 7);

            var rectT = InputTextSV.GetComponent<RectTransform>();
            var targetR = OutParamBtn.GetComponent<RectTransform>();
            var offset = new Vector2(targetR.rect.width / 2, targetR.rect.height / 2) + new Vector2(5, -5);
            var pos = Utils.GetPos(rectT, targetR, offset, true);
            rectT.anchoredPosition = pos;
        }


        #endregion


        #region MapPathFinding

        void RefreshMapPathFindingPanel()
        {
            var data = _data as MapPathFindingNode;

            CommonParamTypeBox.SetData(AutoDataUIConfig.GetMapPFTypeNameList(), (index) =>
            {
                data.FindMode = (PathFindingType)index;
                InitMapPathFindingEditList();
                RefreshParamContent();
            }, TipsComp);

            CommonParamTypeBox.SetCurIndex((int)data.FindMode);

        }

        void InitMapPathFindingEditList()
        {
            var data = _data as MapPathFindingNode;
            var type = data.FindMode;
            var inParam = data.GetInputParam();
            var outParam = data.GetOutputParam();
            switch (type)
            {
                case PathFindingType.ExploreFog:
                    if (inParam.Length < 4) return;
                    _inParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("Map_Id:", inParam[0]),
                        new InputTextSVItemData("Player:", inParam[1]),
                        new InputTextSVItemData("Avoid_Factor:", inParam[2]),
                        new InputTextSVItemData("Team_Dist:", inParam[3]),
                    };
                    _outParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("Move_Dir:", outParam[0]),
                    };
                    break;
                case PathFindingType.FollowPlayer:
                    if (inParam.Length < 6) return;
                    _inParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("Map_Id:", inParam[0]),
                        new InputTextSVItemData("Target:", inParam[1]),
                        new InputTextSVItemData("P1:", inParam[2]),
                        new InputTextSVItemData("P2:", inParam[3]),
                        new InputTextSVItemData("Stop_Dist:", inParam[4]),
                        new InputTextSVItemData("Avoid_Factor:", inParam[5]),
                    };
                    _outParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("P1_Dir:", outParam[0]),
                        new InputTextSVItemData("P2_Dir:", outParam[1]),
                    };
                    break;
                case PathFindingType.ReachPos:
                    if (inParam.Length < 6) return;
                    _inParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("Map_Id:", inParam[0]),
                        new InputTextSVItemData("Player:", inParam[1]),
                        new InputTextSVItemData("Reach_Pos:", inParam[2]),
                        new InputTextSVItemData("Stop_Dist:", inParam[3]),
                        new InputTextSVItemData("Avoid_Factor:", inParam[4]),
                        new InputTextSVItemData("Team_Dist:", inParam[5]),
                    };
                    _outParam_edit_list = new InputTextSVItemData[]
                    {
                        new InputTextSVItemData("Move_Dir:", outParam[0]),
                    };
                    break;
                default:
                    _inParam_edit_list = new InputTextSVItemData[0];
                    _outParam_edit_list = new InputTextSVItemData[0];
                    break;
            }


            _inParam_onEditEnd = (_) =>
            {
                var in_param = new string[_inParam_edit_list.Length];
                for (int i = 0; i < _inParam_edit_list.Length; i++)
                {
                    in_param[i] = _inParam_edit_list[i].Content;
                }
                data.SetInputParam(in_param);
                RefreshParamContent();
            };
            _outParam_onEditEnd = (_) =>
            {
                var out_param = new string[_outParam_edit_list.Length];
                for (int i = 0; i < _outParam_edit_list.Length; i++)
                {
                    out_param[i] = _outParam_edit_list[i].Content;
                }
                data.SetOutputParam(out_param);
                RefreshParamContent();
            };


        }

        #endregion

        #region ItemGridRecog
        void RefreshItemGridRecogPanel()
        {
            var data = _data as ItemGridRecogNode;


            _inParam_edit_list = new InputTextSVItemData[]
            {
                new InputTextSVItemData("Pos_Type:", $"{(int)(data.PosType)}"),
                new InputTextSVItemData("Start_Pos:", AutoDataUIConfig.FormulaFormat(data.StartPos)),
            };
            _outParam_edit_list = new InputTextSVItemData[]
            {
                new InputTextSVItemData("Result_Prefix:", data.ResultPrefix),
            };

            _inParam_onEditEnd = (_) =>
            {
                data.PosType = (ItemGridPosType)int.Parse(_inParam_edit_list[0].Content);
                data.StartPos = _inParam_edit_list[1].DoFormulaFormat();
                RefreshParamContent();
            };
            _outParam_onEditEnd = (_) =>
            {
                data.ResultPrefix = _outParam_edit_list[0].Content;
                RefreshParamContent();
            };

            RefreshParamContent();
        }

        #endregion

    }
}