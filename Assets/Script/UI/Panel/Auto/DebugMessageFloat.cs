
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
        [SerializeField] private TextScrollView LogListComp;

        List<TextUIData> _logs;
        void Awake()
        {
            _animEnable = false;
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
                LogListComp.Clear();
                return;
            }

            LogListComp.SetData(_logs, ScrollToPos.Bottom);
        }

        void OnMessageRefresh(ScriptLogType type)
        {
            var cur_index = TabComp.GetCurIndex();
            var cur_type = (ScriptLogType)cur_index;

            if (cur_type == type)
                LogListComp.SetData(_logs, ScrollToPos.StayStill);

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


    }
}