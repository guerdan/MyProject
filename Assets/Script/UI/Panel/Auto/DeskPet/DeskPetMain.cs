
using System;
using System.Collections.Generic;
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
        RectTransform _rectT;
        string _script_id;

        void Awake()
        {

            RunBtn.onClick.AddListener(OnClickRunBtn);
            Utils.SetActive(TipsCompShared, false);

            _rectT = transform as RectTransform;

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
            AutoScriptData scriptData = null;

            var script_id = Manager.HotSpotScriptId;
            _script_id = script_id;

            if (Manager.HotSpotScriptId != null)
            {
                scriptData = Manager.GetScriptData(Manager.HotSpotScriptId);
            }

            if (scriptData == null)
            {
                Utils.SetActive(RunBtn, false);
                ScriptName.text = "无执行脚本";
                ClearNodeInfo();
                return;
            }
            ScriptName.text = scriptData.Config.Name;

            Utils.SetActive(ErrorIcon, scriptData.IsError);
            Utils.SetActive(RunBtn, true);
            RunBtn.GetComponent<CheckBox>().SetData(!scriptData.Running);
            if (scriptData.IsEnd)
            {
                ClearNodeInfo();
                return;
            }

            var node_id = scriptData.HotSpotNodeId;
            var node = scriptData.NodeDatas[node_id];
            NodeName.text = node.Name;
            NodeCountDown.text = $"{(node.Delay - node.Timer).ToString("F1")}s";
            NodeIcon.SetData(script_id, node_id, true);

            string last_node_id = node.ExcuteLastNodId;
            if (last_node_id != null)
            {
                var last_node = scriptData.NodeDatas[last_node_id];
                LastNodeName.text = last_node.Name;
                LastNodeIcon.SetData(script_id, last_node_id, false);
            }
            else
            {
                LastNodeName.text = "";
                LastNodeIcon.SetData(null, null, false);
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
            Utils.SetActive(RunBtn, false);
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
                options.Add(($"{(int)MO.Debug}_0", "小地图", OpenMapFloat));
                options.Add(($"{(int)MO.Debug}_1", "物品", () => { }));
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
            RunBtn.GetComponent<CheckBox>().SetData(!Manager.IsRuning(id));
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