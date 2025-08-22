
using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Script.Util;
using Script.Framework.UI;
using System;
using System.Collections.Generic;

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
        [SerializeField] private RectTransform ScaleNode;
        [SerializeField] public RectTransform InflowNode;
        // [SerializeField] private RectTransform EventNode;     //决定不要了,拆离出来单独做个事件接收节点
        [SerializeField] public RectTransform TrueOutNode;
        [SerializeField] public RectTransform FalseOutNode;
        [SerializeField] private Image OutlineF;      //前
        [SerializeField] private Image OutlineB;      //后。常规色
        [SerializeField] private GameObject IsFirstMark;      //起点标记


        public RectTransform rectTransform => (RectTransform)transform;
        public AutoScriptManager manager => AutoScriptManager.Inst;
        public AutoScriptData _autoData => AutoScriptManager.Inst._autoData;


        [HideInInspector] public BaseNodeData _data;
        [HideInInspector] public NodeType _type;
        protected DrawProcessPanel _panel;


        void Awake()
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
        }
        void OnEnable()
        {
            AutoScriptManager.Inst.Tick += Tick;
            AutoScriptManager.Inst.StatusChange += StatusChange;
        }

        void OnDisable()
        {
            AutoScriptManager.Inst.Tick -= Tick;
            AutoScriptManager.Inst.StatusChange -= StatusChange;

        }

        public void SetData(BaseNodeData nodeData, DrawProcessPanel panel)
        {
            _data = nodeData;
            _panel = panel;
            _type = _data.GetNodeType();
            Reset();
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
            bool selected = _data.Id == _panel.MouseSelectedId;
            bool show_progress = _type != NodeType.AssignOper && _type != NodeType.ListenEvent;
            if (show_progress)
            {
                if (_data.Status == NodeStatus.Off)
                {
                    if (_type == NodeType.MouseOper)
                    {
                        Utils.SetActive(OutlineF, false);
                        Utils.SetActive(OutlineB, selected);
                        OutlineB.color = WhiteColor;
                    }
                    else
                    {
                        Utils.SetActive(OutlineF, false);
                        if (selected)
                            OutlineB.color = WhiteColor;
                        else
                            OutlineB.color = _data.ExcuteTimes > 0 ? BrownColor : GreenColor;
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
            }
            else
            {
                // 只使用选中功能
                Utils.SetActive(OutlineB, selected);
                OutlineB.color = WhiteColor;
            }


            Utils.SetActive(IsFirstMark, _data.Id == _autoData.FirstNode);
        }

        /// <summary>
        /// 刷新选中状态
        /// </summary>
        public void RefreshSelected()
        {
            bool selected = _data.Id == _panel.MouseSelectedId;

            //圈圈操作
            Utils.SetActive(TrueOutNode, selected);
            Utils.SetActive(FalseOutNode, selected);
            if (selected)
            {
                TrueOutNode.GetComponent<Image>().color = _data.TrueNextNodes.Count > 0 ? WhiteColor : BrownColor;
                if (FalseOutNode)
                    FalseOutNode.GetComponent<Image>().color = _data.FalseNextNodes.Count > 0 ? WhiteColor : BrownColor;
            }
        }

        /// <summary>
        /// 所属节点的线段刷新
        /// </summary>
        public void RefreshLine()
        {
            _panel.RefreshLineByNode(_data.Id);
        }

        /// <summary>
        /// 设置坐标
        /// </summary>
        public void SetPos()
        {
            var pos = _data.Pos;
            rectTransform.anchoredPosition = new Vector2(pos.x, _panel.CanvasCfg.H - pos.y);
        }


        // 每帧刷新
        void Tick()
        {
            Refresh();
        }
        // 任意节点状态流入或流出触发
        void StatusChange(string id)
        {
            // if (_data.Id != id) return;
            RefreshSelected();
            RefreshLine();
        }




        #region 操作交互
        private readonly float BiggerScale = 1.07f;
        private readonly float ScalingDuration = 0.1f;
        private Tween touchStartTween;
        private Tween touchEndTween;
        private ProcessNodeDragStatus _dragStatus = ProcessNodeDragStatus.None;
        private bool _startDrag = false;
        private Vector2 _dragOffset;
        private bool _doubleClick = false;

        // PointerDown > BeginDrag > Drag > PointerUp > EndDrag
        // BeginDrag在用户PointerDown后的下一帧才触发，一帧的位移会被忽略
        public void OnPointerDown(PointerEventData eventData)
        {
            // DU.LogWarning("PointerDown");
            _doubleClick = _panel.MouseSelectedId == _data.Id;

            // 节点变白色调
            {

                string pre = _panel.MouseSelectedId;
                _panel.MouseSelectedId = _data.Id;
                // 通知刷新旧选中的ui 和新选中的ui
                _panel.RefreshUISelectedStatus(pre);
                _panel.RefreshUISelectedStatus(_panel.MouseSelectedId);
            }

            // 检测是否点击在 TrueOutNode 的区域，矩形30X30范围，用于拖拽连线
            {
                Vector2 clickPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera,
                    out clickPos
                );

                Vector2 truePos = rectTransform.InverseTransformPoint(TrueOutNode.transform.position);
                bool trueIntersect = Intersect(clickPos, truePos, 30, 30);
                if (trueIntersect) _dragStatus = ProcessNodeDragStatus.LineTrue;

                if (FalseOutNode)
                {
                    Vector2 falsePos = rectTransform.InverseTransformPoint(FalseOutNode.transform.position);
                    bool falseIntersect = Intersect(clickPos, falsePos, 30, 30);
                    if (falseIntersect) _dragStatus = ProcessNodeDragStatus.LineFalse;
                }

                // DU.LogWarning($"Drag {clickPos - localPos}");
            }


            // 用于拖拽节点实现， 服务OnDrag、OnEndDrag
            if (_dragStatus == ProcessNodeDragStatus.None)
            {
                Vector2 pointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _panel.Canvas, eventData.position, eventData.pressEventCamera,
                    out pointerLocalPos
                );
                _dragOffset = rectTransform.anchoredPosition - pointerLocalPos;
                _dragStatus = ProcessNodeDragStatus.Card;
            }
        }

        // 双击出界面。
        public void OnPointerUp(PointerEventData eventData)
        {
            // DU.LogWarning("OnPointerUp");
            if (_startDrag) return;
            if (!_doubleClick) return;
            var datas = new List<object>();
            datas.Add(_data);
            datas.Add(rectTransform);
            datas.Add(new Tuple<float, float>(0, -120));
            datas.Add(_panel);
            UIManager.Inst.ShowPanel(PanelEnum.ProcessNodeInfoPanel, datas);
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            // DU.LogWarning("BeginDrag");
            _startDrag = true;
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
                _data.Pos = _panel.MapConvert(point + _dragOffset);
                SetPos();
                RefreshLine();
            }
            else if (_dragStatus == ProcessNodeDragStatus.LineTrue || _dragStatus == ProcessNodeDragStatus.LineFalse)
            {
                var from = _dragStatus == ProcessNodeDragStatus.LineTrue ? GetChildNodePos(TrueOutNode) : GetChildNodePos(FalseOutNode);
                string hover_id = _panel.MouseHoverId;

                if (hover_id != null && hover_id != _data.Id &&
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

                if (hover_id != null && hover_id != _data.Id)
                {
                    AutoScriptManager.Inst.AddLine(_data.Id, hover_id, _dragStatus == ProcessNodeDragStatus.LineTrue);
                    RefreshSelected();
                    _panel.SyncData();
                }
            }


            _dragStatus = ProcessNodeDragStatus.None;
            _startDrag = false;
        }


        //鼠标悬浮进入事件
        public void OnPointerEnter(PointerEventData eventData)
        {
            _panel.MouseHoverId = _data.Id;

            Reset();
            float scale = 1;
            ScaleNode.localScale = new Vector3(scale, scale, scale);
            touchStartTween = DOTween.To(() => scale, x => { scale = x; }, BiggerScale, ScalingDuration).OnUpdate(() =>
            {
                ScaleNode.localScale = new Vector3(scale, scale, scale);
            });
        }

        //鼠标悬浮离开事件
        public void OnPointerExit(PointerEventData eventData)
        {
            _panel.MouseHoverId = null;

            Reset();
            float scale = BiggerScale;
            ScaleNode.localScale = new Vector3(scale, scale, scale);
            touchEndTween = DOTween.To(() => scale, x => { scale = x; }, 1, ScalingDuration).OnUpdate(() =>
            {
                ScaleNode.localScale = new Vector3(scale, scale, scale);
            });
        }

        private void Reset()
        {
            touchStartTween?.Kill();
            touchStartTween = null;
            touchEndTween?.Kill();
            touchEndTween = null;

            ScaleNode.localScale = Vector3.one;
        }

        bool Intersect(Vector2 pos1, Vector2 pos2, float w, float h)
        {
            var delta = pos1 - pos2;
            return Math.Abs(delta.x) < w / 2 && Math.Abs(delta.y) < h / 2;
        }

        Vector2 GetChildNodePos(RectTransform child) {
            return rectTransform.anchoredPosition + Utils.GetRelativePosToParent(child);
        }

        #endregion
    }
}