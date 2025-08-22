
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework.UI;
using Script.Model.Auto;
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
    /// todo：
    /// 1.节点实际功能
    /// </summary>
    public class DrawProcessPanel : BasePanel
    {
        [SerializeField] private Text Title;
        [SerializeField] private ScrollRect ScrollRect;
        [SerializeField] public RectTransform Canvas;
        [SerializeField] public SplitLineComp SplitLineComp;          //分段线组件
        [SerializeField] private GameObject LinePre; //线段预制件
        [SerializeField] public Transform NodeParent; //节点父物体
        [SerializeField] private Transform LineParent; //线段父物体
        [SerializeField] private DrawProcessBtnBar BtnBar;

        [Header("各式节点预制件")]
        [SerializeField] public GameObject CommonNodePre;   //普通节点
        [SerializeField] public GameObject MouseNodePre;    //鼠标节点
        [SerializeField] public GameObject KeyboardNodePre; //键盘节点
        [SerializeField] public GameObject AssignNodePre;   //赋值节点

        [NonSerialized] public CanvasConfig CanvasCfg;
        //当前选中的节点 格式：
        //节点id: "node-{index}"
        //线段id: "line-{from}-{to}"
        [NonSerialized] public string MouseSelectedId;
        [NonSerialized] public string MouseHoverId;       //悬浮的节点

        public AutoScriptManager manager => AutoScriptManager.Inst;
        Dictionary<string, BaseNodeData> _nodeDatas => AutoScriptManager.Inst._nodeDatas;
        SPool _commonNodeUIPool;
        SPool _mouseNodeUIPool;
        SPool _keyboardNodeUIPool;
        SPool _assignNodeUIPool;

        SPool _linePool;
        GameObject _tempLine; //拖拽时的临时线段

        //Dic<id,实体>  这样才是严谨、不错的存储
        Dictionary<string, ProcessNodeUI> _nodeUIMap = new Dictionary<string, ProcessNodeUI>();
        //Dic<id,实体> id = "line-{from}-{to}"，from和to线段的两端节点作为key,设计前提是两节点间最多一根线
        Dictionary<string, GameObject> _lineUIMap = new Dictionary<string, GameObject>();

        void Awake()
        {
            ScrollRect.onValueChanged.AddListener(OnScrolling);

            //初始化预制件
            LinePre.gameObject.SetActive(false);
            CommonNodePre.SetActive(false);
            MouseNodePre.SetActive(false);
            KeyboardNodePre.SetActive(false);
            AssignNodePre.SetActive(false);

            var trans_line = LinePre.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);

            _linePool = new SPool(LinePre, 0, "LineUI");
            _commonNodeUIPool = new SPool(CommonNodePre, 0, "C_NodeUI");
            _mouseNodeUIPool = new SPool(MouseNodePre, 0, "M_NodeUI");
            _keyboardNodeUIPool = new SPool(KeyboardNodePre, 0, "K_NodeUI");
            _assignNodeUIPool = new SPool(AssignNodePre, 0, "A_NodeUI");
        }

        public override void SetData(object data)
        {
            _useScaleAnim = false;
            Init();
        }

        void Init()
        {
            CanvasCfg = new CanvasConfig(4000, 4000);

            SplitLineComp.SetData(300);  //绘制栅格
            BtnBar.SetData(this);

            //生成流程的节点
            OnScrolling(ScrollRect.normalizedPosition);

            Title.text = $"绘制面板：{manager._autoData.Name}";
        }



        public Vector2 MapConvert(Vector2 pos)
        {
            return new Vector2(pos.x, CanvasCfg.H - pos.y);
        }

        void OnScrolling(Vector2 pos)
        {
            SyncData();
        }


        void Update()
        {
            //上方有界面就应该跳过
            BasePanel last = UISceneMixin.Inst.PeekPanel(PanelDefine.Layer) as BasePanel;
            if (last.PanelDefine.Key != PanelDefine.Key)
            {
                return;
            }



            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S))
            {
                manager.Save();
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
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
        }


        #region 节点
        // 只显示在视窗内的节点 (ok)
        // 完全同步节点(id对应的方案)，完全同步线段
        public void SyncData()
        {
            HashSet<string> node_ids = SplitLineComp.GetInViewNodeIds();
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
                    var data = _nodeDatas[id];
                    var type = data.GetNodeType();
                    var nodeUI = GetFromPool(type);
                    nodeUI.transform.SetParent(NodeParent, false);
                    _nodeUIMap[id] = nodeUI;
                    nodeUI.SetData(data, this);
                }

                // 统计所有的线段
                lines_ids.AddRange(manager.GetLinesByNode(id));
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
            var pos = SplitLineComp.MapConvert(SplitLineComp.GetCenter());
            var data = manager.CreateNode(NodeType.TemplateMatchOper, pos);
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
            manager.DeleteNode(id);
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
        public ProcessNodeUI GetFromPool(NodeType type)
        {
            GameObject go = null;
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                    go = _commonNodeUIPool.Pop();
                    break;
                case NodeType.MouseOper:
                    go = _mouseNodeUIPool.Pop();
                    break;
                case NodeType.KeyBoardOper:
                    go = _keyboardNodeUIPool.Pop();
                    break;
                case NodeType.AssignOper:
                    go = _assignNodeUIPool.Pop();
                    break;
            }
            return go.GetComponent<ProcessNodeUI>();
        }
        /// <summary>
        /// 把ui还给对象池 
        /// </summary>
        public void PushToPool(ProcessNodeUI nodeUI)
        {
            var type = nodeUI._data.GetNodeType();
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                    _commonNodeUIPool.Push(nodeUI.gameObject);
                    break;
                case NodeType.MouseOper:
                    _mouseNodeUIPool.Push(nodeUI.gameObject);
                    break;
                case NodeType.KeyBoardOper:
                    _keyboardNodeUIPool.Push(nodeUI.gameObject);
                    break;
                case NodeType.AssignOper:
                    _assignNodeUIPool.Push(nodeUI.gameObject);
                    break;
            }
        }
        /// <summary>
        /// 拿预制件
        /// </summary>
        public GameObject GetPrefab(NodeType type)
        {
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                    return CommonNodePre;
                case NodeType.MouseOper:
                    return MouseNodePre;
                case NodeType.KeyBoardOper:
                    return KeyboardNodePre;
                case NodeType.AssignOper:
                    return AssignNodePre;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
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

            var line_ids = manager.GetLinesByNode(node_id);

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
            manager.DeleteLine(from_id, to_id);
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

        #endregion
    }
}