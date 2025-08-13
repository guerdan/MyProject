
using Script.Model.Auto;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Script.Util;
using System.Collections.Generic;
using Script.Framework.UI;
using System;

namespace Script.UI.Panel.Auto
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


        [SerializeField] private RectTransform ScaleNode;
        [SerializeField] private RectTransform InflowNode;
        [SerializeField] private RectTransform EventNode;
        [SerializeField] private RectTransform TrueOutNode;
        [SerializeField] private RectTransform FalseOutNode;
        [SerializeField] private Image OutlineF;      //前
        [SerializeField] private Image OutlineB;      //后。常规色

        [SerializeField] private GameObject IsFirstMark;      //起点标记


        public RectTransform rectTransform => (RectTransform)transform;
        public Vector2 InflowNodePos => rectTransform.anchoredPosition + InflowNode.anchoredPosition;
        public Vector2 EventNodePos => rectTransform.anchoredPosition + EventNode.anchoredPosition;
        public Vector2 TrueOutNodePos => rectTransform.anchoredPosition + TrueOutNode.anchoredPosition;
        public Vector2 FalseOutNodePos => rectTransform.anchoredPosition + FalseOutNode.anchoredPosition;

        public AutoScriptData _autoData => ProcessNodeManager.Inst._autoData;
        [SerializeField] private Text Title;



        private BaseNodeData _data;
        private DrawProcessPanel _panel;


        void Awake()
        {
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
        }
        void OnEnable()
        {
            ProcessNodeManager.Inst.Tick += Tick;
            ProcessNodeManager.Inst.StatusChange += StatusChange;
        }

        void OnDisable()
        {
            ProcessNodeManager.Inst.Tick -= Tick;
            ProcessNodeManager.Inst.StatusChange -= StatusChange;

        }

        public void SetData(BaseNodeData nodeData, DrawProcessPanel panel)
        {
            _data = nodeData;
            _panel = panel;
            RefreshContent();
            Refresh();
            SetPos();
        }



        public void RefreshContent()
        {
            Title.text = _data.Id;
        }


        public void Refresh()
        {
            if (_data.Status == NodeStatus.Off)
            {
                OutlineF.gameObject.SetActive(false);
                if (_panel.MouseSelectedId == _data.Id)
                    OutlineB.color = WhiteColor;
                else
                    OutlineB.color = _data.ExcuteTimes > 0 ? BrownColor : GreenColor;
            }
            else
            {
                OutlineF.gameObject.SetActive(true);
                OutlineF.color = RedColor;
                OutlineF.fillAmount = _data.Timer / _data.Delay;
                OutlineB.color = RedBgColor;
            }

            IsFirstMark.SetActive(_data.Id == _autoData.FirstNode);
        }



        public void SetPos()
        {
            var pos = _data.Pos;
            rectTransform.anchoredPosition = new Vector2(pos.x, _panel.CanvasCfg.H - pos.y);
        }


        // StatusChange事件下，节点流入到另个节点，前节点先画后节点后画
        public void DrawLine()
        {
            bool selected = _data.Id == _panel.MouseSelectedId;
            
            var total_list = new List<string>();
            total_list.AddRange(_data.TrueNextNodes);
            total_list.AddRange(_data.FalseNextNodes);
            int trueCount = _data.TrueNextNodes.Count;
            _panel.RefreshLine(_data.Id, total_list, trueCount);

            for (int i = 0; i < total_list.Count; i++)
            {
                var next_id = total_list[i];
                var isTrue = i < trueCount;

                var line = _panel.GetLine(_data.Id, next_id);
                var next_ui = _panel.GetNode(next_id);

                Color color;
                if (_data.Id == _panel.MouseSelectedId || next_id == _panel.MouseSelectedId)
                {
                    color = WhiteColor;
                }
                else
                {
                    if (_data.Status == NodeStatus.Off)
                    {
                        color = _data.ExcuteTimes > 0 ? BrownColor : GreenColor;
                        if (next_ui._data.Status == NodeStatus.In) color = RedColor;
                    }
                    else
                        color = RedColor;
                }

                var p = isTrue ? TrueOutNodePos : FalseOutNodePos;
                if (selected)
                {
                    Vector2 v = next_ui.InflowNodePos - p;
                    p += v.normalized * 10;
                }
                line.GetComponent<ProcessNodeLineUI>().DrawLine(p, next_ui.InflowNodePos, color);
            }

            //圈圈操作
            TrueOutNode.gameObject.SetActive(selected);
            FalseOutNode.gameObject.SetActive(selected);
            if (selected)
            {
                TrueOutNode.GetComponent<Image>().color = _data.TrueNextNodes.Count > 0 ? WhiteColor : BrownColor;
                FalseOutNode.GetComponent<Image>().color = _data.FalseNextNodes.Count > 0 ? WhiteColor : BrownColor;
            }
        }
        // 自身和前节点 刷新线段ui
        public void DrawLineSelfAndLast()
        {
            DrawLine();
            foreach (var id in _data.LastNode)
            {
                var n = _panel.GetNode(id);
                n.DrawLine();
            }
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
            DrawLine();
        }


        #region 操作交互
        private readonly float BiggerScale = 1.07f;
        private readonly float ScalingDuration = 0.1f;
        private Tween touchStartTween;
        private Tween touchEndTween;
        private ProcessNodeDragStatus _dragStatus = ProcessNodeDragStatus.None;
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
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out clickPos
                );

                Vector2 truePos = rectTransform.InverseTransformPoint(TrueOutNode.transform.position);
                bool trueIntersect = Intersect(clickPos, truePos, 30, 30);
                if (trueIntersect) _dragStatus = ProcessNodeDragStatus.LineTrue;

                Vector2 falsePos = rectTransform.InverseTransformPoint(FalseOutNode.transform.position);
                bool falseIntersect = Intersect(clickPos, falsePos, 30, 30);
                if (falseIntersect) _dragStatus = ProcessNodeDragStatus.LineFalse;
                // DU.LogWarning($"Drag {clickPos - localPos}");
            }


            // 用于拖拽节点实现， 服务OnDrag、OnEndDrag
            if (_dragStatus == ProcessNodeDragStatus.None)
            {
                Vector2 pointerLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
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
            if (_dragStatus != ProcessNodeDragStatus.None) return;
            if (!_doubleClick) return;

            UIManager.Inst.ShowPanel(PanelEnum.ProcessNodePanel, null);
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            // DU.LogWarning("BeginDrag");
        }

        public void OnDrag(PointerEventData eventData)
        {
            //鼠标位置，以rectTransform.parent的pivot为原点
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                rectTransform.parent as RectTransform,
                                eventData.position,
                                eventData.pressEventCamera,
                                out point);

            if (_dragStatus == ProcessNodeDragStatus.Card)
            {
                _data.Pos = _panel.MapConvert(point + _dragOffset);
                SetPos();
                DrawLineSelfAndLast();

            }
            else if (_dragStatus == ProcessNodeDragStatus.LineTrue || _dragStatus == ProcessNodeDragStatus.LineFalse)
            {
                var from = _dragStatus == ProcessNodeDragStatus.LineTrue ? TrueOutNodePos : FalseOutNodePos;
                string hover_id = _panel.MouseHoverId;

                if (hover_id != null && hover_id != _data.Id &&
                !_data.TrueNextNodes.Contains(hover_id) && !_data.FalseNextNodes.Contains(hover_id))
                {
                    // 有悬浮节点，画到悬浮节点入口
                    var hover_ui = _panel.GetNode(_panel.MouseHoverId);
                    var to = hover_ui.InflowNodePos;
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
                    ProcessNodeManager.Inst.AddLine(_data.Id, hover_id, _dragStatus == ProcessNodeDragStatus.LineTrue);
                    DrawLine();
                }
            }



            _dragStatus = ProcessNodeDragStatus.None;
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
        }

        bool Intersect(Vector2 pos1, Vector2 pos2, float w, float h)
        {
            var delta = pos1 - pos2;
            return Math.Abs(delta.x) < w / 2 && Math.Abs(delta.y) < h / 2;
        }

        #endregion
    }
}