
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private GameObject[] TabNums;
        [SerializeField] private VirtualListComp LogListComp;
        [SerializeField] private GameObject itemPrefab;
        int textUI_fontSize;
        float textUI_Height;
        readonly float refreshInternal = 0.1f;
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
            TabComp.SetNum(new List<int>() { 0, 0, 0 });
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
            TabComp.SetData(OnSelectTab, 3, 0);
        }

        void Init()
        {
            Dictionary<string, FormulaVarInfo> refs = _script_data.GetInEditVarRef();

            _allLogs = new Dictionary<FormulaVarType, List<VarInfo>>();
            var types = new FormulaVarType[] { FormulaVarType.Float, FormulaVarType.Vector2, FormulaVarType.Vector4 };


            List<int> nums = new List<int>();
            foreach (var type in types)
            {
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

                if (type == FormulaVarType.Float)
                {
                    foreach (var pair in _script_data.Edit_TriggerNodes)
                    {
                        VarInfo info = new VarInfo(pair.Key, FormulaVarType.Float);
                        list.Add(info);
                    }
                }

                list.Sort((a, b) => a.VarName.CompareTo(b.VarName));
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
            if (var_type == FormulaVarType.Float)
            {
                var newVal = _script_data.GetFloatVarValue(var_name);
                if (info.TextWidth == -1 || newVal != info.ValueF)
                {
                    var text = "";
                    if (info.isBool)
                        text = $"{var_name} = {(newVal > 0 ? "<color='#069D00'>true</color>" : "<color='#FF0000'>flase</color>")}";
                    else
                        text = $"{var_name} = {DU.FloatFormat(newVal)}";

                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.ValueF = newVal;
                    info.Text = text;

                    // 文本宽度只赋值一次，免得混排方式下Item上窜下跳
                    if (info.TextWidth == -1)
                        info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.Vector2)
            {
                var newVal = _script_data.GetV2VarValue(var_name);
                if (info.TextWidth == -1 || newVal != info.ValueV2)
                {
                    var text = $"{var_name} = ({DU.FloatFormat(newVal.x)},{DU.FloatFormat(newVal.y)})";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.ValueV2 = newVal;
                    info.Text = text;
                    info.TextWidth = width;
                }
            }
            else if (var_type == FormulaVarType.Vector4)
            {
                var newVal = _script_data.GetV4VarValue(var_name);
                if (info.TextWidth == -1 || newVal != info.ValueV4)
                {
                    var text = $"{var_name} = ({DU.FloatFormat(newVal.x)},{DU.FloatFormat(newVal.y)},{DU.FloatFormat(newVal.z)},{DU.FloatFormat(newVal.w)})";
                    var width = SceneTool.Inst.GetTextPreferWidth(textUI_fontSize, text) / 2 + 16;
                    info.ValueV4 = newVal;
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

            // Utils.SetActive(item.transform.GetChild(0), index % 2 == 0);        //背景
        }

        class VarInfo
        {
            public string VarName;              // 变量名
            public FormulaVarType VarType;      // 变量类型
            public bool isBool;
            public float ValueF;                // 值
            public Vector2 ValueV2;             // 值
            public Vector4 ValueV4;             // 值

            public string Text;                 // 文本
            public float TextWidth = -1;             // UI文本宽度

            public VarInfo(string varName, FormulaVarType varType)
            {
                VarName = varName; VarType = varType;
                isBool = VarName.StartsWith("bo_");
            }
        }
    }
}