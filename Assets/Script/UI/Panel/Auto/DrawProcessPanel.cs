
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Component;
using Script.UI.Panel.Auto.Node;
using Script.Util;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// 画布配置
    /// </summary>
    public class CanvasConfig
    {
        public float W;
        public float H;

        public CanvasConfig(float width, float height)
        {
            this.W = width;
            this.H = height;
        }
    }


    /// <summary>
    /// 特性：
    /// 1.画分段线，300为间隔
    /// 2.按钮栏（运行、暂停、新建节点）
    /// 3.连线功能，删节点，删线
    /// 4.剔除视窗外的节点、线段（对现成逻辑大修改，也不慢）
    /// 5.节点层级、线段层级、覆盖顺序
    /// 
    /// 节点ui维护机制：用户增/删操作 => 通知逻辑并直接增/删节点ui => 刷节点而删线段
    /// 线段ui维护机制：用户增/删操作 => 通知逻辑并直接增/删线段ui
    /// 
    /// 
    /// </summary>
    public class DrawProcessPanel : BasePanel
    {
        public AutoScriptManager manager => AutoScriptManager.Inst;

        [SerializeField] private Text Title;
        [SerializeField] private Button TitleEditBtn;
        [SerializeField] private ScrollRect ScrollRect;
        [SerializeField] public RectTransform Canvas;
        [SerializeField] public SplitLineComp Map;          //分段线组件, 现在是地图的地位
        [SerializeField] private GameObject LinePre;        //线段预制件
        [SerializeField] public Transform NodeParent;       //节点父物体
        [SerializeField] private Transform LineParent;      //线段父物体
        [SerializeField] private DrawProcessBtnBar BtnBar;
        [SerializeField] public KeywordTipsComp TipsComp;  //提示词组件


        [Header("各式节点预制件")]
        [SerializeField] public GameObject TemplateMatchPre;   //普通节点
        [SerializeField] public GameObject MouseNodePre;    //鼠标节点
        [SerializeField] public GameObject KeyboardNodePre; //键盘节点
        [SerializeField] public GameObject AssignNodePre;   //赋值节点

        public static event Action OnMouseSelected;
        private string _mouseSelectedId;
        //当前选中的节点 格式：
        //节点id: "node-{index}"
        //线段id: "line-{from}-{to}"
        public string MouseSelectedId
        {
            get => _mouseSelectedId; set
            {
                var last = _mouseSelectedId;
                _mouseSelectedId = value;
                RefreshUISelectedStatus(last);
                RefreshUISelectedStatus(_mouseSelectedId);
                OnMouseSelected?.Invoke();
            }
        }

        [NonSerialized] public CanvasConfig CanvasCfg;

        [NonSerialized] public string MouseHoverId;         //悬浮的节点

        [NonSerialized] public string _id;                                  //脚本id
        [NonSerialized] public AutoScriptData _scriptData;                  //脚本运行时数据

        SPool _templateMatchNodeUIPool;
        SPool _mouseNodeUIPool;
        SPool _keyboardNodeUIPool;
        SPool _assignNodeUIPool;

        SPool _linePool;
        GameObject _tempLine; //拖拽时的临时线段

        //Dic<id,实体>  这样才是严谨、不错的存储
        Dictionary<string, ProcessNodeUI> _nodeUIMap = new Dictionary<string, ProcessNodeUI>();
        //Dic<id,实体> id = "line-{from}-{to}"，from和to线段的两端节点作为key,设计前提是两节点间最多一根线
        Dictionary<string, GameObject> _lineUIMap = new Dictionary<string, GameObject>();

        DateTime _lastSaveTime;


        void Awake()
        {
            TitleEditBtn.onClick.AddListener(OnTitleEditBtnClick);
            ScrollRect.onValueChanged.AddListener(OnScrolling);

            //初始化预制件
            LinePre.gameObject.SetActive(false);
            TemplateMatchPre.SetActive(false);
            MouseNodePre.SetActive(false);
            KeyboardNodePre.SetActive(false);
            AssignNodePre.SetActive(false);

            var trans_line = LinePre.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);

            _linePool = new SPool(LinePre, 0, "LineUI");
            _templateMatchNodeUIPool = new SPool(TemplateMatchPre, 0, "C_NodeUI");
            _mouseNodeUIPool = new SPool(MouseNodePre, 0, "M_NodeUI");
            _keyboardNodeUIPool = new SPool(KeyboardNodePre, 0, "K_NodeUI");
            _assignNodeUIPool = new SPool(AssignNodePre, 0, "A_NodeUI");
        }

        public override void SetData(object data)
        {
            _useScaleAnim = false;

            _id = data as string;
            _scriptData = manager.GetScriptData(_id);
            _lastSaveTime = DateTime.Now;
            Init();
        }

        void Init()
        {
            CanvasCfg = new CanvasConfig(4000, 4000);

            Map.SetData(this, 300);  //绘制栅格
            BtnBar.SetData(this);

            //生成流程的节点
            OnScrolling(ScrollRect.normalizedPosition);

            Title.text = $"绘制面板：{_scriptData.Config.Name}";
        }


        void OnTitleEditBtnClick()
        {
            Action<string, string> OnConfirm = (string name, string _) =>
            {
                manager.RenameScript(_id, name);
                Title.text = $"绘制面板：{_scriptData.Config.Name}";
            };

            EditNamePanelParam param = new EditNamePanelParam
            {
                Target = Title.rectTransform,
                Offset = new Vector2(0, -30),
                PanelTitle = "重命名脚本",
                Region0Title = "脚本名",
                Region0Text = _scriptData.Config.Name,
                OnConfirm = OnConfirm,
            };

            UIManager.Inst.ShowPanel(PanelEnum.EditNamePanel, param);
        }
        void OnScrolling(Vector2 pos)
        {
            SyncData();
        }

        #region Update
        public bool HoldCtrl;

        void Update()
        {

            //上方有界面就应该跳过
            BasePanel last = UISceneMixin.Inst.PeekPanel(PanelDefine.Layer) as BasePanel;
            if (last && last.PanelDefine.Key != PanelDefine.Key)
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S))
            {
                manager.SaveScript(_id);
            }

            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                if (MouseSelectedId != null)
                {
                    if (MouseSelectedId.StartsWith(BaseNodeData.IdStart))
                    {
                        // 删除节点
                        DeleteNode(MouseSelectedId);
                        MouseSelectedId = null;
                    }
                    else if (MouseSelectedId.StartsWith("line-"))
                    {
                        // 删除线段
                        var s = MouseSelectedId.Substring(5);
                        int first = s.IndexOf('-');
                        int second = s.IndexOf('-', first + 1);
                        string from = s.Substring(0, second);      // "from"id
                        string to = s.Substring(second + 1);    // "to"id

                        DeleteLine(from, to);
                        MouseSelectedId = null;
                    }
                }
            }

            // 按着Ctrl键，点击节点出引用
            HoldCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        }

        #endregion
        #region 节点
        // 只显示在视窗内的节点 (ok)
        // 完全同步节点(id对应的方案)，完全同步线段
        public void SyncData()
        {
            HashSet<string> node_ids = Map.GetInViewNodeIds();
            HashSet<string> lines_ids = new HashSet<string>();

            // 删节点
            foreach (var id in _nodeUIMap.Keys.ToList())
            {
                if (!node_ids.Contains(id))
                {
                    var ui = _nodeUIMap[id];
                    PushToPool(ui);
                    _nodeUIMap.Remove(id);
                }
            }


            foreach (var id in node_ids)
            {
                // 增节点
                if (!_nodeUIMap.ContainsKey(id))
                {
                    var data = _scriptData.NodeDatas[id];
                    var type = data.NodeType;
                    var nodeUI = GetFromPool(type);
                    nodeUI.transform.SetParent(NodeParent, false);
                    _nodeUIMap[id] = nodeUI;
                    nodeUI.SetData(data, this);
                }

                // 统计所有的线段
                lines_ids.AddRange(manager.GetLinesByNode(_scriptData, id));
            }



            // 删线段
            foreach (var id in _lineUIMap.Keys.ToList())
            {
                if (!lines_ids.Contains(id))
                {
                    var line = _lineUIMap[id];
                    _linePool.Push(line);
                    _lineUIMap.Remove(id);
                }
            }

            // 增线段
            foreach (var id in lines_ids)
            {
                if (!_lineUIMap.ContainsKey(id))
                {
                    var line = _linePool.Pop();
                    line.transform.SetParent(LineParent, false);
                    _lineUIMap[id] = line;

                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.SetData(id, this);
                    ui.DrawLine();
                }
            }

        }

        // 获取节点
        public ProcessNodeUI GetNode(string id)
        {
            _nodeUIMap.TryGetValue(id, out var nodeUI);
            return nodeUI;
        }
        // 创建新节点功能
        public void CreateNewNode()
        {
            var pos = Map.MapConvert(Map.GetCenter());
            var data = manager.CreateNode(_scriptData, NodeType.TemplateMatchOper, pos);
            SyncData();

            // 确保新节点在最上层
            var new_ui = _nodeUIMap[data.Id];
            new_ui.transform.SetAsLastSibling();
        }
        // 删除节点功能
        public void DeleteNode(string id)
        {
            if (id == null) return;
            if (!_nodeUIMap.ContainsKey(id)) return;
            // 逻辑先更新，ui后更新
            manager.DeleteNode(_scriptData, id);
            SyncData();
        }

        // 刷新节点功能
        public void RefreshNode(string id)
        {
            if (id == null) return;
            if (!_nodeUIMap.TryGetValue(id, out var nodeUI)) return;

            // UI层主动删除节点
            _nodeUIMap.Remove(id);
            PushToPool(nodeUI);
            // 刷新出新节点
            SyncData();
        }

        /// <summary>
        /// 从对象池中拿ui
        /// </summary>
        public void GetPool(NodeType type, out SPool pool, out GameObject prefab)
        {
            pool = null;
            prefab = null;
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                    pool = _templateMatchNodeUIPool;
                    prefab = TemplateMatchPre;
                    break;

                case NodeType.MouseOper:
                    pool = _mouseNodeUIPool;
                    prefab = MouseNodePre;
                    break;

                case NodeType.KeyBoardOper:
                    pool = _keyboardNodeUIPool;
                    prefab = KeyboardNodePre;
                    break;

                case NodeType.AssignOper:
                case NodeType.ConditionOper:
                case NodeType.TriggerEvent:
                case NodeType.ListenEvent:
                    pool = _assignNodeUIPool;
                    prefab = AssignNodePre;
                    break;

            }
        }

        /// <summary>
        /// 从对象池中拿ui
        /// </summary>
        public ProcessNodeUI GetFromPool(NodeType type)
        {
            GetPool(type, out var pool, out var _);
            GameObject go = pool.Pop();

            return go.GetComponent<ProcessNodeUI>();
        }
        /// <summary>
        /// 把ui还给对象池 
        /// </summary>
        public void PushToPool(ProcessNodeUI nodeUI)
        {
            var type = nodeUI._data.NodeType;
            GetPool(type, out var pool, out var _);
            pool.Push(nodeUI.gameObject);
        }


        // 刷新选中状态
        public void RefreshUISelectedStatus(string id)
        {
            if (id == null) return;

            if (id.StartsWith(BaseNodeData.IdStart))
            {
                if (_nodeUIMap.TryGetValue(id, out var ui))
                {
                    ui.RefreshSelected();
                    ui.RefreshLine();
                }
            }

            if (id.StartsWith(BaseNodeData.LineIdStart))
            {
                if (_lineUIMap.TryGetValue(id, out var line))
                {
                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.DrawLine();
                }
            }

        }

        #endregion

        #region 线段


        // 按id列表刷新线段
        public void RefreshLineByNode(string node_id)
        {

            var line_ids = manager.GetLinesByNode(_scriptData, node_id);

            foreach (var id in line_ids)
            {
                if (_lineUIMap.TryGetValue(id, out GameObject line))
                {
                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.DrawLine();
                }
            }
        }

        // 数据层面删线段
        public void DeleteLine(string from_id, string to_id)
        {
            manager.DeleteLine(_scriptData, from_id, to_id);
            SyncData();
        }


        // 编辑-连线功能 -- 获取临时线段
        public void ShowLineForDrag(Vector2 from, Vector2 to, Color color)
        {
            if (_tempLine == null)
            {
                _tempLine = _linePool.Pop();
                _tempLine.transform.SetParent(LineParent, false);
                _tempLine.GetComponent<Image>().raycastTarget = false; // 不响应鼠标事件
            }
            _tempLine.SetActive(true);
            _tempLine.GetComponent<ProcessNodeLineUI>().DrawLine(from, to, color);
        }
        public void HideLineForDrag()
        {
            _tempLine.SetActive(false);
        }

        // 
        Dictionary<NodeType, Vector2[]> _lineEndPosCache;

        // type: 0-in, 1-out_true, 2-out_false  ,画布(0,0)为原点下的坐标
        public Vector2 GetLineEndPos(BaseNodeData nData, int type)
        {
            if (_lineEndPosCache == null)
            {
                _lineEndPosCache = new Dictionary<NodeType, Vector2[]>();
                foreach (var t in AutoDataUIConfig.NodeTypes)
                {
                    GetPool(t, out var _, out var go);

                    if (t == NodeType.ConditionOper)
                    {
                        var template = go.GetComponent<AssignNodeUI>();
                        var l = new Vector2[3];
                        _lineEndPosCache[t] = l;
                        l[0] = Utils.GetRelativePosToParent(template.InflowNode);
                        l[1] = Utils.GetRelativePosToParent(template.TopCircle.GetComponent<RectTransform>());
                        l[2] = Utils.GetRelativePosToParent(template.BottomCircle.GetComponent<RectTransform>());
                    }
                    else
                    {
                        GetLineEndOwnership(t, out bool in_own, out bool out_true_own, out bool out_false_own);
                        var template = go.GetComponent<ProcessNodeUI>();
                        var l = new Vector2[3];
                        _lineEndPosCache[t] = l;
                        if (in_own) l[0] = Utils.GetRelativePosToParent(template.InflowNode);
                        if (out_true_own) l[1] = Utils.GetRelativePosToParent(template.TrueOutNode);
                        if (out_false_own) l[2] = Utils.GetRelativePosToParent(template.FalseOutNode);
                    }
                }
            }

            var list = _lineEndPosCache[nData.NodeType];
            return Map.MapConvert(nData.Pos) + list[type];
        }

        public void GetLineEndOwnership(NodeType type, out bool in_has, out bool out_true_has, out bool out_false_has)
        {
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                case NodeType.ConditionOper:
                    in_has = true;
                    out_true_has = true;
                    out_false_has = true;
                    break;
                case NodeType.MouseOper:
                case NodeType.KeyBoardOper:
                case NodeType.AssignOper:
                    in_has = true;
                    out_true_has = true;
                    out_false_has = false;
                    break;
                case NodeType.TriggerEvent:
                    in_has = true;
                    out_true_has = false;
                    out_false_has = false;
                    break;
                case NodeType.ListenEvent:
                    in_has = false;
                    out_true_has = true;
                    out_false_has = false;
                    break;
                default:
                    in_has = false;
                    out_true_has = false;
                    out_false_has = false;
                    break;
            }
        }
        #endregion

        void OnDisable()
        {
            manager.SaveScript(_id);
        }

        void LateUpdate()
        {
            var now = DateTime.Now;
            if ((now - _lastSaveTime).TotalSeconds >= 2 * 60)
            {
                manager.SaveScript(_id);
                _lastSaveTime = now;
            }
        }
    }
}