
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
    public class DebugMessageFloat : BasePanel
    {
        public AutoScriptManager Manager = AutoScriptManager.Inst;

        [SerializeField] private PageTabComp TabComp;
        [SerializeField] private GameObject[] TabNums;
        [SerializeField] private VirtualListComp LogListComp;
        [SerializeField] private GameObject itemPrefab;

        List<(string, float)> _logs;
        float _text_width;
        int _text_fontSize;
        void Awake()
        {
            _animEnable = false;
            _text_width = itemPrefab.GetComponent<RectTransform>().rect.size.x;
            _text_fontSize = itemPrefab.GetComponentInChildren<Text>().fontSize;

            itemPrefab.SetActive(false);
            LogListComp.OnGetItemSize = GetItemSize;
            LogListComp.OnGetItemTemplate = (int index) => itemPrefab;
            LogListComp.OnUpdateItem = UpdateItem;
            TabComp.SetNum(new List<int>() { 0, 0, 0 });

            // Manager.AddLog(ScriptLogType.Log, "如果遇到了，“峰型”大格子，空地在“峰”的两边并且不相通。这种情况就是有5 X 3像素的障碍。可能性很小，或者加打印提醒下。");
            // Manager.AddLog(ScriptLogType.Log, "我觉得要标注，无法连通所有空地的大格子。对它在各环节特殊处理。");
            // Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Warning, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            // Manager.AddLog(ScriptLogType.Error, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");


            // for (int i = 0; i < 100; i++)
            // {
            //     var k = i;
            //     GameTimer.Inst.SetTimeOnce(this, () =>
            //     {
            //         var a = k;
            //         Manager.AddLog(ScriptLogType.Log, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            //         Manager.AddLog(ScriptLogType.Warning, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            //         Manager.AddLog(ScriptLogType.Error, "算障碍地块，不能用200X200的得用最可靠的范围—150X150。");
            //     }, i);
            // }
        }

        void OnEnable()
        {
            Manager.OnMessageRefresh += OnMessageRefresh;
        }

        void OnDisable()
        {
            Manager.OnMessageRefresh -= OnMessageRefresh;
        }

        public override void SetData(object data)
        {
            TabComp.SetData(OnSelectTab, 3, 0);
            RefreshLogNum();
        }

        void OnSelectTab(int page)
        {
            ScriptLogType type = (ScriptLogType)page;
            if (!Manager.LogDic.TryGetValue(type, out _logs))
            {
                LogListComp.ReloadData(0, false);
                return;
            }

            LogListComp.ReloadData(_logs.Count, true);
            LogListComp.ScrollToBottom();
        }

        void OnMessageRefresh(ScriptLogType type)
        {
            var cur_index = TabComp.GetCurIndex();
            var cur_type = (ScriptLogType)cur_index;

            if (cur_type == type)
                LogListComp.ReloadData(_logs.Count, false);

            RefreshLogNum();
        }

        void RefreshLogNum()
        {
            List<int> nums = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var num = 0;
                if (Manager.LogDic.TryGetValue((ScriptLogType)i, out var list))
                    num = list.Count;
                nums.Add(num);
            }

            TabComp.SetNum(nums);
        }



        Vector2 GetItemSize(int index)
        {
            var data = _logs[index];
            string content = data.Item1;
            float height = 0;
            if (data.Item2 < 0)
            {
                height = SceneTool.Inst.GetTextPreferHeight(_text_width, _text_fontSize, content);
                data.Item2 = height;
                _logs[index] = data;
            }
            else
            {
                height = data.Item2;
            }


            return new Vector2(_text_width, height + 12);
        }

        void UpdateItem(GameObject item, int index)
        {
            var data = _logs[index];
            float height = data.Item2;
            string content = data.Item1;

            var rectT = item.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(rectT.sizeDelta.x, height + 12);      //高度
            item.GetComponentInChildren<Text>().text = content;                 //内容

            Utils.SetActive(item.transform.GetChild(0), index % 2 == 0);        //背景
        }

    }
}