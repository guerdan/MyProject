
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
        [SerializeField] public GameObject TemplateMatchPre;   //模版匹配节点
        [SerializeField] public GameObject MouseNodePre;    //鼠标节点
        [SerializeField] public GameObject KeyboardNodePre; //键盘节点
        [SerializeField] public GameObject AssignNodePre;   //赋值节点
        [SerializeField] public GameObject MapCaptureNodePre;   //赋值节点

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
        SPool _keyboardNodeUIPool;
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
        Dictionary<NodeType, Vector2[]> _nodeUISlotPos;
        // 存节点默认使用插槽情况。<类型, <type,槽序>>
        Dictionary<NodeType, int[]> _nodeUISlotDefault;
        // 存节点使用插槽情况。<拼接id, 槽序>
        Dictionary<string, int> _nodeUISlotCache;


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
            MapCaptureNodePre.SetActive(false);

            var trans_line = LinePre.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);

            _linePool = new SPool(LinePre, 0, "LineUI");
            _templateMatchNodeUIPool = new SPool(TemplateMatchPre, 0, "T_NodeUI");
            _mouseNodeUIPool = new SPool(MouseNodePre, 0, "M_NodeUI");
            _keyboardNodeUIPool = new SPool(KeyboardNodePre, 0, "K_NodeUI");
            _assignNodeUIPool = new SPool(AssignNodePre, 0, "A_NodeUI");
            _mapCaptureNodeUIPool = new SPool(MapCaptureNodePre, 0, "Map_NodeUI");
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
            _nodeUISlotCache.Clear();
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
                    nodeUI.transform.SetParent(NodeParent, false);
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
                case NodeType.StopScript:
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
                case NodeType.MapCapture:
                    pool = _mapCaptureNodeUIPool;
                    prefab = MapCaptureNodePre;
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
            if (_nodeUISlotPos != null) return;
            _nodeUISlotPos = new Dictionary<NodeType, Vector2[]>();
            _nodeUISlotCache = new Dictionary<string, int>();

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

                        l = new Vector2[8] {
                            new Vector2(0, -h_half), new Vector2(-sqr_w, -sqr_h), new Vector2(-w_half, 0), new Vector2(-sqr_w, sqr_h),
                            new Vector2(0, h_half), new Vector2(sqr_w, sqr_h), new Vector2(w_half, 0), new Vector2(sqr_w, -sqr_h)
                        };
                    }
                }
                _nodeUISlotPos[t] = l;
            }

            _nodeUISlotDefault = new Dictionary<NodeType, int[]>()
            {
                { NodeType.TemplateMatchOper, new int[]{0,1,2} },
                { NodeType.MouseOper, new int[]{2,6} },
                { NodeType.StopScript, new int[]{2,6} },
                { NodeType.KeyBoardOper, new int[]{2,6} },
                { NodeType.AssignOper, new int[]{2,6} },
                { NodeType.ConditionOper, new int[]{2,5,7} },
                { NodeType.TriggerEvent, new int[]{2,6} },
                { NodeType.ListenEvent, new int[]{2,6}},
                { NodeType.MapCapture, new int[]{2,6}},
            };


        }

        // type: 0-in, 1-out_true, 2-out_false  ,画布(0,0)为原点下的坐标
        // 每个节点要在_nodeUISlotCache 初始化
        public Vector2 GetLineEndPos(BaseNodeData nData, int type)
        {
            Vector2 offset = default;
            var id = nData.Id + "-" + type;
            var pos_list = _nodeUISlotPos[nData.NodeType];

            // 没有的就用默认的
            if (_nodeUISlotCache.TryGetValue(id, out int slot_i))
            {
                offset = pos_list[slot_i];
            }
            else
            {
                slot_i = _nodeUISlotDefault[nData.NodeType][type];
                offset = pos_list[slot_i];
            }

            return Map.MapConvert(nData.Pos) + offset;
        }

        public void RefreshNodeSlot(string node_id)
        {
            var self_ui = GetNode(node_id);
            if (self_ui == null)
                return;

            BaseNodeData self = _scriptData.NodeDatas[node_id];
            // 模版匹配节点用固定的位置
            if (self.NodeType == NodeType.TemplateMatchOper)
                return;

            int type = 0;
            int direction = 0;  // 0-下，转一圈
            var pos_list = new List<Vector2>(_nodeUISlotPos[self.NodeType]);
            var self_pos = Map.MapConvert(self.Pos);
            Vector2 target_pos = default;
            var default_list = _nodeUISlotDefault[self.NodeType];

            GetLineEndOwnership(self.NodeType, out bool _, out bool _, out bool out_false_has);

            if (!_nodeUISlotCache.TryGetValue(self.Id + "-0", out int value_0))
                value_0 = default_list[0];
            if (!_nodeUISlotCache.TryGetValue(self.Id + "-1", out int value_1))
                value_1 = default_list[1];

            int value_2 = 0;
            if (out_false_has && !_nodeUISlotCache.TryGetValue(self.Id + "-2", out value_2))
                value_2 = default_list[2];

            HashSet<int> used = new HashSet<int>();

            if (self.LastNode.Count > 0)
            {
                type = 0;
                used.Clear();
                used.Add(value_1);
                if (out_false_has) used.Add(value_2);
                BaseNodeData target = _scriptData.NodeDatas[self.LastNode[0]];
                if (target.NodeType == NodeType.TemplateMatchOper)
                    target_pos = target.TrueNextNodes.Contains(node_id) ? GetLineEndPos(target, 1) : GetLineEndPos(target, 2);
                else
                    target_pos = Map.MapConvert(target.Pos);
                direction = GetMinDistanceSlot(self_pos, target_pos, pos_list, used);
                _nodeUISlotCache[self.Id + "-" + type] = direction;
                value_0 = direction;
            }

            if (self.TrueNextNodes.Count > 0)
            {
                type = 1;
                used.Clear();
                used.Add(value_0);
                if (out_false_has) used.Add(value_2);

                used.Remove(default_list[1]);
                BaseNodeData target = _scriptData.NodeDatas[self.TrueNextNodes[0]];
                if (target.NodeType == NodeType.TemplateMatchOper)
                    target_pos = GetLineEndPos(target, 0);
                else
                    target_pos = Map.MapConvert(target.Pos);
                direction = GetMinDistanceSlot(self_pos, target_pos, pos_list, used);
                _nodeUISlotCache[self.Id + "-" + type] = direction;
                used.Add(direction);
                value_1 = direction;

            }

            if (self.FalseNextNodes.Count > 0)
            {
                type = 2;
                used.Clear();
                used.Add(value_0);
                used.Add(value_1);

                if (default_list.Count() > 2)
                    used.Remove(default_list[2]);
                BaseNodeData target = _scriptData.NodeDatas[self.FalseNextNodes[0]];
                if (target.NodeType == NodeType.TemplateMatchOper)
                    target_pos = GetLineEndPos(target, 0);
                else
                    target_pos = Map.MapConvert(target.Pos);
                direction = GetMinDistanceSlot(self_pos, target_pos, pos_list, used);
                _nodeUISlotCache[self.Id + "-" + type] = direction;
                used.Add(direction);
                value_2 = direction;

            }

            self_ui.RefreshSlotUI();
        }


        // 获取最小距离的插槽
        int GetMinDistanceSlot(Vector2 self_pos, Vector2 target_pos, List<Vector2> pos_list, HashSet<int> used)
        {
            float min_distance = float.MaxValue;
            int direction = 0;  // 0-下，转一圈
            for (int i = 0; i < 8; i++)
            {
                if (used.Contains(i)) continue;
                var t = (self_pos + pos_list[i] - target_pos).SqrMagnitude();
                if (t < min_distance)
                {
                    min_distance = t;
                    direction = i;
                }
            }

            return direction;
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
                case NodeType.MapCapture:
                case NodeType.StopScript:
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