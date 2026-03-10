
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
    public enum ProcessNodeDragStatus
    {
        None,           // 无拖拽状态
        DragCard,       // 拖拽卡片
        DrawLineTrue,   // 拖拽true线
        DrawLineFalse,  // 拖拽false线
        DragCircleIn,   // 拖拽in圈圈
        DragCircleTrue, // 拖拽true圈圈
        DragCircleFalse,// 拖拽false圈圈
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
        [SerializeField] private Image OutlineF;      //前
        [SerializeField] private Image OutlineB;      //后。常规色
        [SerializeField] private GameObject IsFirstMark;      //起点标记
        [SerializeField] private Text DelayText;


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
        }
        protected virtual void OnEnable()
        {
            AutoScriptManager.Inst.OnTick += Tick;
            AutoScriptManager.Inst.OnStatusChange += StatusChange;
        }

        protected virtual void OnDisable()
        {
            AutoScriptManager.Inst.OnTick -= Tick;
            AutoScriptManager.Inst.OnStatusChange -= StatusChange;

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
            bool selected = _id == _panel.MouseSelectedId;
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
            InflowNode.anchoredPosition = _panel.GetLineEndPos(_data, 0) - selfR.anchoredPosition;
            TrueOutNode.anchoredPosition = _panel.GetLineEndPos(_data, 1) - selfR.anchoredPosition;

            _panel.GetLineEndOwnership(_data.NodeType, out bool _, out bool has_true, out bool has_false);
            if (has_false) FalseOutNode.anchoredPosition = _panel.GetLineEndPos(_data, 2) - selfR.anchoredPosition;

            if (_data.NodeType == NodeType.ConditionOper || _data.NodeType == NodeType.MapPathFinding
                || _data.NodeType == NodeType.AssignOper)
            {
                var pos0 = _panel.GetLineEndPos(_data, 1) - selfR.anchoredPosition;
                TrueCircleTextR.anchoredPosition = GetTextPos(pos0);
                var pos1 = _panel.GetLineEndPos(_data, 2) - selfR.anchoredPosition;
                FalseCircleTextR.anchoredPosition = GetTextPos(pos1);
                var pos2 = _panel.GetLineEndPos(_data, 0) - selfR.anchoredPosition;
                InCircleTextR.anchoredPosition = GetTextPos(pos2, 1);
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
            bool selected = _id == _panel.MouseSelectedId;
            _panel.GetLineEndOwnership(_data.NodeType, out bool _, out bool has_true, out bool has_false);
            var checkBox_true = TrueOutNode.GetComponent<CheckBox>();
            var checkBox_false = FalseOutNode ? FalseOutNode.GetComponent<CheckBox>() : null;
            var checkBox_in = InflowNode ? InflowNode.GetComponent<CheckBox>() : null;


            if (_data.NodeType == NodeType.TemplateMatchOper
                || _data.NodeType == NodeType.ConditionOper || _data.NodeType == NodeType.MapPathFinding)
            {
                // 复杂圈圈，有T/F字样
                Utils.SetActive(TrueOutNode, true);
                Utils.SetActive(FalseOutNode, true);
                checkBox_true.SetData(selected);
                checkBox_false.SetData(selected);
                if (checkBox_in) checkBox_in.SetData(false);
            }
            else
            {
                // 普通圈圈款式，无T/F字样
                Utils.SetActive(TrueOutNode, selected && has_true);
                Utils.SetActive(FalseOutNode, selected && has_false);
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
        private ProcessNodeDragStatus _dragStatus = ProcessNodeDragStatus.None;
        private bool _inDragging = false;
        private Vector2 _dragOffset;
        private bool _doubleClick = false;
        protected bool _finishOneClick = false;

        // 重置状态位
        void ClearOperation()
        {
            _dragStatus = ProcessNodeDragStatus.None;
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

            _doubleClick = _panel.MouseSelectedId == _id;

            // 设置选中，节点变白框
            _panel.MouseSelectedId = _id;

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
                _panel.GetLineEndOwnership(_data.NodeType, out bool has_in, out bool has_out_true,
                    out bool has_out_false);

                bool change_circle = _data.NodeType != NodeType.TemplateMatchOper;

                if (has_in && click_right && change_circle)
                {
                    Vector2 inPos = _panel.GetLineEndPos(_data, 0);
                    bool inIntersect = Intersect(clickPos, inPos, 30, 30);
                    if (inIntersect)
                        _dragStatus = ProcessNodeDragStatus.DragCircleIn;
                }

                // 画布(0,0)为原点下的坐标
                if (has_out_true)
                {
                    Vector2 truePos = _panel.GetLineEndPos(_data, 1);
                    bool trueIntersect = Intersect(clickPos, truePos, 30, 30);
                    if (trueIntersect)
                        if (click_left)
                            _dragStatus = ProcessNodeDragStatus.DrawLineTrue;
                        else if (click_right && change_circle)
                            _dragStatus = ProcessNodeDragStatus.DragCircleTrue;

                }

                if (has_out_false)
                {
                    Vector2 falsePos = _panel.GetLineEndPos(_data, 2);
                    bool falseIntersect = Intersect(clickPos, falsePos, 30, 30);
                    if (falseIntersect)
                        if (click_left)
                            _dragStatus = ProcessNodeDragStatus.DrawLineFalse;
                        else if (click_right && change_circle)
                            _dragStatus = ProcessNodeDragStatus.DragCircleFalse;
                }

            }


            // 拖拽卡片
            if (_dragStatus == ProcessNodeDragStatus.None)
            {
                Vector2 pointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out pointerLocalPos
                );
                _dragOffset = selfR.anchoredPosition - pointerLocalPos;
                _dragStatus = ProcessNodeDragStatus.DragCard;
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

            if (_dragStatus == ProcessNodeDragStatus.DragCard)
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
            else if (_dragStatus == ProcessNodeDragStatus.DrawLineTrue || _dragStatus == ProcessNodeDragStatus.DrawLineFalse)
            {
                var from = _panel.GetLineEndPos(_data, _dragStatus == ProcessNodeDragStatus.DrawLineTrue ? 1 : 2);
                string hover_id = _panel.MouseHoverId;
                bool has_in = GetTargetHasIn(hover_id);

                if (hover_id != null && hover_id != _id && has_in &&
                !_data.TrueNextNodes.Contains(hover_id) && !_data.FalseNextNodes.Contains(hover_id))
                {
                    // 有悬浮节点，画到悬浮节点入口
                    var hover_ui = _panel.GetNode(_panel.MouseHoverId);
                    var to = _panel.GetLineEndPos(hover_ui._data, 0);
                    _panel.ShowLineForDrag(from, to, WhiteColor);
                }
                else
                {
                    // 没有悬浮节点，画到鼠标位置
                    var to = new Vector2(canvas_point.x, _panel.CanvasCfg.H + canvas_point.y);
                    _panel.ShowLineForDrag(from, to, WhiteColor);
                }

            }
            else if (_dragStatus == ProcessNodeDragStatus.DragCircleTrue
                    || _dragStatus == ProcessNodeDragStatus.DragCircleFalse
                    || _dragStatus == ProcessNodeDragStatus.DragCircleIn)
            {
                Vector2 node_point;             // 本节点坐标系下的位置
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                    (RectTransform)transform, eventData.position, eventData.pressEventCamera,
                                    out node_point);


                var pos_list = new List<Vector2>(_panel.nodeUISlotPosCfg[_data.NodeType]);
                float min_distance = float.MaxValue;

                int type = -1;
                if (_dragStatus == ProcessNodeDragStatus.DragCircleIn)
                    type = 0;
                else if (_dragStatus == ProcessNodeDragStatus.DragCircleTrue)
                    type = 1;
                else if (_dragStatus == ProcessNodeDragStatus.DragCircleFalse)
                    type = 2;

                var list = new int[8];
                var slots = _scriptData.GetSlot(_data);
                // 标记已占用的槽位
                if (slots != null)
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == type) continue;
                        int slot_value = slots[i];
                        if (slot_value >= 0)
                        {
                            list[slot_value] = 1;
                        }
                    }

                int direction = 0;  // 0-下，转一圈
                for (int i = 0; i < 8; i++)
                {
                    if (list[i] == 1) continue;   // 跳过已占用的槽
                    var t = (node_point - pos_list[i]).SqrMagnitude();
                    if (t < min_distance)
                    {
                        min_distance = t;
                        direction = i;
                    }
                }



                _scriptData.AddSlot(_data, type, direction);

                // 调整其他槽位，以及刷新线段
                RefreshRelativeLine();
            }

        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // DU.LogWarning("OnEndDrag");

            if (_dragStatus == ProcessNodeDragStatus.DrawLineTrue || _dragStatus == ProcessNodeDragStatus.DrawLineFalse)
            {
                _panel.HideLineForDrag();
                string hover_id = _panel.MouseHoverId;
                bool in_own = GetTargetHasIn(hover_id);

                if (hover_id != null && hover_id != _id && in_own)
                {
                    _panel.AddLine(_scriptData, _id, hover_id,
                        _dragStatus == ProcessNodeDragStatus.DrawLineTrue);
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
                _panel.GetLineEndOwnership(ui._data.NodeType, out has_in, out bool _, out bool _);
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
                    match_list = _scriptData.GetListenNodes(data.EventNameParse).ConvertAll(m => m as BaseNodeData);
                }
                else if (_data.NodeType == NodeType.ListenEvent)
                {
                    var data = _data as ListenEventNode;
                    match_list = _scriptData.GetEditTriggerNodes(data.EventNameParse).ConvertAll(m => m as BaseNodeData);
                }


                TipsComp.gameObject.SetActive(true);
                TipsComp.SetData(match_list.ConvertAll(m => m.Name),
                index =>
                {
                    _panel.Map.ScrollToNode(match_list[index].Id);
                });

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
            var set = _data.GetRelativeNode();
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