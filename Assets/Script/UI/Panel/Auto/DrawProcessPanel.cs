
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework;
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

    public enum MouseSelectType
    {
        Undefined,
        Node,
        Line,
    }

    public struct MouseSelectInfo
    {
        public string SelectId;
        public MouseSelectType Type;
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

        public AutoScriptManager Manager => AutoScriptManager.Inst;

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
        [SerializeField] public KeywordTipsComp TipsComp;       //提示词组件

        [SerializeField] private VirtualListComp CanvasTabListComp;   //页签列表
        [SerializeField] private GameObject CanvasTabPrefab;          //页签
        [SerializeField] private Button AddCanvasBtn;
        [SerializeField] private Button DeleteCanvasBtn;
        [SerializeField] public Text ErrorText;                 //报错提示


        [Header("各式节点预制件")]
        [SerializeField] public GameObject TemplateMatchPre;    //模版匹配节点
        [SerializeField] public GameObject MouseNodePre;        //鼠标节点
        [SerializeField] public GameObject AssignNodePre;       //赋值节点
        [SerializeField] public GameObject MapNodePre;          //地图相关节点

        [NonSerialized] public CanvasConfig CanvasCfg;
        [NonSerialized] public string MouseHoverId;                         // 鼠标悬浮的节点
        [NonSerialized] public bool HoldCtrl;                               // 长按Ctrl键状态
        [NonSerialized] public string _id;                                  // 脚本id
        [NonSerialized] public AutoScriptData _scriptData;                  // 脚本运行时数据
        [NonSerialized] public int _canvas_index;                           // 当前画布

        List<ScriptCanvasConfig> _canvas_list;
        float CanvasTabPrefab_height;
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

        private MouseSelectInfo _currentSelect;       //当前选中元素
        private MouseSelectInfo _copySelect;          //复制的元素


        // 存八个插槽.方位位置
        public Dictionary<NodeType, Vector2[]> nodeUI_slotPos_cfg;
        // 存节点默认使用插槽情况。<类型, <type,槽序>>
        Dictionary<NodeType, int[]> nodeUI_doorToSlot_default;
        // 存节点使用插槽情况。<拼接id, 槽序>  
        Dictionary<string, int> _nodeUI_doorToSlot_result;


        void Awake()
        {
            TitleEditBtn.onClick.AddListener(OnClickTitleEditBtn);
            AddCanvasBtn.onClick.AddListener(OnClickAddCanvasBtn);
            DeleteCanvasBtn.onClick.AddListener(OnClickDeleteCanvasBtn);
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

            CanvasTabPrefab.SetActive(false);
            CanvasTabListComp.OnGetItemSize = GetItemSize;
            CanvasTabListComp.OnGetItemTemplate = (int index) => CanvasTabPrefab;
            CanvasTabListComp.OnUpdateItem = UpdateItem;

            CanvasTabPrefab_height = CanvasTabPrefab.GetComponent<RectTransform>().rect.height;
        }

        public override void SetData(object data)
        {
            _useScaleAnim = false;

            _id = data as string;
            LastOpenId = _id;
            _scriptData = Manager.GetScriptData(_id);
            _canvas_list = _scriptData.Config.Canvas;
            _canvas_index = 0;
            _lastSaveTime = DateTime.Now;
            Init();
        }

        void Init()
        {
            InitSlotPosConfig();

            CanvasCfg = new CanvasConfig(4000, 4000);
            Map.SetData(this, 300);  //绘制栅格
            BtnBar.SetData(this);
            Title.text = $"{_scriptData.Config.Name}";
            IdText.text = $"id: {_scriptData.Config.Id}";

            RefreshCanvasTab();

            Clear();
            //生成流程的节点
            OnScrolling(ScrollRect.normalizedPosition);
            RefreshError();
        }

        void RefreshCanvasTab()
        {
            var canvas_list = _scriptData.Config.Canvas;
            CanvasTabListComp.ReloadData(canvas_list.Count, false);
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
            _nodeUI_doorToSlot_result.Clear();
        }

        void OnClickTitleEditBtn()
        {
            Action<string, string> OnConfirm = (string name, string _) =>
            {
                Manager.RenameScript(_id, name);
                Title.text = $"绘制面板：{_scriptData.Config.Name}";
            };

            ConfirmPanelParam param = new ConfirmPanelParam
            {
                Type = ConfirmPanelType.EditInput,
                PanelTitle = SU.GetString(SU.ChongMinMingJiaoBen),
                Region0Title = SU.GetString(SU.JiaoBenMing),
                Region0Text = _scriptData.Config.Name,
                OnConfirm = OnConfirm,
            };

            PanelRunConfig config = new PanelRunConfig
            {
                SetPosType = PanelSetPosType.Reference,
                PosTarget = Title.rectTransform,
                PosOffset = new Vector2(0, -30),
            };
            UIManager.Inst.ShowPanel(PanelEnum.ConfirmPanel, param, config);
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
                Manager.SaveScript(_id);
            }

            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
            {
                var type = _currentSelect.Type;
                var select_id = _currentSelect.SelectId;
                if (type == MouseSelectType.Node)
                {
                    // 删除节点
                    DeleteNode(select_id);
                    SelectUI(MouseSelectType.Undefined, null);
                }
                else if (_currentSelect.Type == MouseSelectType.Line)
                {
                    // 删除线段
                    int sep = select_id.IndexOf('-');                   //separator
                    string from = select_id.Substring(0, sep);      // "from"id
                    string to = select_id.Substring(sep + 1);    // "to"id

                    DeleteLine(from, to);
                    SelectUI(MouseSelectType.Undefined, null);
                }
            }

            // 按着Ctrl键，点击节点出引用
            HoldCtrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
            {
                _copySelect = _currentSelect;
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                if (_copySelect.Type == MouseSelectType.Node)
                    PasteNode(_copySelect.SelectId);
            }

        }

        public void SelectUI(MouseSelectType type, string id)
        {
            var last = _currentSelect;
            _currentSelect = new MouseSelectInfo() { Type = type, SelectId = id };
            RefreshUISelectedStatus(last);
            RefreshUISelectedStatus(_currentSelect);
            OnMouseSelected?.Invoke();
        }

        public bool IsSelect(MouseSelectType type, string id)
        {
            return _currentSelect.Type == type && _currentSelect.SelectId == id;
        }

        #endregion
        #region 节点
        // 只显示在视窗内的节点 (ok)
        // 完全同步节点(id对应的方案)，完全同步线段
        public void SyncData()
        {
            var canvas_id = _scriptData.Config.Canvas[_canvas_index].Id;
            int frameTime = Time.frameCount;
            HashSet<string> node_ids = Map.GetInViewNodeIds(canvas_id);
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
                var line_ids = Manager.GetLinesByNode(_scriptData, id);

                // 增节点
                if (!_nodeUIMap.ContainsKey(id))
                {
                    var data = _scriptData.NodeDatas[id];
                    var type = data.NodeType;
                    var nodeUI = GetFromPool(type);

                    var parent = IsSelect(MouseSelectType.Node, id) ? TopLayer : NodeParent;
                    nodeUI.transform.SetParent(parent, false);
                    _nodeUIMap[id] = nodeUI;
                    nodeUI.SetData(data, this);
                    RefreshNodeSlot(id);

                    lines_need_refresh_ids.AddRange(line_ids);
                }

                // 统计所有的线段
                lines_ids.AddRange(line_ids);
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
                    ui.DrawLine(frameTime);
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
                    ui.DrawLine(frameTime);
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
            var canvas_data = _canvas_list[_canvas_index];
            var data = Manager.CreateNode(_scriptData, canvas_data.Id, pos, NodeType.TemplateMatchOper);
            SyncData();

            // 确保新节点在最上层
            if (_nodeUIMap.TryGetValue(data.Id, out var new_ui))
                new_ui.transform.SetAsLastSibling();
        }
        // 复制粘贴功能
        public void PasteNode(string id)
        {
            var pos = Map.MapConvert(Map.GetMousePos());
            var canvas_data = _canvas_list[_canvas_index];
            var data = Manager.CopyNode(_scriptData, canvas_data.Id, pos, id);
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
            Manager.DeleteNode(_scriptData, id);
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


                case NodeType.CaptureOper:
                case NodeType.AssignOper:
                case NodeType.ConditionOper:
                case NodeType.ForOper:
                case NodeType.TriggerEvent:
                case NodeType.ListenEvent:
                    pool = _assignNodeUIPool;
                    prefab = AssignNodePre;
                    break;
                case NodeType.MapCapture:
                case NodeType.MapPathFinding:
                case NodeType.ItemGridRecog:
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
        public void RefreshUISelectedStatus(MouseSelectInfo info)
        {
            int frameTime = Time.frameCount;
            var id = info.SelectId;
            if (info.Type == MouseSelectType.Node)
            {
                if (_nodeUIMap.TryGetValue(id, out var ui))
                {
                    ui.RefreshSelected();
                    RefreshLineByNode(id);
                }
            }

            if (info.Type == MouseSelectType.Line)
            {
                if (_lineUIMap.TryGetValue(id, out var line))
                {
                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.DrawLine(frameTime);
                }
            }
        }

        #endregion

        #region 线段


        // 按id列表刷新线段
        public void RefreshLineByNode(string node_id)
        {
            var line_ids = Manager.GetLinesByNode(_scriptData, node_id);
            int frameTime = Time.frameCount;

            foreach (var id in line_ids)
            {
                if (_lineUIMap.TryGetValue(id, out GameObject line))
                {
                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.DrawLine(frameTime);
                }
            }
        }

        // 增线段
        public void AddLine(AutoScriptData scriptData, string from_id, string to_id,
                            NodeDoor from_door, NodeDoor to_door)
        {
            Manager.AddLine(scriptData, from_id, to_id, from_door, to_door);
            RefreshNodeSlot(from_id);
            RefreshNodeSlot(to_id);
            RefreshLineByNode(from_id);
            RefreshLineByNode(to_id);
            SyncData();
        }
        // 删线段
        public void DeleteLine(string from_id, string to_id)
        {
            Manager.DeleteLine(_scriptData, from_id, to_id);
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
        void InitSlotPosConfig()
        {
            if (nodeUI_slotPos_cfg != null) return;
            nodeUI_slotPos_cfg = new Dictionary<NodeType, Vector2[]>();
            _nodeUI_doorToSlot_result = new Dictionary<string, int>();

            foreach (var typeData in AutoDataUIConfig.NodeTypes)
            {
                var type = typeData.Item1;
                Vector2[] l = null;
                GetPool(type, out var _, out var go);
                var ui = go.GetComponent<ProcessNodeUI>();
                var rectT = go.GetComponent<RectTransform>();
                if (type == NodeType.TemplateMatchOper)
                {
                    l = new Vector2[3];
                    l[0] = Utils.GetRelativePosToParent(ui.InflowNode);
                    l[1] = Utils.GetRelativePosToParent(ui.TrueOutNode);
                    l[2] = Utils.GetRelativePosToParent(ui.FalseOutNode);
                }
                else
                {
                    bool isCircle = type == NodeType.MouseOper || type == NodeType.KeyBoardOper;
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
                nodeUI_slotPos_cfg[type] = l;
            }

            // 4个门的默认槽位。-1代表Undefined，0代表正下方的槽
            nodeUI_doorToSlot_default = new Dictionary<NodeType, int[]>()
            {
                { NodeType.TemplateMatchOper, new int[]{0,-1,1,2} },

                { NodeType.MouseOper, new int[]{2,-1,6,0} },         // 以下4个一致
                { NodeType.KeyBoardOper, new int[]{2,-1,6,0} },
                { NodeType.WaitOper, new int[]{2,-1,6,0} },
                { NodeType.StopScript, new int[]{2,-1,6,0} },

                { NodeType.CaptureOper, new int[]{2,-1,6,0} },
                { NodeType.AssignOper, new int[]{2,-1,6,0} },
                { NodeType.ConditionOper, new int[]{2,-1,5,7} },
                { NodeType.ForOper, new int[]{3,7,1,5} },
                { NodeType.TriggerEvent, new int[]{2,-1,6,0} },
                { NodeType.ListenEvent, new int[]{2,-1,6,0}},
                { NodeType.MapCapture, new int[]{2,-1,6,0}},
                { NodeType.MapPathFinding, new int[]{2,-1,5,7}},
                { NodeType.ItemGridRecog, new int[]{2,-1,6,0}},

            };


        }

        /// <summary>
        /// type: 0-in, 1-out_true, 2-out_false  ,画布(0,0)为原点下的坐标。
        /// 每个节点要在_nodeUISlotCache 初始化
        /// </summary>
        public Vector2 GetLineEndPos(BaseNodeData nData, NodeDoor door)
        {
            Vector2 offset = default;
            int door_int = (int)door;
            var id = $"{nData.Index}-{door_int}";
            var pos_cfg = nodeUI_slotPos_cfg[nData.NodeType];
            // try
            // {

            // 没有的就用默认的
            if (_nodeUI_doorToSlot_result.TryGetValue(id, out int slot_i))
            {
                offset = pos_cfg[slot_i];
            }
            else
            {
                slot_i = nodeUI_doorToSlot_default[nData.NodeType][door_int];
                offset = pos_cfg[slot_i];
            }
            // }
            // catch (Exception)
            // {
            //     var a = 1;
            // }

            return Map.MapConvert(nData.Pos) + offset;
        }

        #region Slots


        bool[] _slot_used;      // 插槽是否被占用
        List<int> _slot_front = new List<int>();
        List<int> _slot_back = new List<int>();

        // 这里是，先排除手动选的，再给出入口自动选插槽 
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

            var offset_list = nodeUI_slotPos_cfg[self.NodeType];
            var self_pos = Map.MapConvert(self.Pos);
            var default_list = nodeUI_doorToSlot_default[self.NodeType];

            GetLineEndOwnership(self.NodeType, out bool has_in, out bool has_in1,
                                out bool has_out_true, out bool has_out_false);
            bool[] has_doors = new bool[4] { has_in, has_in1, has_out_true, has_out_false };
            // 手动 > 自动就近排序 > 默认值(无任何连接时)

            // 4个口需要从8个槽位中挑选位置。-1表示待定
            int[] doors = new int[4] { -1, -1, -1, -1 };

            var fixed_caches = _scriptData.Config.Slots;
            if (fixed_caches.TryGetValue(self.IndexStr, out int[] fixed_cache))
                Array.Copy(fixed_cache, doors, doors.Length);

            _slot_used = new bool[8];
            for (int i = 0; i < 4; i++)
            {
                var slot_i = doors[i];
                if (slot_i > -1)
                    _slot_used[slot_i] = true;
            }

            // 有连线的出入口优先级更高。In > In1 > OutTrue > OutFalse 
            var door_lineCount = self.Door_LineCount;
            _slot_front.Clear();
            _slot_back.Clear();
            for (int i = 0; i < door_lineCount.Length; i++)
                if (has_doors[i])                   // 有没有这个门
                    if (door_lineCount[i] > 0)
                        _slot_front.Add(i);
                    else
                        _slot_back.Add(i);

            for (int i = 0; i < _slot_front.Count; i++)
            {
                int door = _slot_front[i];
                _nodeUI_doorToSlot_result[$"{self.IndexStr}-{door}"] = doors[door] > -1 ? doors[door] :
                    Method(self, door, true, _slot_used, default_list, self_pos, offset_list);
            }

            for (int i = 0; i < _slot_back.Count; i++)
            {
                int door = _slot_back[i];
                _nodeUI_doorToSlot_result[$"{self.IndexStr}-{door}"] = doors[door] > -1 ? doors[door] :
                    Method(self, door, false, _slot_used, default_list, self_pos, offset_list);
            }


            self_ui.RefreshSlotUI();

        }

        int Method(BaseNodeData self, int door, bool has_line, bool[] used, int[] default_list,
                    Vector2 self_pos, Vector2[] pos_list)
        {
            var slot_i = 0;
            Vector2 target_pos;

            if (has_line)
            {
                var list = self.GetLinksByDoor((NodeDoor)door);
                var link = list[0];
                BaseNodeData target = _scriptData.NodeDatas[link.OtherId];
                if (target.NodeType == NodeType.TemplateMatchOper)
                {
                    target_pos = GetLineEndPos(target, link.OtherDoor);
                    slot_i = GetMinDistanceSlot(self_pos, pos_list, used, target_pos, null);
                }
                else
                {
                    // 在两个槽偏移列表中，选最小距离的两个槽
                    target_pos = Map.MapConvert(target.Pos);
                    var target_offset_list = nodeUI_slotPos_cfg[target.NodeType];
                    slot_i = GetMinDistanceSlot(self_pos, pos_list, used, target_pos, target_offset_list);
                }
            }
            else
            {
                slot_i = default_list[door];
                // 如果默认槽也被占了，从默认槽往后推3个
                for (int i = 0; i < 3; i++)
                {
                    if (!used[slot_i])
                        break;
                    slot_i = (slot_i + 1) % 8;
                }
            }
            used[slot_i] = true;
            return slot_i;
        }


        // 获取最小距离的插槽, 对于长条形节点效果堪忧。
        int GetMinDistanceSlot(Vector2 self_pos, Vector2[] off_list, bool[] used, Vector2 target_pos, Vector2[] target_off_list)
        {
            float min_distance = float.MaxValue;
            int direction = 0;                          //结果
            var share_item = self_pos - target_pos;     //公共项

            if (target_off_list == null)
                for (int i = 0; i < 8; i++)
                {
                    if (used[i]) continue;
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
                    if (used[i]) continue;
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
        public void GetLineEndOwnership(NodeType type, out bool has_in, out bool has_second_in,
                                        out bool has_out_true, out bool has_out_false)
        {
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                case NodeType.ConditionOper:
                case NodeType.MapPathFinding:
                    has_in = true;
                    has_second_in = false;
                    has_out_true = true;
                    has_out_false = true;
                    break;
                case NodeType.ForOper:
                    has_in = true;
                    has_second_in = true;
                    has_out_true = true;
                    has_out_false = true;
                    break;
                case NodeType.CaptureOper:
                case NodeType.MouseOper:
                case NodeType.KeyBoardOper:
                case NodeType.AssignOper:
                case NodeType.MapCapture:
                case NodeType.ItemGridRecog:
                case NodeType.WaitOper:
                case NodeType.TriggerEvent:
                case NodeType.ListenEvent:
                case NodeType.StopScript:
                    has_in = true;
                    has_second_in = false;
                    has_out_true = true;
                    has_out_false = false;
                    break;

                // case NodeType.ListenEvent:
                //     in_has = false;
                //     out_true_has = true;
                //     out_false_has = false;
                //     break;
                default:
                    has_in = false;
                    has_second_in = false;
                    has_out_true = false;
                    has_out_false = false;
                    break;
            }
        }
        #endregion

        #region CanvasTabs

        Vector2 GetItemSize(int index)
        {
            var data = _canvas_list[index];
            if (data.UIWidth == 0 || data.Changed)
            {
                var text_width = SceneTool.Inst.GetTextPreferWidth(36, data.GetName());
                data.UIWidth = text_width / 2 + 11;
                data.Changed = false;
            }
            return new Vector2(data.UIWidth, CanvasTabPrefab_height);

        }

        void UpdateItem(GameObject item, int index)
        {
            var data = _canvas_list[index];

            var rectT = item.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(data.UIWidth, CanvasTabPrefab_height);   //高度

            bool is_selected = index == _canvas_index;
            var imageUI = item.transform.GetChild(0).GetComponent<Image>();
            var textUI = item.transform.GetChild(1).GetComponent<Text>();
            item.GetComponentInChildren<Text>().text = data.GetName();               //内容

            var white = Utils.ParseHtmlString("#fffcf9");
            imageUI.color = is_selected ? white : ProcessNodeUI.TextColor;
            textUI.color = is_selected ? white : ProcessNodeUI.TextColor;

            var btn = item.GetComponent<ExButton>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickCanvasTab(index));
            btn.onClickRight.RemoveAllListeners();
            btn.onClickRight.AddListener(() => OnClickRightCanvasTab(index, rectT));
        }

        public int GetCanvasIndex(string id)
        {
            var data = _scriptData.NodeDatas[id];
            var canvas_id = data.CanvasId;
            return _scriptData.Config.Canvas.FindIndex(c => c.Id == canvas_id);
        }

        public void OnClickCanvasTab(int index, bool reset = true)
        {
            _canvas_index = index;
            RefreshCanvasTab();
            if (reset)
                Map.ScrollTo(new Vector2(0, 1));
            SyncData();
        }

        void OnClickAddCanvasBtn()
        {
            _canvas_index = _scriptData.AddCanvas();
            RefreshCanvasTab();
            Map.ScrollTo(new Vector2(0, 1));
            SyncData();
        }
        void OnClickDeleteCanvasBtn()
        {
            if (!_scriptData.IsEnd)         // 需要终止脚本，才能编辑
                return;

            if (_canvas_list.Count < 1)     // 至少留一个
                return;

            // 弹个确认弹窗
            Action<string, string> OnConfirm = (string _, string _) =>
            {
                _scriptData.DeleteCanvas(_canvas_index);

                // _canvas_index不变。如果超引用范围则-1
                if (_canvas_index > _canvas_list.Count - 1)
                    _canvas_index = _canvas_list.Count - 1;

                RefreshCanvasTab();
                Map.ScrollTo(new Vector2(0, 1));
                SyncData();
            };

            ConfirmPanelParam param = new ConfirmPanelParam
            {
                Type = ConfirmPanelType.Confirm,
                PanelTitle = "提示",
                Content = "确定要删除此画布吗？（将删除此画布的全部节点）",
                OnConfirm = OnConfirm,
            };

            PanelRunConfig config = new PanelRunConfig
            {
                SetPosType = PanelSetPosType.Reference,
                PosTarget = DeleteCanvasBtn.GetComponent<RectTransform>(),
                PosOffset = new Vector2(0, -30),
            };
            UIManager.Inst.ShowPanel(PanelEnum.ConfirmPanel, param, config);

        }

        /// <summary>
        /// 画布右键菜单
        /// </summary>
        void OnClickRightCanvasTab(int tab_index, RectTransform selfR)
        {
            TipsComp.gameObject.SetActive(true);

            var options = new List<string>()
            { "复制","粘贴","重命名" };
            TipsComp.SetData(options, (option) => OnClickCanvasTabOption(option, tab_index, selfR));
            TipsComp.SetCurIndex(-1);

            // 设置位置
            var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
            tipsCompRectT.SetParent(Content, false);
            var pos = Utils.GetPos(tipsCompRectT, selfR, default);
            pos = pos + new Vector2(-selfR.rect.width / 2, -selfR.rect.height / 2 - 5);

            tipsCompRectT.anchoredPosition = pos;
        }

        /// <summary>
        /// 画布右键菜单执行
        /// </summary>
        void OnClickCanvasTabOption(int option, int tab_index, RectTransform selfR)
        {
            var canvas_data = _canvas_list[tab_index];
            if (option == 0)
            {
                Manager.CopyCanvas(_scriptData, canvas_data.Id);
            }
            else if (option == 1)
            {
                Manager.PasteCanvas(_scriptData, canvas_data.Id);
                SyncData();
            }
            else if (option == 2)
            {
                Action<string, string> OnConfirm = (string input1, string _) =>
                {
                    canvas_data.SetName(input1);
                    RefreshCanvasTab();
                };
                ConfirmPanelParam param = new ConfirmPanelParam
                {
                    Type = ConfirmPanelType.EditInput,
                    PanelTitle = "重命名",
                    Region0Title = "画布名",
                    Region0Text = canvas_data.GetName(),
                    OnConfirm = OnConfirm,
                };

                PanelRunConfig config = new PanelRunConfig
                {
                    SetPosType = PanelSetPosType.Reference,
                    PosTarget = selfR.GetComponent<RectTransform>(),
                    PosOffset = new Vector2(0, -30),
                };
                UIManager.Inst.ShowPanel(PanelEnum.ConfirmPanel, param, config);
            }
        }

        #endregion

        #region Others
        void OnDisable()
        {
            // 关闭页面清掉 手动slot缓存(并未连线的)
            foreach (var node in _scriptData.NodeDatas.Values)
                _scriptData.RefreshSlot(node);

            Save();
        }

        /// <summary>
        /// Init()需要调用Save()吗。。
        /// </summary>
        void Save()
        {
            if (_id != null)
                Manager.SaveScript(_id);
        }

        void LateUpdate()
        {
            var now = DateTime.Now;
            if ((now - _lastSaveTime).TotalSeconds >= 2 * 60)
            {
                Manager.SaveScript(_id);
                _lastSaveTime = now;
            }
        }

        public override void Close()
        {
            base.Close();
            Utils.OpenDeskPet();
        }


        public void RefreshError()
        {
            var error_count = _scriptData.ErrorCount;
            bool is_error = error_count > 0;
            ErrorText.transform.parent.gameObject.SetActive(is_error);
            if (is_error)
                ErrorText.text = error_count.ToString();
        }

        #endregion
    }
}