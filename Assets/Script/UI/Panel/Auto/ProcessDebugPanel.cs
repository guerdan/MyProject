
using System.Collections.Generic;
using System.Linq;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ProcessDebugPanel : BasePanel
    {
        public AutoScriptManager manager => AutoScriptManager.Inst;

        [SerializeField] private CheckBox ScreenDrawBtn;        //开启屏幕绘制
        [SerializeField] private CheckBox SaveMapCaptureBtn;    //保存地图拍摄快照
        [SerializeField] private Button LookRunTimeBtn;         //查看执行耗时按钮
        [SerializeField] private KeywordTipsComp TipsComp;

        DrawProcessPanel _panel;

        void Awake()
        {
            LookRunTimeBtn.onClick.AddListener(OnClickLookRunTimeBtn);
        }


        public override void SetData(object data)
        {
            _useScaleAnim = false;
            _panel = data as DrawProcessPanel;
            Utils.SetActive(TipsComp, false);
            Refresh();
        }
        void Refresh()
        {
            ScreenDrawBtn.SetData(manager.ScreenDrawStatus, OnChangeScreenDraw);
            SaveMapCaptureBtn.SetData(manager.SaveMapCaptureStatus, OnChangeSaveMapCapture);
        }
        void OnChangeScreenDraw(bool value)
        {
            manager.ScreenDrawStatus = value;
        }
        void OnChangeSaveMapCapture(bool value)
        {
            manager.SaveMapCaptureStatus = value;
        }

        void OnClickLookRunTimeBtn()
        {
            Utils.SetActive(TipsComp, true);


            List<string> options = GetStrings();

            TipsComp.SetData(options, null, 600);

            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            var targetR = LookRunTimeBtn.GetComponent<RectTransform>();
            var offset = new Vector2(targetR.rect.width / 2, targetR.rect.height / 2) + new Vector2(5, -5);
            var pos = Utils.GetPos(tipsCompRectT, targetR, offset, true);
            tipsCompRectT.anchoredPosition = pos;
        }

        List<string> GetStrings()
        {
            List<string> result = new List<string>();
            var dic = _panel._scriptData.RunTimeDic;
            var keys = dic.Keys.ToList();
            keys.Sort();
            foreach (string mes in keys)
            {
                var data = dic[mes];
                string str = $"[{mes}]  {data.Item1}次  ";
                var list = data.Item2;
                foreach (double ms in list)
                    str += $"{ms.ToString("F1")}ms  ";
                result.Add(str);
            }


            return result;
        }
    }
}