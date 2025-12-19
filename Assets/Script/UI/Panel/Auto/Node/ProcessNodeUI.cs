
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
        Card,           // 拖拽卡片
        LineTrue,       // 拖拽true线
        LineFalse,      // 拖拽false线
    }

    public class ProcessNodeUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public static readonly Color BrownColor;
        public static readonly Color RedColor;
        public static readonly Color GreenColor;
        public static readonly Color WhiteColor;
        public static readonly Color RedBgColor;

        static ProcessNodeUI()
        {
            BrownColor = Utils.ParseHtmlString("#4B2C21");
            RedColor = Utils.ParseHtmlString("#B2382D");
            GreenColor = Utils.ParseHtmlString("#4C7543");
            WhiteColor = Utils.ParseHtmlString("#FAF8F4");
            RedBgColor = Utils.ParseHtmlString("#EFEDE9");
        }

        [Header("通用")]
        [SerializeField] private ScaleArtComp ScaleNode;
        [SerializeField] public RectTransform InflowNode;
        // [SerializeField] private RectTransform EventNode;     //决定不要了,拆离出来单独做个事件接收节点
        [SerializeField] public RectTransform TrueOutNode;
        [SerializeField] public RectTransform FalseOutNode;
        [SerializeField] private Image OutlineF;      //前
        [SerializeField] private Image OutlineB;      //后。常规色
        [SerializeField] private GameObject IsFirstMark;      //起点标记


        public RectTransform selfR => (RectTransform)transform;
        public AutoScriptManager manager => AutoScriptManager.Inst;


        [HideInInspector] public BaseNodeData _data;
        [HideInInspector] public string _id;
        [HideInInspector] public NodeType _type;
        protected DrawProcessPanel _panel;
        AutoScriptData _scriptData;

       protected virtual void Awake()
        {
            selfR.anchorMin = new Vector2(0, 0);
            selfR.anchorMax = new Vector2(0, 0);
            // 初始化组件。子层级会阻拦事件传入父层级
            ScaleNode.SetData(OnPointerEnter, OnPointerExit);
        }
        protected virtual void OnEnable()
        {
            AutoScriptManager.Inst.Tick += Tick;
            AutoScriptManager.Inst.StatusChange += StatusChange;
        }

        protected virtual void OnDisable()
        {
            AutoScriptManager.Inst.Tick -= Tick;
            AutoScriptManager.Inst.StatusChange -= StatusChange;

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
        public virtual void RefreshContent() { }

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
            TrueOutNode.anchoredPosition = _panel.GetLineEndPos(_data, 1) - selfR.anchoredPosition;
            if (FalseOutNode) FalseOutNode.anchoredPosition = _panel.GetLineEndPos(_data, 2) - selfR.anchoredPosition;
        }

        /// <summary>
        /// 刷新选中状态
        /// </summary>
        public virtual void RefreshSelected()
        {
            bool selected = _id == _panel.MouseSelectedId;
            _panel.GetLineEndOwnership(_data.NodeType, out bool _, out bool has_true, out bool has_false);

            //圈圈操作
            Utils.SetActive(TrueOutNode, selected && has_true);
            Utils.SetActive(FalseOutNode, selected && has_false);
            if (selected)
            {
                if (has_true) TrueOutNode.GetComponent<Image>().color = _data.TrueNextNodes.Count > 0 ? WhiteColor : BrownColor;
                if (has_false) FalseOutNode.GetComponent<Image>().color = _data.FalseNextNodes.Count > 0 ? WhiteColor : BrownColor;
            }
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

        // PointerDown > BeginDrag > Drag > PointerUp > EndDrag
        // BeginDrag在用户PointerDown后的下一帧才触发，一帧的位移会被忽略
        public void OnPointerDown(PointerEventData eventData)
        {
            // DU.LogWarning("PointerDown");
            _doubleClick = _panel.MouseSelectedId == _id;
            _finishOneClick = false;

            // 节点变白色调
            {

                string pre = _panel.MouseSelectedId;
                _panel.MouseSelectedId = _id;

            }

            // 检测是否点击在 TrueOutNode 的区域，矩形30X30范围，用于拖拽连线
            {
                Vector2 clickPos;
                // 这个接口按pivot为原点，所以是画布(0,1)为原点下的坐标
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out clickPos
                );
                // 故加个画布Height
                clickPos += new Vector2(0, _panel.Map.ContentH);
                _panel.GetLineEndOwnership(_data.NodeType, out bool _, out bool out_true_own,
                    out bool out_false_own);

                // 画布(0,0)为原点下的坐标
                if (out_true_own)
                {
                    Vector2 truePos = _panel.GetLineEndPos(_data, 1);
                    bool trueIntersect = Intersect(clickPos, truePos, 30, 30);
                    if (trueIntersect) _dragStatus = ProcessNodeDragStatus.LineTrue;
                }

                if (out_false_own)
                {
                    Vector2 falsePos = _panel.GetLineEndPos(_data, 2);
                    bool falseIntersect = Intersect(clickPos, falsePos, 30, 30);
                    if (falseIntersect) _dragStatus = ProcessNodeDragStatus.LineFalse;
                }

                // DU.LogWarning($"Drag {clickPos - localPos}");
            }


            // 用于拖拽节点实现， 服务于OnDrag、OnEndDrag
            if (_dragStatus == ProcessNodeDragStatus.None)
            {
                Vector2 pointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out pointerLocalPos
                );
                _dragOffset = selfR.anchoredPosition - pointerLocalPos;
                _dragStatus = ProcessNodeDragStatus.Card;
            }
        }

        // 双击出界面。
        public virtual void OnPointerUp(PointerEventData eventData)
        {
            // DU.LogWarning("OnPointerUp");
            _finishOneClick = true;

            if (_inDragging) return;

            // 按住Ctrl再点击节点出引用
            bool already = false;
            if (_panel.HoldCtrl)
            {
                already = HoldCtrlAndClick();
            }

            // 选中之后的点击，出详情
            if (_doubleClick && !already)
            {
                var datas = new List<object>();
                datas.Add(_data);
                datas.Add(selfR);
                datas.Add(_panel);
                UIManager.Inst.ShowPanel(PanelEnum.ProcessNodeInfoPanel, datas);
            }
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            // DU.LogWarning("BeginDrag");
            _inDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            //鼠标位置，以rectTransform.parent的pivot为原点
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                _panel.Canvas, eventData.position, eventData.pressEventCamera,
                                out point);

            if (_dragStatus == ProcessNodeDragStatus.Card)
            {
                _data.Pos = _panel.Map.MapConvert(point + _dragOffset);
                SetPos();
                RefreshRelativeLine();

            }
            else if (_dragStatus == ProcessNodeDragStatus.LineTrue || _dragStatus == ProcessNodeDragStatus.LineFalse)
            {
                var from = _panel.GetLineEndPos(_data, _dragStatus == ProcessNodeDragStatus.LineTrue ? 1 : 2);
                string hover_id = _panel.MouseHoverId;
                bool has_in = GetTargetHasIn(hover_id);

                if (hover_id != null && hover_id != _id && has_in &&
                !_data.TrueNextNodes.Contains(hover_id) && !_data.FalseNextNodes.Contains(hover_id))
                {
                    // 有悬浮节点，画到悬浮节点入口
                    var hover_ui = _panel.GetNode(_panel.MouseHoverId);
                    var to = hover_ui.GetChildNodePos(hover_ui.InflowNode);
                    _panel.ShowLineForDrag(from, to, WhiteColor);
                }
                else
                {

                    // 没有悬浮节点，画到鼠标位置
                    var to = new Vector2(point.x, _panel.CanvasCfg.H + point.y);
                    _panel.ShowLineForDrag(from, to, WhiteColor);
                }

            }


        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // DU.LogWarning("OnEndDrag");
            if (_dragStatus == ProcessNodeDragStatus.LineTrue || _dragStatus == ProcessNodeDragStatus.LineFalse)
            {
                _panel.HideLineForDrag();
                string hover_id = _panel.MouseHoverId;
                bool in_own = GetTargetHasIn(hover_id);

                if (hover_id != null && hover_id != _id && in_own)
                {
                    _panel.AddLine(_scriptData, _id, hover_id,
                        _dragStatus == ProcessNodeDragStatus.LineTrue);
                    RefreshSelected();
                }
            }


            _dragStatus = ProcessNodeDragStatus.None;
            _inDragging = false;
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

        Vector2 GetChildNodePos(RectTransform child)
        {
            return selfR.anchoredPosition + Utils.GetRelativePosToParent(child);
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
                    match_list = _scriptData.GetListenNodes(data.EventName).ConvertAll(m => m as BaseNodeData);
                }
                else if (_data.NodeType == NodeType.ListenEvent)
                {
                    var data = _data as ListenEventNode;
                    match_list = _scriptData.GetEditTriggerNodes(data.EventName).ConvertAll(m => m as BaseNodeData);
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
            // 简洁的就是只刷自己的槽，严谨的效果就是，把关联的所有节点都刷一遍槽点，然后刷新线段 要的
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