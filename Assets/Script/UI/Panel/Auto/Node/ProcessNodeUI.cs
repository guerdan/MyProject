
using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Script.Util;
using Script.Framework.UI;
using System;
using System.Collections.Generic;
using Script.UI.Components;

namespace Script.UI.Panel.Auto.Node
{
    public enum NodeUI_DragType
    {
        None,           // 无
        DragCard,       // 拖拽卡片
        DrawLine,       // 画线
        DragCircle,     // 拖拽圈圈
    }

    public class ProcessNodeUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public static readonly Color BrownColor;
        public static readonly Color RedColor;
        public static readonly Color GreenColor;
        public static readonly Color WhiteColor;
        public static readonly Color RedBgColor;
        public static readonly Color TextColor;

        static ProcessNodeUI()
        {
            BrownColor = Utils.ParseHtmlString("#4B2C21");
            RedColor = Utils.ParseHtmlString("#B2382D");
            GreenColor = Utils.ParseHtmlString("#4C7543");
            WhiteColor = Utils.ParseHtmlString("#FAF8F4");
            RedBgColor = Utils.ParseHtmlString("#EFEDE9");
            TextColor = Utils.ParseHtmlString("#52344C");
        }

        [Header("通用")]
        [SerializeField] private ScaleArtComp ScaleNode;
        [SerializeField] public RectTransform InflowNode;
        // [SerializeField] private RectTransform EventNode;     //决定不要了,拆离出来单独做个事件接收节点
        [SerializeField] public RectTransform TrueOutNode;      // 上圈圈，有T字显示
        [SerializeField] public RectTransform FalseOutNode;     // 下圈圈，有F字显示
        [SerializeField] public RectTransform SecondInNode;     // 第二个In口
        [SerializeField] private Image OutlineF;      //前
        [SerializeField] private Image OutlineB;      //后。常规色
        [SerializeField] private GameObject IsFirstMark;      //起点标记
        [SerializeField] protected Text DelayText;


        public RectTransform selfR => (RectTransform)transform;
        public AutoScriptManager manager => AutoScriptManager.Inst;

        [HideInInspector] public BaseNodeData _data;
        [HideInInspector] public string _id;
        [HideInInspector] public NodeType _type;
        protected DrawProcessPanel _panel;
        AutoScriptData _scriptData;

        bool _last_selected = false;

        RectTransform TrueCircleTextR;
        RectTransform FalseCircleTextR;
        RectTransform InCircleTextR;
        RectTransform SecondInCircleTextR;

        protected virtual void Awake()
        {
            selfR.anchorMin = new Vector2(0, 0);
            selfR.anchorMax = new Vector2(0, 0);
            // 初始化组件。子层级会阻拦事件传入父层级
            ScaleNode.SetData(OnPointerEnter, OnPointerExit);

            if (TrueOutNode.childCount > 0)
                TrueCircleTextR = (RectTransform)TrueOutNode.GetComponentInChildren<Text>().transform;
            if (FalseOutNode && FalseOutNode.childCount > 0)
                FalseCircleTextR = (RectTransform)FalseOutNode.GetComponentInChildren<Text>().transform;
            if (InflowNode.childCount > 0)
                InCircleTextR = (RectTransform)InflowNode.GetComponentInChildren<Text>().transform;
            if (SecondInNode && SecondInNode.childCount > 0)
                SecondInCircleTextR = (RectTransform)SecondInNode.GetComponentInChildren<Text>().transform;
        }
        protected virtual void OnEnable()
        {
            AutoScriptManager.Inst.OnTick += Tick;
            AutoScriptManager.Inst.OnChangeNodeStatus += StatusChange;
        }

        protected virtual void OnDisable()
        {
            AutoScriptManager.Inst.OnTick -= Tick;
            AutoScriptManager.Inst.OnChangeNodeStatus -= StatusChange;

        }

        public void SetData(BaseNodeData nodeData, DrawProcessPanel panel)
        {
            _data = nodeData;
            _id = _data.Id;
            _panel = panel;
            _type = _data.NodeType;
            _scriptData = panel._scriptData;
            RefreshContent();
            Refresh();
            RefreshSelected();
            SetPos();
        }


        /// <summary>
        /// 刷新内容
        /// </summary>
        public virtual void RefreshContent()
        {

            if (DelayText) DelayText.text = $"{Math.Round(_data.Delay, 2)}s";
        }

        /// <summary>
        /// 每帧刷新的
        /// </summary>
        public void Refresh()
        {
            bool selected = _panel.IsSelect(MouseSelectType.Node, _id);
            // bool show_progress = _type != NodeType.AssignOper && _type != NodeType.ListenEvent;
            // 这个样式空闲时要框
            bool type1 = _type == NodeType.TemplateMatchOper;

            if (_data.Status == NodeStatus.Off)
            {
                if (type1)
                {
                    Utils.SetActive(OutlineF, false);
                    if (selected)
                        OutlineB.color = WhiteColor;
                    else
                        OutlineB.color = _data.ExcuteTimes > 0 ? BrownColor : GreenColor;
                }
                else
                {
                    Utils.SetActive(OutlineF, false);
                    Utils.SetActive(OutlineB, selected);
                    OutlineB.color = WhiteColor;
                }
            }
            else
            {
                Utils.SetActive(OutlineF, true);
                Utils.SetActive(OutlineB, true);
                OutlineF.color = RedColor;
                OutlineF.fillAmount = _data.Timer / _data.Delay;
                OutlineB.color = RedBgColor;
            }


            Utils.SetActive(IsFirstMark, _id == _scriptData.Config.FirstNode);
        }

        public virtual void RefreshSlotUI()
        {
            InflowNode.anchoredPosition = _panel.GetLineEndPos(_data, NodeDoor.In) - selfR.anchoredPosition;
            TrueOutNode.anchoredPosition = _panel.GetLineEndPos(_data, NodeDoor.OutTrue) - selfR.anchoredPosition;

            _panel.GetLineEndOwnership(_data.NodeType, out bool _, out bool has_second_in,
                                        out bool has_true, out bool has_false);
            if (has_false) FalseOutNode.anchoredPosition = _panel.GetLineEndPos(_data, NodeDoor.OutFalse) - selfR.anchoredPosition;
            if (has_second_in) SecondInNode.anchoredPosition = _panel.GetLineEndPos(_data, NodeDoor.In1) - selfR.anchoredPosition;

            // 计算圈圈旁文本的位置
            //  
            if (_data.NodeType == NodeType.ConditionOper || _data.NodeType == NodeType.ForOper
                || _data.NodeType == NodeType.MapPathFinding || _data.NodeType == NodeType.AssignOper)
            {
                var pos0 = _panel.GetLineEndPos(_data, NodeDoor.In) - selfR.anchoredPosition;
                InCircleTextR.anchoredPosition = GetTextPos(pos0, 1);
                var pos1 = _panel.GetLineEndPos(_data, NodeDoor.OutTrue) - selfR.anchoredPosition;
                TrueCircleTextR.anchoredPosition = GetTextPos(pos1);
                var pos2 = _panel.GetLineEndPos(_data, NodeDoor.OutFalse) - selfR.anchoredPosition;
                FalseCircleTextR.anchoredPosition = GetTextPos(pos2);
            }

            if (_data.NodeType == NodeType.ForOper)
            {
                var pos3 = _panel.GetLineEndPos(_data, NodeDoor.In1) - selfR.anchoredPosition;
                SecondInCircleTextR.anchoredPosition = GetTextPos(pos3);
            }
        }

        Vector2 GetTextPos(Vector2 source, int far_away = 0)
        {
            int x = 0;
            int y = 0;

            if (source.y == 0)
                y = 0;
            else if (source.y > 0)
                y = -6 + far_away / 2;
            else
                y = 6 - far_away / 2;

            if (source.x >= 0)
                x = -10 + far_away;
            else
                x = 10 - far_away;

            return new Vector2(x, y);
        }

        /// <summary>
        /// 刷新选中状态
        /// </summary>
        public virtual void RefreshSelected()
        {
            bool selected = _panel.IsSelect(MouseSelectType.Node, _id);
            _panel.GetLineEndOwnership(_data.NodeType, out bool _, out _,
                                        out bool has_true, out bool has_false);
            var checkBox_true = TrueOutNode.GetComponent<CheckBox>();
            var checkBox_false = FalseOutNode ? FalseOutNode.GetComponent<CheckBox>() : null;
            var checkBox_in = InflowNode ? InflowNode.GetComponent<CheckBox>() : null;


            if (_data.NodeType == NodeType.TemplateMatchOper || _data.NodeType == NodeType.ConditionOper
                || _data.NodeType == NodeType.MapPathFinding || _data.NodeType == NodeType.ForOper)
            {
                // 复杂圈圈，有T/F字样
                Utils.SetActive(TrueOutNode, true);
                Utils.SetActive(FalseOutNode, true);
                Utils.SetActive(SecondInNode, _data.NodeType == NodeType.ForOper);  // ForOper的唯一差别
                checkBox_true.SetData(selected);
                checkBox_false.SetData(selected);
                if (checkBox_in) checkBox_in.SetData(false);    // false代表 selected = false，显示文本
            }
            else
            {
                // 普通圈圈款式，无T/F字样
                Utils.SetActive(TrueOutNode, selected && has_true);
                Utils.SetActive(FalseOutNode, selected && has_false);
                Utils.SetActive(SecondInNode, false);
                if (checkBox_true) checkBox_true.SetData(true);
                if (checkBox_false) checkBox_false.SetData(true);
                if (checkBox_in) checkBox_in.SetData(_data.NodeType != NodeType.AssignOper);
            }


            SwitchParent(selected);
        }

        public void SwitchParent(bool selected)
        {
            if (!_last_selected && selected)
            {
                transform.SetParent(_panel.TopLayer, false);
            }
            else if (_last_selected && !selected)
            {
                transform.SetParent(_panel.NodeParent, false);
            }

            _last_selected = selected;
        }

        public virtual void Clear()
        {
            Utils.SetActive(TrueOutNode, false);
            Utils.SetActive(FalseOutNode, false);
        }


        /// <summary>
        /// 设置坐标
        /// </summary>
        public void SetPos()
        {
            var pos = _data.Pos;
            selfR.anchoredPosition = new Vector2(pos.x, _panel.CanvasCfg.H - pos.y);
        }


        // 每帧刷新
        void Tick()
        {
            Refresh();
        }
        // 任意节点状态流入或流出触发
        void StatusChange(string id)
        {
            // if (_id != id) return;
            RefreshSelected();
            _panel.RefreshLineByNode(_id);
        }


        #region 操作交互

        public struct NodeUI_DragInfo
        {
            public NodeUI_DragType Type;
            public NodeDoor FromDoor;
            public NodeDoor ToDoor;

            public NodeUI_DragInfo(NodeUI_DragType type, NodeDoor fromDoor)
            {
                Type = type;
                FromDoor = fromDoor;
                ToDoor = NodeDoor.In;
            }
        }

        private NodeUI_DragInfo _dragStatus = default;
        private bool _inDragging = false;
        private Vector2 _dragOffset;
        private bool _doubleClick = false;
        protected bool _finishOneClick = false;

        // 重置状态位
        void ClearOperation()
        {
            _dragStatus = default;
            _inDragging = false;
            _finishOneClick = false;
        }

        // PointerDown > BeginDrag > Drag > PointerUp > EndDrag
        // BeginDrag在用户PointerDown后的下一帧才触发，一帧的位移会被忽略
        public void OnPointerDown(PointerEventData eventData)
        {
            // DU.LogWarning("PointerDown");

            ClearOperation();

            var click_left = eventData.button == PointerEventData.InputButton.Left;
            var click_right = eventData.button == PointerEventData.InputButton.Right;

            _doubleClick = _panel.IsSelect(MouseSelectType.Node, _id);

            // 设置选中，节点变白框
            _panel.SelectUI(MouseSelectType.Node, _id);

            // 拖拽圈圈
            // 检测点击位置，是否在 圈圈区域 内，矩形30X30范围，用于拖拽连线
            {
                Vector2 clickPos;
                // 这个接口按pivot为原点，所以是画布(0,1)为原点下的坐标
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out clickPos
                );
                // 故加个画布Height
                clickPos += new Vector2(0, _panel.Map.ContentH);
                _panel.GetLineEndOwnership(_data.NodeType, out bool has_in, out bool has_second_in,
                                            out bool has_out_true, out bool has_out_false);

                bool circle_change_pos = _data.NodeType != NodeType.TemplateMatchOper;

                if (has_in && click_right && circle_change_pos)
                {
                    Vector2 inPos = _panel.GetLineEndPos(_data, NodeDoor.In);
                    bool inIntersect = Intersect(clickPos, inPos, 30, 30);
                    if (inIntersect)
                        _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DragCircle, NodeDoor.In);
                }

                if (has_second_in && click_right && circle_change_pos)
                {
                    Vector2 inPos = _panel.GetLineEndPos(_data, NodeDoor.In1);
                    bool inIntersect = Intersect(clickPos, inPos, 30, 30);
                    if (inIntersect)
                        _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DragCircle, NodeDoor.In1);

                }

                // 画布(0,0)为原点下的坐标
                if (has_out_true)
                {
                    Vector2 truePos = _panel.GetLineEndPos(_data, NodeDoor.OutTrue);
                    bool trueIntersect = Intersect(clickPos, truePos, 30, 30);
                    if (trueIntersect)
                        if (click_left)
                            _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DrawLine, NodeDoor.OutTrue);
                        else if (click_right && circle_change_pos)
                            _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DragCircle, NodeDoor.OutTrue);

                }

                if (has_out_false)
                {
                    Vector2 falsePos = _panel.GetLineEndPos(_data, NodeDoor.OutFalse);
                    bool falseIntersect = Intersect(clickPos, falsePos, 30, 30);
                    if (falseIntersect)
                        if (click_left)
                            _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DrawLine, NodeDoor.OutFalse);
                        else if (click_right && circle_change_pos)
                            _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DragCircle, NodeDoor.OutFalse);
                }
            }

            // 拖拽卡片
            if (_dragStatus.Type == NodeUI_DragType.None)
            {
                Vector2 pointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out pointerLocalPos
                );
                _dragOffset = selfR.anchoredPosition - pointerLocalPos;
                _dragStatus = new NodeUI_DragInfo(NodeUI_DragType.DragCard, 0);
            }

        }

        // 双击出界面。
        public virtual void OnPointerUp(PointerEventData eventData)
        {
            // DU.LogWarning("OnPointerUp");

            _finishOneClick = true;
            if (_inDragging) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // 按住Ctrl再点击节点出引用
                bool already = false;
                if (_panel.HoldCtrl)
                    already = HoldCtrlAndClick();

                // 选中之后的点击，出详情
                if (_doubleClick && !already)
                {
                    var datas = new List<object>() { _data, _panel };
                    PanelRunConfig config = new PanelRunConfig
                    {
                        SetPosType = PanelSetPosType.ReferenceAndOptimal,
                        PosTarget = selfR,
                        // PosOffset = new Vector2(0, -30),
                    };
                    UIManager.Inst.ShowPanel(PanelEnum.ProcessNodeInfoPanel, datas, config);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // 菜单是要的

            }

        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            // DU.LogWarning("BeginDrag");

            _inDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {

            Vector2 canvas_point;          // 鼠标位置，以_panel.Canvas的pivot为原点
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                _panel.Canvas, eventData.position, eventData.pressEventCamera,
                                out canvas_point);

            var type = _dragStatus.Type;
            NodeDoor door = _dragStatus.FromDoor;
            if (type == NodeUI_DragType.DragCard)
            {

                var pos = _panel.Map.MapConvert(canvas_point + _dragOffset);
                // 限制拖动范围
                var w = selfR.rect.width;
                var h = selfR.rect.height;
                pos.x = Math.Clamp(pos.x, 0 + w / 2, _panel.CanvasCfg.W - w / 2);
                pos.y = Math.Clamp(pos.y, 0 + h / 2, _panel.CanvasCfg.H - h / 2);

                _data.Pos = pos;
                SetPos();
                RefreshRelativeLine();

            }
            else if (type == NodeUI_DragType.DrawLine)
            {
                var from = _panel.GetLineEndPos(_data, door);
                string hover_id = _panel.MouseHoverId;
                bool has_in = GetTargetHasIn(hover_id);
                var mouse_pos = new Vector2(canvas_point.x, _panel.CanvasCfg.H + canvas_point.y);

                if (hover_id != null && hover_id != _id && has_in && !_data.Links.ContainsKey(hover_id))
                {
                    // 有悬浮节点，画到悬浮节点入口
                    var hover_ui = _panel.GetNode(_panel.MouseHoverId);
                    _panel.GetLineEndOwnership(hover_ui._type, out _, out bool has_in1, out _, out _);
                    var to0 = _panel.GetLineEndPos(hover_ui._data, NodeDoor.In);
                    if (has_in1)
                    {
                        var to1 = _panel.GetLineEndPos(hover_ui._data, NodeDoor.In1);
                        if ((mouse_pos - to0).sqrMagnitude < (mouse_pos - to1).sqrMagnitude)
                        { _panel.ShowLineForDrag(from, to0, WhiteColor); _dragStatus.ToDoor = NodeDoor.In; }
                        else
                        { _panel.ShowLineForDrag(from, to1, WhiteColor); _dragStatus.ToDoor = NodeDoor.In1; }
                    }
                    else
                    {
                        { _panel.ShowLineForDrag(from, to0, WhiteColor); _dragStatus.ToDoor = NodeDoor.In; }
                    }
                }
                else
                {
                    // 没有悬浮节点，画到鼠标位置。DragEnd时也不会做算
                    _panel.ShowLineForDrag(from, mouse_pos, WhiteColor);
                }

            }
            else if (type == NodeUI_DragType.DragCircle)
            {
                // 这里是给出入口手动绑插槽
                //
                Vector2 node_point;             // 本节点坐标系下的位置
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                    (RectTransform)transform, eventData.position, eventData.pressEventCamera,
                                    out node_point);


                var pos_list = new List<Vector2>(_panel.nodeUI_slotPos_cfg[_data.NodeType]);
                float min_distance = float.MaxValue;


                var list = new int[8];
                var slots = _scriptData.GetSlot(_data);
                // 标记已占用的槽位
                if (slots != null)
                    for (int i = 0; i < 4; i++)
                    {
                        if (i == (int)door) continue;
                        int slot_value = slots[i];
                        if (slot_value >= 0)
                        {
                            list[slot_value] = 1;
                        }
                    }

                // 给出入口找槽, 跳过已占用的槽
                int direction = 0;  // 0-正下
                for (int i = 0; i < 8; i++)
                {
                    if (list[i] == 1) continue;
                    var t = (node_point - pos_list[i]).SqrMagnitude();
                    if (t < min_distance)
                    {
                        min_distance = t;
                        direction = i;
                    }
                }


                _scriptData.AddSlot(_data, (int)door, direction);

                // 调整其他槽位，以及刷新线段
                RefreshRelativeLine();
            }

        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // DU.LogWarning("OnEndDrag");
            var type = _dragStatus.Type;
            if (type == NodeUI_DragType.DrawLine)
            {
                _panel.HideLineForDrag();
                string hover_id = _panel.MouseHoverId;
                bool has_in = GetTargetHasIn(hover_id);

                if (hover_id != null && hover_id != _id && has_in && !_data.Links.ContainsKey(hover_id))
                {
                    _panel.AddLine(_scriptData, _id, hover_id, _dragStatus.FromDoor, _dragStatus.ToDoor);
                    RefreshSelected();
                }
            }

        }

        //鼠标悬浮进入事件
        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            _panel.MouseHoverId = _id;
        }

        //鼠标悬浮离开事件
        public virtual void OnPointerExit(PointerEventData eventData)
        {
            _panel.MouseHoverId = null;
        }

        bool GetTargetHasIn(string id)
        {
            if (id == null) return false;

            bool has_in = false;
            var ui = _panel.GetNode(id);
            if (ui)
                _panel.GetLineEndOwnership(ui._data.NodeType, out has_in, out bool _, out bool _, out bool _);
            return has_in;
        }


        bool Intersect(Vector2 pos1, Vector2 pos2, float w, float h)
        {
            var delta = pos1 - pos2;
            return Math.Abs(delta.x) < w / 2 && Math.Abs(delta.y) < h / 2;
        }

        bool HoldCtrlAndClick()
        {
            if (_data.NodeType == NodeType.TriggerEvent || _data.NodeType == NodeType.ListenEvent)
            {
                var TipsComp = _panel.TipsComp;

                List<BaseNodeData> match_list = null;
                if (_data.NodeType == NodeType.TriggerEvent)
                {
                    var data = _data as TriggerEventNode;
                    var event_name = data.EventList != null ? data.EventList[0].Item3 : "";
                    match_list = _scriptData.GetListenNodes(event_name).ConvertAll(m => m as BaseNodeData);
                }
                else if (_data.NodeType == NodeType.ListenEvent)
                {
                    var data = _data as ListenEventNode;
                    match_list = _scriptData.GetEditTriggerNodes(data.EventNameParse).ConvertAll(m => m as BaseNodeData);
                }


                TipsComp.gameObject.SetActive(true);
                TipsComp.SetData(match_list.ConvertAll(m => m.GetShowName()),
                index =>
                {
                    var id = match_list[index].Id;
                    _panel.OnClickCanvasTab(_panel.GetCanvasIndex(id), false);
                    _panel.Map.ScrollToNode(id);
                }, 180);

                // 设置位置
                var tipsCompRectT = TipsComp.GetComponent<RectTransform>();
                tipsCompRectT.SetParent(_panel.Canvas, false);
                var pos = Utils.GetPos(tipsCompRectT, selfR, default);
                // 还得考虑 target的minAnchor和maxAnchor。
                var anchor = selfR.anchorMin;
                var parentRect = selfR.parent as RectTransform;
                pos = pos + new Vector2(
                    (anchor.x - 0.5f) * parentRect.rect.width,
                    (0.5f - anchor.y) * parentRect.rect.height
                );
                pos = pos + new Vector2(selfR.rect.width / 2 + 5, selfR.rect.height / 2 - 5);

                tipsCompRectT.anchoredPosition = pos;
                return true;
            }
            return false;
        }

        void RefreshRelativeLine()
        {
            _panel.RefreshNodeSlot(_id);

            // 把关联的所有节点都刷一遍槽点，然后刷新线段
            var set = _data.LineNodeIds;
            foreach (var node_id in set)
            {
                _panel.RefreshNodeSlot(node_id);
            }

            _panel.RefreshLineByNode(_id);
            foreach (var node_id in set)
            {
                _panel.RefreshLineByNode(node_id);
            }
        }

        #endregion
    }
}