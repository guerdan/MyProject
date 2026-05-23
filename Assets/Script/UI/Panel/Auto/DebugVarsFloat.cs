
using System.Collections.Generic;
using Script.Framework;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class DebugVarsFloat : BasePanel
    {
        public AutoScriptManager Manager = AutoScriptManager.Inst;

        [SerializeField] private PageTabComp TabComp;
        [SerializeField] private VirtualListComp LogListComp;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private KeywordTipsComp TipsComp;

        int textUI_fontSize;
        float textUI_Height;
        readonly float refreshInternal = 0.2f;
        float _refreshCountdown;
        bool _needRefresh;


        string _script_id;
        AutoScriptData _script_data;
        Dictionary<FormulaVarType, List<VarInfo>> _allLogs;
        List<VarInfo> _curLogs = new List<VarInfo>();
        FormulaVarType _select_type;


        void Awake()
        {
            _animEnable = false;
            textUI_fontSize = itemPrefab.GetComponentInChildren<Text>().fontSize;
            textUI_Height = itemPrefab.GetComponent<RectTransform>().rect.height;

            itemPrefab.SetActive(false);
            LogListComp.OnGetItemSize = GetItemSize;
            LogListComp.OnGetItemTemplate = (int index) => itemPrefab;
            LogListComp.OnUpdateItem = UpdateItem;
            TabComp.SetNum(new List<int>() { 0, 0, 0, 0 });

            Utils.SetActive(TipsComp, false);
        }


        void OnDisable()
        {
            if (_script_data != null) _script_data.OnVarValueChange -= OnChange;
        }

        public override void SetData(object data)
        {
            _script_id = data as string;

            if (_script_id == null)
                return;

            _script_data = Manager.GetScriptData(_script_id);
            _script_data.OnVarValueChange += OnChange;

            Init();
            TabComp.SetData(OnSelectTab, 4, 0);
        }

        void Init()
        {
            Dictionary<string, FormulaVarInfo_Edit> refs = _script_data.GetInEditVarRef();

            _allLogs = new Dictionary<FormulaVarType, List<VarInfo>>();
            var types = new FormulaVarType[]
            { FormulaVarType.Float, FormulaVarType.Vector2, FormulaVarType.Vector4, FormulaVarType.String };
            var typeLs = new FormulaVarType[]
            { FormulaVarType.ListFloat, FormulaVarType.ListVector2, FormulaVarType.ListVector4 , FormulaVarType.ListString};


            List<int> nums = new List<int>();
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var list = new List<VarInfo>();

                foreach (var pair in refs)
                {
                    var var_name = pair.Key;
                    var var_info = pair.Value;
                    var var_type = var_info.Type;
                    if (var_type == type && var_name != "")
                    {
                        VarInfo info = new VarInfo(var_name, var_type);
                        list.Add(info);
                    }
                }


                // 往float类型加入 trigger与bool类型
                if (type == FormulaVarType.Float)
                {
                    foreach (var pair in _script_data.Edit_TriggerNodes)
                    {
                        bool isCondition = pair.Value.Item3;
                        if (!isCondition)
                            continue;
                        VarInfo info = new VarInfo(pair.Key, FormulaVarType.Float);
                        list.Add(info);
                    }

                    foreach (var pair in refs)
                    {
                        var var_name = pair.Key;
                        var var_info = pair.Value;
                        var var_type = var_info.Type;
                        if (var_type == FormulaVarType.Bool && var_name != "")
                        {
                            VarInfo info = new VarInfo(var_name, var_type);
                            list.Add(info);
                        }
                    }
                }

                list.Sort((a, b) => a.VarName.CompareTo(b.VarName));

                // 列表
                var list_l = new List<VarInfo>();
                var typeL = typeLs[i];
                foreach (var pair in refs)
                {
                    var var_name = pair.Key;
                    var var_info = pair.Value;
                    var var_type = var_info.Type;
                    if (var_type == typeL && var_name != "")
                    {
                        VarInfo info = new VarInfo(var_name, var_type);
                        list_l.Add(info);
                    }
                }
                list_l.Sort((a, b) => a.VarName.CompareTo(b.VarName));

                list.AddRange(list_l);
                _allLogs[type] = list;
                nums.Add(list.Count);
            }

            TabComp.SetNum(nums);

        }


        void OnSelectTab(int page)
        {
            _select_type = (FormulaVarType)(page + 1);
            Refresh();
        }

        void Refresh()
        {

            // DU.LogWarning("Refresh");
            _curLogs = _allLogs[_select_type];
            for (int j = 0; j < _curLogs.Count; j++)
            {
                var info = _curLogs[j];
                CheckChange(info);
            }

            LogListComp.ReloadData(_curLogs.Count, false);
        }

        void OnChange()
        {
            _needRefresh = true;
        }


        void Update()
        {
            _refreshCountdown -= Time.deltaTime;
            if (_refreshCountdown <= 0 && _needRefresh)
            {
                Refresh();
                _refreshCountdown = refreshInternal;
                _needRefresh = false;
            }
        }

        void CheckChange(VarInfo info)
        {
            var var_name = info.VarName;
            var var_type = info.VarType;
            if (var_type == FormulaVarType.Float || var_type == FormulaVarType.Bool)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? 0 : (float)data.Value;
                if (info.TextWidth == -1 || newVal != (float)info.Value)
                {
                    var text = "";
                    if (var_type == FormulaVarType.Bool)
                        text = $"{var_name} = {(newVal > 0 ? "<color='#069D00'>true</color>" : "<color='#FF0000'>flase</color>")}";
                    else
                        text = $"{var_name} = {DU.FloatFormat(newVal)}";

                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;

                    // 文本宽度只赋值一次，免得混排方式下Item上窜下跳
                    if (info.TextWidth == -1)
                        info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.Vector2)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? default : (Vector2)data.Value;
                if (info.TextWidth == -1 || newVal != (Vector2)info.Value)
                {
                    var text = $"{var_name} = ({DU.FloatFormat(newVal.x)},{DU.FloatFormat(newVal.y)})";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.Vector4)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? default : (Vector4)data.Value;
                if (info.TextWidth == -1 || newVal != (Vector4)info.Value)
                {
                    var text = $"{var_name} = ({DU.FloatFormat(newVal.x)},{DU.FloatFormat(newVal.y)},{DU.FloatFormat(newVal.z)},{DU.FloatFormat(newVal.w)})";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.String)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? default : (string)data.Value;
                if (info.TextWidth == -1 || newVal != (string)info.Value)
                {
                    var text = $"{var_name} = \"{newVal}\"";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.ListFloat)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? null : (float[])data.Value;
                if (info.TextWidth == -1 || newVal != info.Value)
                {
                    var text = $"{var_name} = " + $"F[{(newVal == null ? 0 : newVal.Length)}]";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.ListVector2)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? null : (Vector2[])data.Value;
                if (info.TextWidth == -1 || newVal != info.Value)
                {
                    var text = $"{var_name} = " + $"V2[{(newVal == null ? 0 : newVal.Length)}]";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.ListVector4)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? null : (Vector4[])data.Value;
                if (info.TextWidth == -1 || newVal != info.Value)
                {
                    var text = $"{var_name} = " + $"V4[{(newVal == null ? 0 : newVal.Length)}]";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.ListString)
            {
                var data = _script_data.GetVarValue(var_name);
                var newVal = data == null ? null : (string[])data.Value;
                if (info.TextWidth == -1 || newVal != info.Value)
                {
                    var text = $"{var_name} = " + $"string[{(newVal == null ? 0 : newVal.Length)}]";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.Value = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
        }


        Vector2 GetItemSize(int index)
        {
            var data = _curLogs[index];
            return new Vector2(data.TextWidth, textUI_Height);
        }

        void UpdateItem(GameObject item, int index)
        {
            var data = _curLogs[index];
            var rectT = item.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(data.TextWidth, rectT.sizeDelta.y);   //高度
            item.GetComponentInChildren<Text>().text = data.Text;               //内容


            var button = item.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClick(item, data));

            // Utils.SetActive(item.transform.GetChild(0), index % 2 == 0);        //背景
        }

        void OnClick(GameObject item, VarInfo info)
        {
            var type = info.VarType;
            if (type != FormulaVarType.ListFloat && type != FormulaVarType.ListVector2
            && type != FormulaVarType.ListVector4 && type != FormulaVarType.ListString)
                return;
            float ui_width = 0;
            var options = new List<string>();

            switch (type)
            {
                case FormulaVarType.ListFloat:
                    {
                        ui_width = 100;
                        var list = (float[])info.Value;
                        if (list != null)
                            for (var i = 0; i < list.Length; i++)
                            {
                                var v = list[i];
                                options.Add($"[{i}] {DU.FloatFormat(v)}");
                            }
                    }
                    break;
                case FormulaVarType.ListVector2:
                    {
                        ui_width = 150;
                        var list = (Vector2[])info.Value;
                        if (list != null)
                            for (var i = 0; i < list.Length; i++)
                            {
                                var v = list[i];
                                options.Add($"[{i}] ({DU.FloatFormat(v.x)}, {DU.FloatFormat(v.y)})");
                            }
                    }
                    break;
                case FormulaVarType.ListVector4:
                    {
                        ui_width = 200;
                        var list = (Vector4[])info.Value;
                        if (list != null)
                            for (var i = 0; i < list.Length; i++)
                            {
                                var v = list[i];
                                options.Add($"[{i}] ({DU.FloatFormat(v.x)}, {DU.FloatFormat(v.y)}, {DU.FloatFormat(v.z)}, {DU.FloatFormat(v.w)})");
                            }
                    }
                    break;
                case FormulaVarType.ListString:
                    {
                        ui_width = 200;
                        var list = (string[])info.Value;
                        if (list != null)
                            for (var i = 0; i < list.Length; i++)
                            {
                                var v = list[i];
                                options.Add($"[{i}] \"{v}\"");
                            }
                    }
                    break;
            }

            if (options.Count == 0)
                return;
            Utils.SetActive(TipsComp, true);
            TipsComp.SetData(options, null, ui_width, 10);
            TipsComp.SetCurIndex(-1);

            var itemR = item.GetComponent<RectTransform>();
            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var pos = Utils.GetPos(tipsCompRectT, itemR
                , new Vector2(-itemR.rect.width / 2, -itemR.rect.height / 2 - 3), true);
            tipsCompRectT.anchoredPosition = pos;
        }

        class VarInfo
        {
            public string VarName;              // 变量名
            public FormulaVarType VarType;      // 变量类型
            public object Value;                // 值
            public string Text;                 // 文本
            public float TextWidth = -1;             // UI文本宽度

            public VarInfo(string varName, FormulaVarType varType)
            {
                VarName = varName; VarType = varType;
            }
        }
    }
}