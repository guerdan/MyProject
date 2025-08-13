
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
            var isRuning = ProcessNodeManager.Inst.IsRuning();
            var path = "Common/Sprites/New/" + (isRuning ? "b_stop" : "b_start");
            AssetUtil.SetImage(path, RunBtn.GetComponent<Image>());

        }

        void OnRunBtnClick()
        {
            if (ProcessNodeManager.Inst.IsRuning())
            {
                ProcessNodeManager.Inst.Stop();
            }
            else
            {
                ProcessNodeManager.Inst.Start();
            }
            RefreshBtn();
        }

        void OnCreateNodeBtnClick()
        {
            _panel.CreateNode();
        }
    }
}