
using Script.Framework.AssetLoader;
using Script.Model.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class DrawProcessBtnBar : MonoBehaviour
    {
        [SerializeField] private Button RunBtn; //运行按钮
        [SerializeField] private Button CreateNodeBtn; //创建节点按钮

        DrawProcessPanel _panel;

        void Awake()
        {
        }


        void OnEnable()
        {
            RunBtn.onClick.AddListener(OnRunBtnClick);
            CreateNodeBtn.onClick.AddListener(OnCreateNodeBtnClick);
        }

        void OnDisable()
        {
            RunBtn.onClick.RemoveListener(OnRunBtnClick);
            CreateNodeBtn.onClick.RemoveListener(OnCreateNodeBtnClick);
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

        void OnRunBtnClick()
        {
            if (AutoScriptManager.Inst.IsRuning(_panel._id))
            {
                AutoScriptManager.Inst.StopScript(_panel._id);
            }
            else
            {
                AutoScriptManager.Inst.StartScript(_panel._id);
            }
            RefreshBtn();
        }

        void OnCreateNodeBtnClick()
        {
            _panel.CreateNewNode();
        }
    }
}