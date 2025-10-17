
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Component;
using UnityEngine;

namespace Script.UI.Panel.Auto
{
    public class ProcessDebugPanel : BasePanel
    {
        [SerializeField] private CheckBox ScreenDrawBtn;       //开启屏幕绘制
        public AutoScriptManager manager => AutoScriptManager.Inst;

        DrawProcessPanel _panel;

        public override void SetData(object data)
        {
            _useScaleAnim = false;
            _panel = data as DrawProcessPanel;
            Refresh();
        }
        void Refresh()
        {
            ScreenDrawBtn.SetData(manager.ScreenDrawStatus, OnScreenDrawChange);
        }
        void OnScreenDrawChange(bool value)
        {
            manager.ScreenDrawStatus = value;
        }

    }
}