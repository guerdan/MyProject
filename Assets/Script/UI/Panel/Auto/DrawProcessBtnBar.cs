
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class DrawProcessBtnBar : MonoBehaviour
    {
        [SerializeField] private Button RunBtn;         //运行按钮
        [SerializeField] private Button CloseBtn;       //关闭按钮
        [SerializeField] private Button CreateNodeBtn;  //创建节点按钮
        [SerializeField] private Button DebugBtn;       //打开Debug窗

        DrawProcessPanel _panel;

        void Awake()
        {
            RunBtn.onClick.AddListener(OnClickRunBtn);
            CloseBtn.onClick.AddListener(OnClickTerminateBtn);
            CreateNodeBtn.onClick.AddListener(OnClickCreateNodeBtn);
            DebugBtn.onClick.AddListener(OnClickDebugBtn);
        }


        void OnEnable()
        {
            AutoScriptManager.Inst.OnChangeScriptStatus += RefreshBtn;
        }

        void OnDisable()
        {
            AutoScriptManager.Inst.OnChangeScriptStatus -= RefreshBtn;
        }

        public void SetData(DrawProcessPanel panel)
        {
            _panel = panel;
            RefreshBtn();
        }


        void RefreshBtn()
        {
            AutoScriptData data = AutoScriptManager.Inst.GetScriptData(_panel._id);
            string name = data.IsRunning ? "b_stop" : "b_start";

            var path = "Common/Sprites/New/" + name;
            AssetUtil.SetImage(path, RunBtn.GetComponent<Image>());

            var gray = new Color(0.8f, 0.8f, 0.8f, 1);
            CloseBtn.GetComponent<Image>().color = data.IsEnd ? gray : Color.white;
        }

        void OnClickRunBtn()
        {
            Utils.AutoScriptSwitchRunStatus(_panel._id);
            RefreshBtn();
        }

        void OnClickTerminateBtn()
        {
            AutoScriptManager.Inst.TerminateScript(_panel._id);
            RefreshBtn();
        }


        void OnClickCreateNodeBtn()
        {
            _panel.CreateNewNode();
        }

        void OnClickDebugBtn()
        {
            UIManager.Inst.ShowPanel(PanelEnum.ProcessDebugPanel, _panel);
        }

    }
}