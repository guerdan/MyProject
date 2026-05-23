
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography.X509Certificates;
using Script.Framework;
using Script.Framework.AssetLoader;
using Script.Framework.Else;
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
        [SerializeField] private Button TermBtn;                // 终止 Terminate
        [SerializeField] private Button RunBtn;                 // 启动/暂停
        [SerializeField] private GameObject ErrorIcon;          // 错误标志

        [SerializeField] private Text LastNodeName;                 // 执行流的上个节点
        [SerializeField] private DeskPetNodeIcon LastNodeIcon;      // 

        // 浮窗节点
        [SerializeField] private Transform FloatParent;             // 浮窗系统的父节点
        [SerializeField] public KeywordTipsComp TipsCompShared;     // 共享的单菜单

        public string ScriptId => _script_id;
        public AutoScriptManager Manager => AutoScriptManager.Inst;
        public GlobalKeyboardManager KeyManager => GlobalKeyboardManager.Inst;

        MenuSystem MenuSystem;                      //菜单
        DeskPetMapFloat MapFloat;                   //小地图窗
        string _script_id;
        AutoScriptData _scriptData;

        void Awake()
        {
            TermBtn.onClick.AddListener(OnClickTermBtn);
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


            Utils.SetActive(LastNodeName, false);
            Utils.SetActive(LastNodeIcon, false);

        }

        void OnEnable()
        {
            Manager.OnChangeScriptStatus += RefreshRunBtn;
            KeyManager.AddListener(KeyboardEnum.Q, true, PressAltSwitchMapStatus);
            KeyManager.AddListener(KeyboardEnum.D1, true, PressAltStop);
            KeyManager.AddListener(KeyboardEnum.D2, true, PressAltTerminate);

        }

        void OnDisable()
        {
            Manager.OnChangeScriptStatus -= RefreshRunBtn;
            KeyManager.RemoveListener(KeyboardEnum.Q, true, PressAltSwitchMapStatus);
            KeyManager.RemoveListener(KeyboardEnum.D1, true, PressAltStop);
            KeyManager.RemoveListener(KeyboardEnum.D2, true, PressAltTerminate);
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
                ScriptName.text = "无《记》";
                Utils.SetActive(ErrorIcon, false);
                ClearNodeInfo();
                return;
            }
            ScriptName.text = _scriptData.Config.Name + "《记》";

            Utils.SetActive(ErrorIcon, _scriptData.IsError);
            if (_scriptData.IsEnd)
            {
                ClearNodeInfo();
                return;
            }

            var node_id = _scriptData.HotSpotNodeId;

            var node = _scriptData.NodeDatas[node_id];
            NodeName.text = node.Id + "行";
            NodeCountDown.text = $"{(node.Delay - node.Timer).ToString("F1")}s";
            NodeIcon.SetData(script_id, node_id, true);


            // 注释，隐藏
            // string last_node_id = node.ExcuteLastNodeId;
            // if (last_node_id != null)
            // {
            //     var last_node = _scriptData.NodeDatas[last_node_id];
            //     LastNodeName.text = last_node.Name;
            //     LastNodeIcon.SetData(script_id, last_node_id, false);
            // }
            // else
            // {
            //     LastNodeName.text = "";
            //     LastNodeIcon.SetData(null, null, false);
            // }
        }

        void RefreshRunBtn()
        {
            int active_child = -1;
            bool showTerm = false;

            if (_scriptData != null)
            {
                active_child = _scriptData.IsRunning ? 0 : 1;
                showTerm = !_scriptData.IsEnd;
            }

            Utils.SetActive(TermBtn, showTerm);
            for (int i = 0; i < 2; i++)
            {
                Transform child = RunBtn.transform.GetChild(i);
                Utils.SetActive(child.gameObject, i == active_child);
            }

        }
        void ClearNodeInfo()
        {
            NodeName.text = "无";
            NodeCountDown.text = "";
            LastNodeName.text = "";
            NodeIcon.SetData(null, null, false);
            LastNodeIcon.SetData(null, null, false);
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


                MenuSystem.SetData(options, new float[3] { 106, 120, 120 }, 10);
            }

            // 修改

            List<(string, string, Action)> change_options = new List<(string, string, Action)>();
            change_options.Add(($"{(int)MO.RecentScript}", "打开最近", null));
            var open_recent = Manager.Settings.OpenRecent;
            for (int i = open_recent.Count - 1; i >= 0; i--)
            {
                var l = open_recent[i];
                var id = l[0];
                var name = l[1];
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
            var config = new PanelRunConfig();
            Vector2 screenPoint = Utils.GetScreenPos(_rectT);
            config.WinPos = screenPoint + new Vector2(100, 100);
            UIManager.Inst.ShowPanel(PanelEnum.DebugMessageFloat, null, config);
        }
        void OpenDebugVarsFloat()
        {
            var config = new PanelRunConfig();
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

        void OnClickTermBtn()
        {
            var id = Manager.HotSpotScriptId;
            Manager.TerminateScript(id);
        }
        void OnClickRunBtn()
        {
            var id = Manager.HotSpotScriptId;
            Utils.AutoScriptSwitchRunStatus(id);
        }


        // 开始或暂停脚本。在两种状态切换
        void PressAltStop()
        {
            if (KeyManager.GetStatus(KeyboardEnum.Alt) && ScriptId != null)
            {
                bool is_run = Manager.IsRuning(ScriptId);
                ScriptRunCommand command = is_run ? ScriptRunCommand.StopScript : ScriptRunCommand.StartScript;

                var list = Manager.GetAllPipeNames();
                foreach (var name in list)
                    Manager.AddPipeMsg(name, command);
            }
        }

        // 终止脚本
        void PressAltTerminate()
        {
            if (KeyManager.GetStatus(KeyboardEnum.Alt) && ScriptId != null)
            {
                var list = Manager.GetAllPipeNames();
                foreach (var name in list)
                    Manager.AddPipeMsg(name, ScriptRunCommand.TerminateScript);
            }
        }

        /// <summary>
        /// 切换 debug_status开关。 各脚本按需自行实现开关逻辑
        /// </summary>
        void PressAltSwitchMapStatus()
        {
            if (KeyManager.GetStatus(KeyboardEnum.Alt) && _script_id != null)
            {
                var script = AutoScriptManager.Inst.GetScriptData(_script_id);
                // 只能再加一个标志位
                var VarName = "debug_status";
                float status = script.FormulaGetResult(VarName);
                var change_status = status == 0 ? 1 : 0;
                script.RunAssignFormula($"{VarName}={change_status}", FormulaVarType.Float, default);
            }
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