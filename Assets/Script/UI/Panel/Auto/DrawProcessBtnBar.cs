
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
            CloseBtn.onClick.AddListener(OnClickCloseBtn);
            CreateNodeBtn.onClick.AddListener(OnClickCreateNodeBtn);
            DebugBtn.onClick.AddListener(OnClickDebugBtn);
        }


        void OnEnable()
        {
            AutoScriptManager.Inst.OnScriptEnd += RefreshBtn;
        }

        void OnDisable()
        {
            AutoScriptManager.Inst.OnScriptEnd -= RefreshBtn;
        }

        public void SetData(DrawProcessPanel panel)
        {
            _panel = panel;
            RefreshBtn();
        }


        void RefreshBtn()
        {
            var isRuning = AutoScriptManager.Inst.IsRuning(_panel._id);
            var path = "Common/Sprites/New/" + (isRuning ? "b_stop" : "b_start");
            AssetUtil.SetImage(path, RunBtn.GetComponent<Image>());

        }

        void OnClickRunBtn()
        {
            if (AutoScriptManager.Inst.IsRuning(_panel._id))
            {
                AutoScriptManager.Inst.StopScript(_panel._id);
            }
            else
            {
                AutoScriptManager.Inst.StartScript(_panel._id);
                CloseElsePanel();
            }
            RefreshBtn();
        }

        void OnClickCloseBtn()
        {
            AutoScriptManager.Inst.TerminateScript(_panel._id);
        }


        void OnClickCreateNodeBtn()
        {
            _panel.CreateNewNode();
        }

        void OnClickDebugBtn()
        {
            UIManager.Inst.ShowPanel(PanelEnum.ProcessDebugPanel, _panel);
        }

        public static void CloseElsePanel()
        {
            UIManager.Inst.PopPanel(PanelEnum.ScriptManagerPanel);
            UIManager.Inst.PopPanel(PanelEnum.DrawProcessPanel);
            UIManager.Inst.PopPanel(PanelEnum.ProcessNodeInfoPanel);
            UIManager.Inst.PopPanel(PanelEnum.ProcessDebugPanel);
        }
    }
}