
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Util;
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
    /// 2.按钮栏（运行、暂停、新建节点），连线功能，删节点，删线
    /// 节点ui维护机制：用户增/删操作 => 通知逻辑并直接增/删节点ui => 刷节点而删线段
    /// 线段ui维护机制：用户增/删操作 => 通知逻辑并直接增/删线段ui
    /// todo：
    /// 1.NodeUI在视窗内才显示
    /// 2.节点实际功能
    /// 3.为了 "视窗内的线段才显示"需求，线段的生成要交给DrawProcessPanel了。
    /// 统计视窗内的节点，统计线段。修改刷新。只有节点变化才会有线段变化。
    /// 线段ui维护机制：用户增/删操作 => 通知逻辑并直接增/删线段ui
    /// </summary>
    public class DrawProcessPanel : BasePanel
    {
        [SerializeField] private Text Title;
        [SerializeField] private ScrollRect ScrollRect;
        [SerializeField] private RectTransform Canvas;
        [SerializeField] public SplitLineComp SplitLineComp;          //分段线组件
        [SerializeField] private GameObject NodePre; //节点预制件
        [SerializeField] private GameObject LinePre;
        [SerializeField] private DrawProcessBtnBar BtnBar;


        [NonSerialized] public CanvasConfig CanvasCfg;
        //当前选中的节点 格式：
        //节点id: "node-{index}"
        //线段id: "line-{from}-{to}"
        [NonSerialized] public string MouseSelectedId;
        [NonSerialized] public string MouseHoverId;       //悬浮的节点

        Dictionary<string, BaseNodeData> _nodeData => ProcessNodeManager.Inst._nodeData;
        SPool _nodeUIPool;
        SPool _linePool;
        GameObject _tempLine; //拖拽时的临时线段

        //Dic<id,实体>  这样才是严谨、不错的存储
        Dictionary<string, ProcessNodeUI> _nodeUIMap = new Dictionary<string, ProcessNodeUI>();
        //Dic<from,<to,实体>>  from和to线段的两端节点作为key,设计前提是两节点间最多一根线
        Dictionary<string, Dictionary<string, GameObject>> _lineUIMap = new Dictionary<string, Dictionary<string, GameObject>>();

        void Awake()
        {
            //初始化预制件
            NodePre.SetActive(false);
            LinePre.gameObject.SetActive(false);

            var trans_line = LinePre.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);

            _nodeUIPool = new SPool(NodePre);
            _linePool = new SPool(LinePre);
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
            foreach (var id in _nodeData.Keys)
            {
                //应该计算位置，视窗内生成
                var ui = GetNode(id);
                ui.SetData(_nodeData[id], this);
            }

            //线的生成权交给NodeUI。
            foreach (var node in _nodeUIMap.Values)
            {
                node.DrawLine();
            }

            Title.text = $"绘制面板：{ProcessNodeManager.Inst._autoData.Name}";
        }



        public Vector2 MapConvert(Vector2 pos)
        {
            return new Vector2(pos.x, CanvasCfg.H - pos.y);
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S))
            {
                ProcessNodeManager.Inst.Save();
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (MouseSelectedId != null)
                {
                    if (MouseSelectedId.StartsWith(BaseNodeData.IdFromat))
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

        // 获取节点
        public ProcessNodeUI GetNode(string id)
        {
            if (!_nodeUIMap.TryGetValue(id, out var ui))
            {
                var go = _nodeUIPool.Pop();
                go.transform.SetParent(Canvas, false);
                ui = go.GetComponent<ProcessNodeUI>();
                _nodeUIMap[id] = ui;
            }

            return ui;
        }
        // 创建节点功能 -- 创建节点
        public void CreateNode()
        {
            var pos = SplitLineComp.MapConvert(SplitLineComp.GetCenter());
            var node = ProcessNodeManager.Inst.CreateNode(pos);

            var ui = GetNode(node.Id);
            ui.SetData(node, this);
        }
        // 删除节点
        public void DeleteNode(string id)
        {
            if (id == null) return;
            if (!_nodeUIMap.ContainsKey(id)) return;
            // 逻辑先更新，ui后更新
            var data = ProcessNodeManager.Inst.GetNode(id);
            var last_list = data.LastNode.ToList();
            ProcessNodeManager.Inst.DeleteNode(id);

            var ui = _nodeUIMap[id];
            ui.DrawLine();
            foreach (var last_id in last_list)
            {
                var n = GetNode(last_id);
                n.DrawLine();
            }

            _nodeUIPool.Push(ui.gameObject);
            _nodeUIMap.Remove(id);
        }

        // 刷新选中状态
        public void RefreshUISelectedStatus(string id)
        {
            if (id == null) return;

            if (id.StartsWith(BaseNodeData.IdFromat))
            {
                if (_nodeUIMap.TryGetValue(id, out var ui))
                {
                    ui.DrawLineSelfAndLast();
                }
            }

            if (id.StartsWith("line-"))
            {
                var s = id.Substring(5);
                int first = s.IndexOf('-');
                int second = s.IndexOf('-', first + 1);
                string from = s.Substring(0, second);      // from-id
                string to = s.Substring(second + 1);    // to-id

                var ui = GetLine(from, to).GetComponent<ProcessNodeLineUI>();
                ui.RefreshForSelected();
            }

        }

        #endregion

        #region 线段



        // 按id列表刷新线段
        public void RefreshLine(string from_id, List<string> to_ids, int trueCount)
        {
            if (!_lineUIMap.TryGetValue(from_id, out var to_map))
            {
                to_map = new Dictionary<string, GameObject>();
                _lineUIMap[from_id] = to_map;
            }
            // 删
            foreach (var to_id in to_map.Keys.ToList())
            {
                if (!to_ids.Contains(to_id))
                {
                    var line = to_map[to_id];
                    _linePool.Push(line);
                    to_map.Remove(to_id);
                }
            }
            // 增
            for (int i = 0; i < to_ids.Count; i++)
            {
                string to_id = to_ids[i];
                if (!to_map.TryGetValue(to_id, out var line))
                {
                    line = _linePool.Pop();
                    line.transform.SetParent(Canvas, false);
                    to_map[to_id] = line;

                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ui.SetData(from_id, to_id, i < trueCount, this);
                }
            }
        }


        // 获取线段
        public GameObject GetLine(string from_id, string to_id)
        {
            return _lineUIMap[from_id][to_id];
        }

        public void DeleteLine(string from_id, string to_id)
        {
            if (_lineUIMap.TryGetValue(from_id, out var to_map))
            {
                if (to_map.TryGetValue(to_id, out var line))
                {
                    _linePool.Push(line);
                    to_map.Remove(to_id);
                    var ui = line.GetComponent<ProcessNodeLineUI>();
                    ProcessNodeManager.Inst.DeleteLine(from_id, to_id, ui._isTrue);
                }
            }

        }


        // 编辑-连线功能 -- 获取临时线段
        public void ShowLineForDrag(Vector2 from, Vector2 to, Color color)
        {
            if (_tempLine == null)
            {
                _tempLine = _linePool.Pop();
                _tempLine.transform.SetParent(Canvas, false);
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