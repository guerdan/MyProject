
using System;
using System.Collections.Generic;
using Script.Framework;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.DeskPet
{
    public class DeskPetMain : BasePanel
    {
        // 日记内容
        [SerializeField] private Text ScriptName;               // 当前脚本名
        [SerializeField] private Text NodeName;                 // 某执行流的当前节点，名字
        [SerializeField] private DeskPetNodeIcon NodeIcon;      // 图标
        [SerializeField] private Text NodeCountDown;            // 倒计时
        [SerializeField] private Button RunBtn;                 // 启动/暂停
        [SerializeField] private GameObject ErrorIcon;          // 错误标志

        [SerializeField] private Text LastNodeName;                 // 执行流的上个节点
        [SerializeField] private DeskPetNodeIcon LastNodeIcon;      // 

        // 浮窗节点
        [SerializeField] private Transform FloatParent;             // 浮窗系统的父节点
        [SerializeField] public KeywordTipsComp TipsCompShared;     // 共享的单菜单

        public string ScriptId => _script_id;
        public AutoScriptManager Manager => AutoScriptManager.Inst;

        MenuSystem MenuSystem;                      //菜单
        DeskPetMapFloat MapFloat;                   //小地图窗
        string _script_id;
        AutoScriptData _scriptData;

        void Awake()
        {

            RunBtn.onClick.AddListener(OnClickRunBtn);
            Utils.SetActive(TipsCompShared, false);

            //异步加载菜单
            var path = "Common/Prefabs/Component/MenuSystem";
            AssetManager.Inst.LoadAssetAsync<GameObject>(path, (prefab) =>
            {
                var obj = Instantiate(prefab);
                obj.transform.SetParent(this.transform, true);
                Utils.SetActive(MenuSystem, false);

                MenuSystem = obj.GetComponent<MenuSystem>();
            }, this);
        }



        public override void SetData(object data)
        {

        }

        void Update()
        {
            UpdateStatus();

            if (Input.GetMouseButtonDown(1) && Utils.IsPointerOverUIObject(_rectT, Root.Inst.Canvas))
            {
                var screenP = Input.mousePosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectT, screenP
                    , null, out var localPoint);

                OpenMenus(localPoint);
            }


        }

        void UpdateStatus()
        {
            _scriptData = null;

            var script_id = Manager.HotSpotScriptId;
            _script_id = script_id;

            if (Manager.HotSpotScriptId != null)
            {
                _scriptData = Manager.GetScriptData(Manager.HotSpotScriptId);
            }

            RefreshRunBtn();
            if (_scriptData == null)
            {
                ScriptName.text = "无执行脚本";
                ClearNodeInfo();
                return;
            }
            ScriptName.text = _scriptData.Config.Name;

            Utils.SetActive(ErrorIcon, _scriptData.IsError);
            if (_scriptData.IsEnd)
            {
                ClearNodeInfo();
                return;
            }

            var node_id = _scriptData.HotSpotNodeId;
            
            var node = _scriptData.NodeDatas[node_id];
            NodeName.text = node.Name;
            NodeCountDown.text = $"{(node.Delay - node.Timer).ToString("F1")}s";
            NodeIcon.SetData(script_id, node_id, true);

            string last_node_id = node.ExcuteLastNodeId;
            if (last_node_id != null)
            {
                var last_node = _scriptData.NodeDatas[last_node_id];
                LastNodeName.text = last_node.Name;
                LastNodeIcon.SetData(script_id, last_node_id, false);
            }
            else
            {
                LastNodeName.text = "";
                LastNodeIcon.SetData(null, null, false);
            }
        }

        void RefreshRunBtn()
        {
            int active_child = -1;

            if (_scriptData != null)
                if (_scriptData.IsRunning)
                    active_child = 0;
                else
                    active_child = _scriptData.IsEnd ? 1 : 2;

            for (int i = 0; i < 3; i++)
            {
                Transform child = RunBtn.transform.GetChild(i);
                Utils.SetActive(child.gameObject, i == active_child);
            }

        }
        void ClearNodeInfo()
        {
            NodeName.text = "无执行节点";
            NodeCountDown.text = "";
            LastNodeName.text = "";
            NodeIcon.SetData(null, null, false);
            LastNodeIcon.SetData(null, null, false);

            Utils.SetActive(ErrorIcon, false);
        }



        void OpenMenus(Vector2 click_pos)
        {
            if (!MenuSystem.Init)
            {
                List<(string, string, Action)> options = new List<(string, string, Action)>();

                options.Add(($"{(int)MO.CurrentScript}", "打开", () =>
                   { if (ScriptId != null) Utils.OpenDrawProcessPanel(ScriptId); }
                ));
                options.Add(($"{(int)MO.Debug}", "debug", null));
                options.Add(($"{(int)MO.Debug}_0", "日志", OpenDebugMessageFloat));
                options.Add(($"{(int)MO.Debug}_1", "变量", OpenDebugVarsFloat));
                options.Add(($"{(int)MO.Debug}_2", "小地图", OpenMapFloat));
                options.Add(($"{(int)MO.OpenScriptManager}", "管理器", () => { UIManager.Inst.ShowPanel(PanelEnum.ScriptManagerPanel, null); }));
                options.Add(($"{(int)MO.Minimize}", "最小化", () => { AutoRoot.Inst.Minimize(); }));
                options.Add(($"{(int)MO.Quit}", "关闭", () => { AutoRoot.Inst.Quit(); }));


                MenuSystem.SetData(options, 106, 10);
            }

            // 修改

            List<(string, string, Action)> change_options = new List<(string, string, Action)>();
            change_options.Add(($"{(int)MO.RecentScript}", "打开最近", null));
            var open_recent = Manager.Settings.OpenRecent;
            for (int i = open_recent.Count - 1; i >= 0; i--)
            {
                var id = open_recent[i];
                var name = Manager.GetScriptData(id).Config.Name;
                // var show_str = $"{name}" + (_script_id == id ? " (cur)" : "");
                var show_str = $"{name}";
                change_options.Add(($"{(int)MO.RecentScript}_{open_recent.Count - 1 - i}", show_str, () =>
                    {
                        Utils.OpenDrawProcessPanel(id);
                    }
                ));
            }
            MenuSystem.ChangeData(change_options);




            var MenusCompRectT = MenuSystem.GetComponent<RectTransform>();
            var pos = click_pos + new Vector2(5, 0);
            MenusCompRectT.anchoredPosition = pos;

            Utils.SetActive(MenuSystem, true);
            MenuSystem.Open();
        }

        void OpenDebugMessageFloat()
        {
            var config = new BasePanelConfig();
            Vector2 screenPoint = Utils.GetScreenPos(_rectT);
            config.WinPos = screenPoint + new Vector2(100, 100);
            UIManager.Inst.ShowPanel(PanelEnum.DebugMessageFloat, null, config);
        }
        void OpenDebugVarsFloat()
        {
            var config = new BasePanelConfig();
            Vector2 screenPoint = Utils.GetScreenPos(_rectT);
            config.WinPos = screenPoint + new Vector2(100, 100);
            UIManager.Inst.ShowPanel(PanelEnum.DebugVarsFloat, _script_id, config);
        }


        void OpenMapFloat()
        {
            Action doAction = () =>
            {
                Utils.SetActive(MapFloat, true);
                MapFloat.GetComponent<RectTransform>().anchoredPosition = new Vector2(240, 0);
                MapFloat.SetData(this);
            };

            if (MapFloat == null)
            {
                //异步加载菜单
                var path = "Auto/Prefabs/DeskPetMapFloat";
                AssetManager.Inst.LoadAssetAsync<GameObject>(path, (prefab) =>
                {
                    if (MapFloat == null)
                    {
                        var obj = Instantiate(prefab);
                        obj.transform.SetParent(FloatParent, false);
                        MapFloat = obj.GetComponent<DeskPetMapFloat>();
                        doAction();
                    }
                }, this);
            }
            else
            {
                doAction();
            }

        }

        void OnClickRunBtn()
        {
            var id = Manager.HotSpotScriptId;
            Utils.AutoScriptSwitchRunStatus(id);
            RefreshRunBtn();

        }

        /// <summary>
        /// MenuOption
        /// </summary>
        enum MO
        {
            CurrentScript,
            RecentScript,
            Debug,
            OpenScriptManager,
            Minimize,
            Quit,
        }
    }
}