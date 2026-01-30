
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
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
        public static string LastOpenId;
        public static event Action OnMouseSelected;

        public AutoScriptManager manager => AutoScriptManager.Inst;

        [SerializeField] private Text Title;
        [SerializeField] private Text IdText;
        [SerializeField] private Button TitleEditBtn;
        [SerializeField] private ScrollRect ScrollRect;
        [SerializeField] public RectTransform Canvas;
        [SerializeField] public SplitLineComp Map;          //分段线组件, 现在是地图的地位
        [SerializeField] private GameObject LinePre;        //线段预制件
        [SerializeField] public Transform NodeParent;       //节点父物体
        [SerializeField] public Transform LineParent;       //线段父物体
        [SerializeField] public Transform TopLayer;         //顶层
        [SerializeField] public Transform RecycleLayer;     //回收层，放delete后的缓存
        [SerializeField] private DrawProcessBtnBar BtnBar;
        [SerializeField] public KeywordTipsComp TipsComp;  //提示词组件


        [Header("各式节点预制件")]
        [SerializeField] public GameObject TemplateMatchPre;    //模版匹配节点
        [SerializeField] public GameObject MouseNodePre;        //鼠标节点
        [SerializeField] public GameObject AssignNodePre;       //赋值节点
        [SerializeField] public GameObject MapNodePre;          //地图相关节点

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
        [NonSerialized] public string MouseHoverId;                         // 鼠标悬浮的节点
        [NonSerialized] public string _id;                                  // 脚本id
        [NonSerialized] public AutoScriptData _scriptData;                  // 脚本运行时数据
        [NonSerialized] public bool HoldCtrl;                               // 长按Ctrl键状态


        SPool _templateMatchNodeUIPool;
        SPool _mouseNodeUIPool;
        SPool _assignNodeUIPool;
        SPool _mapCaptureNodeUIPool;


        SPool _linePool;
        GameObject _tempLine; //拖拽时的临时线段

        //Dic<id,实体>  这样才是严谨、不错的存储
        Dictionary<string, ProcessNodeUI> _nodeUIMap = new Dictionary<string, ProcessNodeUI>();
        //Dic<id,实体> id = "line-{from}-{to}"，from和to线段的两端节点作为key,设计前提是两节点间最多一根线
        Dictionary<string, GameObject> _lineUIMap = new Dictionary<string, GameObject>();

        DateTime _lastSaveTime;

        private string _mouseSelectedId;        //选中元素
        private string _copyId;                 //选中并复制的元素


        // 存八个插槽.方位位置
        public Dictionary<NodeType, Vector2[]> nodeUISlotPosCfg;
        // 存节点默认使用插槽情况。<类型, <type,槽序>>
        Dictionary<NodeType, int[]> _nodeUIGate2SlotDefault;
        // 存节点使用插槽情况。<拼接id, 槽序>
        Dictionary<string, int> _nodeUIGate2SlotCache;


        void Awake()
        {
            TitleEditBtn.onClick.AddListener(OnTitleEditBtnClick);
            ScrollRect.onValueChanged.AddListener(OnScrolling);

            //初始化预制件
            LinePre.gameObject.SetActive(false);
            TemplateMatchPre.SetActive(false);
            MouseNodePre.SetActive(false);
            AssignNodePre.SetActive(false);
            MapNodePre.SetActive(false);

            var trans_line = LinePre.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);

            _linePool = new SPool(LinePre, 0, "LineUI", RecycleLayer);
            _templateMatchNodeUIPool = new SPool(TemplateMatchPre, 0, "T_NodeUI", RecycleLayer);
            _mouseNodeUIPool = new SPool(MouseNodePre, 0, "M_NodeUI", RecycleLayer);
            _assignNodeUIPool = new SPool(AssignNodePre, 0, "A_NodeUI", RecycleLayer);
            _mapCaptureNodeUIPool = new SPool(MapNodePre, 0, "Map_NodeUI", RecycleLayer);
        }

        public override void SetData(object data)
        {
            _useScaleAnim = false;

            _id = data as string;
            LastOpenId = _id;
            _scriptData = manager.GetScriptData(_id);
            _lastSaveTime = DateTime.Now;
            Init();
        }

        void Init()
        {
            Save();
            InitNodeSlotPos();

            CanvasCfg = new CanvasConfig(4000, 4000);
            Map.SetData(this, 300);  //绘制栅格
            BtnBar.SetData(this);

            Clear();

            //生成流程的节点
            OnScrolling(ScrollRect.normalizedPosition);

            Title.text = $"绘制面板：{_scriptData.Config.Name}";
            IdText.text = $"id: {_scriptData.Config.Id}";
        }
        // 全删
        void Clear()
        {
            // 删节点
            foreach (var id in _nodeUIMap.Keys.ToList())
            {
                var ui = _nodeUIMap[id];
                PushToPool(ui);
                _nodeUIMap.Remove(id);
            }

            // 删线段
            foreach (var id in _lineUIMap.Keys.ToList())
            {
                var line = _lineUIMap[id];
                _linePool.Push(line);
                _lineUIMap.Remove(id);
            }

            // 清线段端点- 插槽缓存
            _nodeUIGate2SlotCache.Clear();
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

        void Update()
        {

            //上方有界面就应该跳过
            BasePanel last = UISceneMixin.Inst.PeekPanel(PanelUtil.GetLayer(UITypeEnum.WindowsPopUp)) as BasePanel;
            BasePanel last1 = UISceneMixin.Inst.PeekPanel(PanelUtil.GetLayer(UITypeEnum.PopUp)) as BasePanel;
            if (last && last.PanelDefine.Key != PanelDefine.Key || last1 != null)
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
                        int sep = s.IndexOf('-');                   //separator
                        string from = s.Substring(0, sep);      // "from"id
                        string to = s.Substring(sep + 1);    // "to"id

                        DeleteLine(BaseNodeData.IdStart + from, BaseNodeData.IdStart + to);
                        MouseSelectedId = null;
                    }
                }
            }

            // 按着Ctrl键，点击节点出引用
            HoldCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
            {
                _copyId = MouseSelectedId;
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                if (_copyId != null && _copyId.StartsWith(BaseNodeData.IdStart))
                    CopyNode(_copyId);
            }

        }

        #endregion
        #region 节点
        // 只显示在视窗内的节点 (ok)
        // 完全同步节点(id对应的方案)，完全同步线段
        public void SyncData()
        {
            HashSet<string> node_ids = Map.GetInViewNodeIds();
            HashSet<string> lines_ids = new HashSet<string>();
            // 增节点的相关线段都要刷新
            HashSet<string> lines_need_refresh_ids = new HashSet<string>();

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

                    var parent = MouseSelectedId == id ? TopLayer : NodeParent;
                    nodeUI.transform.SetParent(parent, false);
                    _nodeUIMap[id] = nodeUI;
                    nodeUI.SetData(data, this);
                    RefreshNodeSlot(id);

                    var line_ids = manager.GetLinesByNode(_scriptData, id);
                    lines_need_refresh_ids.AddRange(line_ids);
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
            HashSet<string> lines_already_refresh_ids = new HashSet<string>();
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
                    lines_already_refresh_ids.Add(id);
                }
            }

            // 增节点的相关线段都要刷新
            foreach (var id in lines_need_refresh_ids)
            {
                if (!lines_already_refresh_ids.Contains(id)
                    && _lineUIMap.TryGetValue(id, out GameObject line))
                {
                    var ui = line.GetComponent<ProcessNodeLineUI>();
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
            if (_nodeUIMap.TryGetValue(data.Id, out var new_ui))
                new_ui.transform.SetAsLastSibling();
        }
        // 创建新节点功能
        public void CopyNode(string id)
        {
            var pos = Map.MapConvert(Map.GetMousePos());
            var data = manager.CopyNode(_scriptData, id, pos);
            SyncData();

            // 确保新节点在最上层
            if (_nodeUIMap.TryGetValue(data.Id, out var new_ui))
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
            nodeUI.Clear();
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
                case NodeType.KeyBoardOper:
                case NodeType.WaitOper:
                case NodeType.StopScript:
                    pool = _mouseNodeUIPool;
                    prefab = MouseNodePre;
                    break;
             

                case NodeType.AssignOper:
                case NodeType.ConditionOper:
                case NodeType.TriggerEvent:
                case NodeType.ListenEvent:
                    pool = _assignNodeUIPool;
                    prefab = AssignNodePre;
                    break;
                case NodeType.MapCapture:
                case NodeType.MapPathFinding:
                    pool = _mapCaptureNodeUIPool;
                    prefab = MapNodePre;
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
                    RefreshLineByNode(id);
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

        // 增线段
        public void AddLine(AutoScriptData scriptData, string from_id, string to_id, bool isTrue)
        {
            manager.AddLine(scriptData, from_id, to_id, isTrue);
            RefreshNodeSlot(from_id);
            RefreshNodeSlot(to_id);
            RefreshLineByNode(from_id);
            RefreshLineByNode(to_id);
            SyncData();
        }
        // 删线段
        public void DeleteLine(string from_id, string to_id)
        {
            manager.DeleteLine(_scriptData, from_id, to_id);
            RefreshNodeSlot(from_id);
            RefreshNodeSlot(to_id);
            RefreshLineByNode(from_id);
            RefreshLineByNode(to_id);
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

        // 初始化节点插槽位置
        void InitNodeSlotPos()
        {
            if (nodeUISlotPosCfg != null) return;
            nodeUISlotPosCfg = new Dictionary<NodeType, Vector2[]>();
            _nodeUIGate2SlotCache = new Dictionary<string, int>();

            foreach (var t in AutoDataUIConfig.NodeTypes)
            {
                Vector2[] l = null;
                GetPool(t, out var _, out var go);
                var ui = go.GetComponent<ProcessNodeUI>();
                var rectT = go.GetComponent<RectTransform>();
                if (t == NodeType.TemplateMatchOper)
                {
                    l = new Vector2[3];
                    l[0] = Utils.GetRelativePosToParent(ui.InflowNode);
                    l[1] = Utils.GetRelativePosToParent(ui.TrueOutNode);
                    l[2] = Utils.GetRelativePosToParent(ui.FalseOutNode);
                }
                else
                {
                    bool isCircle = t == NodeType.MouseOper || t == NodeType.KeyBoardOper;
                    if (isCircle)
                    {
                        float r_w = rectT.rect.width / 2 + 16;
                        float r_h = r_w;
                        float sqr_w = r_w / 1.414f;
                        float sqr_h = r_h / 1.414f;
                        l = new Vector2[8] {
                            new Vector2(0, -r_h), new Vector2(-sqr_w, -sqr_h), new Vector2(-r_w, 0), new Vector2(-sqr_w, sqr_h),
                            new Vector2(0, r_h), new Vector2(sqr_w, sqr_h), new Vector2(r_w, 0), new Vector2(sqr_w, -sqr_h)
                        };
                    }
                    else
                    {
                        float w_half = rectT.rect.width / 2 + 18;
                        float h_half = rectT.rect.height / 2 + 12;
                        float sqr_w = w_half - 3;
                        float sqr_h = h_half - 3;
                        // w_half -= 3;
                        // h_half -= 3;
                        // float sqr_w = w_half;
                        // float sqr_h = h_half;

                        l = new Vector2[8] {
                            new Vector2(0, -h_half), new Vector2(-sqr_w, -sqr_h), new Vector2(-w_half, 0), new Vector2(-sqr_w, sqr_h),
                            new Vector2(0, h_half), new Vector2(sqr_w, sqr_h), new Vector2(w_half, 0), new Vector2(sqr_w, -sqr_h)
                        };
                    }
                }
                nodeUISlotPosCfg[t] = l;
            }

            _nodeUIGate2SlotDefault = new Dictionary<NodeType, int[]>()
            {
                { NodeType.TemplateMatchOper, new int[]{0,1,2} },

                { NodeType.MouseOper, new int[]{2,6,0} },         // 以下4个一致
                { NodeType.KeyBoardOper, new int[]{2,6,0} },
                { NodeType.WaitOper, new int[]{2,6,0} },
                { NodeType.StopScript, new int[]{2,6,0} },

                { NodeType.AssignOper, new int[]{2,6,0} },
                { NodeType.ConditionOper, new int[]{2,5,7} },
                { NodeType.TriggerEvent, new int[]{2,6,0} },
                { NodeType.ListenEvent, new int[]{2,6,0}},
                { NodeType.MapCapture, new int[]{2,6,0}},
                { NodeType.MapPathFinding, new int[]{2,5,7}},
            };


        }

        /// <summary>
        /// type: 0-in, 1-out_true, 2-out_false  ,画布(0,0)为原点下的坐标。
        /// 每个节点要在_nodeUISlotCache 初始化
        /// </summary>
        public Vector2 GetLineEndPos(BaseNodeData nData, int type)
        {
            Vector2 offset = default;
            var id = $"{nData.Index}-{type}";
            var pos_cfg = nodeUISlotPosCfg[nData.NodeType];

            // 没有的就用默认的
            if (_nodeUIGate2SlotCache.TryGetValue(id, out int slot_i))
            {
                offset = pos_cfg[slot_i];
            }
            else
            {
                slot_i = _nodeUIGate2SlotDefault[nData.NodeType][type];
                offset = pos_cfg[slot_i];
            }

            return Map.MapConvert(nData.Pos) + offset;
        }

        #region Slots


        HashSet<int> _slot_used = new HashSet<int>();
        List<int> _slot_front = new List<int>();
        List<int> _slot_back = new List<int>();


        public void RefreshNodeSlot(string node_id)
        {
            var self_ui = GetNode(node_id);
            if (self_ui == null)
                return;

            // 每帧：移动某节点时，会刷相关联的节点
            // DU.LogWarning($"RefreshNodeSlot：{node_id}");

            BaseNodeData self = _scriptData.NodeDatas[node_id];
            // 模版匹配节点用固定的位置
            if (self.NodeType == NodeType.TemplateMatchOper)
                return;

            var offset_list = nodeUISlotPosCfg[self.NodeType];
            var self_pos = Map.MapConvert(self.Pos);
            var default_list = _nodeUIGate2SlotDefault[self.NodeType];

            GetLineEndOwnership(self.NodeType, out bool _, out bool _, out bool out_false_has);

            // 本地cache > 最近排序 > _nodeUISlotCache内存cache > 默认值
            var all_cache = _scriptData.Config.Slots;
            all_cache.TryGetValue(self.IndexStr, out int[] cache);

            // 三个口需要从8个槽位中挑选位置。-1表示待定
            int[] gates = new int[3] { -1, -1, -1 };

            if (cache != null)
                Array.Copy(cache, gates, 3);

            _slot_used.Clear();
            _slot_front.Clear();
            _slot_back.Clear();

            for (int i = 0; i < 3; i++)
            {
                var slot_i = gates[i];
                if (slot_i > -1)
                    _slot_used.Add(slot_i);
            }

            // 有线的出入口优先级更高
            if (self.LastNode.Count > 0) _slot_front.Add(0); else _slot_back.Add(0);
            if (self.TrueNextNodes.Count > 0) _slot_front.Add(1); else _slot_back.Add(1);
            if (out_false_has)
                if (self.FalseNextNodes.Count > 0) _slot_front.Add(2); else _slot_back.Add(2);

            for (int i = 0; i < _slot_front.Count; i++)
            {
                int gate = _slot_front[i];
                _nodeUIGate2SlotCache[$"{self.IndexStr}-{gate}"] = gates[gate] > -1 ? gates[gate] :
                    Method(self, gate, _slot_used, default_list, self_pos, offset_list);
            }

            for (int i = 0; i < _slot_back.Count; i++)
            {
                int gate = _slot_back[i];
                _nodeUIGate2SlotCache[$"{self.IndexStr}-{gate}"] = gates[gate] > -1 ? gates[gate] :
                    Method(self, gate, _slot_used, default_list, self_pos, offset_list);
            }


            self_ui.RefreshSlotUI();

        }

        int Method(BaseNodeData self, int gate, HashSet<int> used, int[] default_list, Vector2 self_pos, Vector2[] pos_list)
        {
            var slot_i = 0;

            List<string> list = null;
            Vector2 target_pos;

            if (gate == 0) list = self.LastNode;
            else if (gate == 1) list = self.TrueNextNodes;
            else if (gate == 2) list = self.FalseNextNodes;

            if (list.Count > 0)
            {
                BaseNodeData target = _scriptData.NodeDatas[list[0]];
                if (target.NodeType == NodeType.TemplateMatchOper)
                {
                    if (gate == 0) target_pos = target.TrueNextNodes.Contains(self.Id) ? GetLineEndPos(target, 1) : GetLineEndPos(target, 2);
                    else target_pos = GetLineEndPos(target, 0);

                    slot_i = GetMinDistanceSlot(self_pos, pos_list, used, target_pos, null);
                }
                else
                {
                    // 在两个槽偏移列表中，选最小距离的两个槽
                    target_pos = Map.MapConvert(target.Pos);
                    var target_offset_list = nodeUISlotPosCfg[target.NodeType];
                    slot_i = GetMinDistanceSlot(self_pos, pos_list, used, target_pos, target_offset_list);
                }
            }
            else
            {
                slot_i = default_list[gate];
                for (int i = 0; i < 3; i++)
                {
                    if (!used.Contains(slot_i))
                        break;
                    slot_i = (slot_i + 1) % 8;
                }
            }
            used.Add(slot_i);
            return slot_i;
        }


        // 获取最小距离的插槽, 对于长条形节点效果堪忧。
        int GetMinDistanceSlot(Vector2 self_pos, Vector2[] off_list, HashSet<int> used, Vector2 target_pos, Vector2[] target_off_list)
        {
            float min_distance = float.MaxValue;
            int direction = 0;                          //结果
            var share_item = self_pos - target_pos;     //公共项

            if (target_off_list == null)
                for (int i = 0; i < 8; i++)
                {
                    if (used.Contains(i)) continue;
                    var t = (share_item + off_list[i]).SqrMagnitude();
                    if (t < min_distance)
                    {
                        min_distance = t;
                        direction = i;
                    }
                }
            else
                for (int i = 0; i < 8; i++)
                {
                    if (used.Contains(i)) continue;
                    for (int j = 0; j < 8; j++)
                    {
                        var t = (share_item + off_list[i] - target_off_list[j]).SqrMagnitude();
                        if (t < min_distance)
                        {
                            min_distance = t;
                            direction = i;
                        }
                    }
                }

            return direction;
        }


        #endregion
        public void GetLineEndOwnership(NodeType type, out bool in_has, out bool out_true_has, out bool out_false_has)
        {
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                case NodeType.ConditionOper:
                case NodeType.MapPathFinding:
                    in_has = true;
                    out_true_has = true;
                    out_false_has = true;
                    break;
                case NodeType.MouseOper:
                case NodeType.KeyBoardOper:
                case NodeType.AssignOper:
                case NodeType.MapCapture:
                case NodeType.WaitOper:
                case NodeType.TriggerEvent:
                case NodeType.StopScript:
                    in_has = true;
                    out_true_has = true;
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
        #region Others
        void OnDisable()
        {
            Save();
        }

        void Save()
        {
            if (_id != null)
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

        public override void Close()
        {
            base.Close();
            Utils.OpenDeskPet();
        }

        #endregion
    }
}