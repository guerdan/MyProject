
using Script.Model.Auto;
using Script.UI.Panel.Auto.Node;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ProcessNodeLineUI : MonoBehaviour, IPointerDownHandler
    {
        Image img;
        string _id;
        string _fromId;
        string _toId;
        BaseNodeData _fromData;
        BaseNodeData _toData;
        public bool _isTrue;
        DrawProcessPanel _panel;
        SplitLineComp _map;


        public void Awake()
        {
            img = GetComponent<Image>();
        }

        public void SetData(string id, DrawProcessPanel panel)
        {
            _id = id;
            _panel = panel;

            var s = id.Substring(5);
            int first = s.IndexOf('-');
            int second = s.IndexOf('-', first + 1);
            _fromId = s.Substring(0, second);      // from-id
            _toId = s.Substring(second + 1);    // to-id
            _fromData = AutoScriptManager.Inst.GetNode(_fromId);
            _toData = AutoScriptManager.Inst.GetNode(_toId);
            _map = _panel.SplitLineComp;

            _isTrue = _fromData.TrueNextNodes.Contains(_toId);

        }


        /// <summary>
        /// 动态剔除功能，要考虑到线段可能没有to节点但也要显示。
        /// </summary>
        public void DrawLine()
        {
            var from_node_pos = _map.MapConvert(_fromData.Pos);
            var to_node_pos = _map.MapConvert(_toData.Pos);

            var from_template = _panel.GetPrefab(_fromData.GetNodeType()).GetComponent<ProcessNodeUI>();
            var to_template = _panel.GetPrefab(_toData.GetNodeType()).GetComponent<ProcessNodeUI>();

            var from_pos = from_node_pos + (_isTrue ? Utils.GetRelativePosToParent(from_template.TrueOutNode) : Utils.GetRelativePosToParent(from_template.FalseOutNode));
            var to_pos = to_node_pos + Utils.GetRelativePosToParent(to_template.InflowNode);

            Color color;
            if (_id == _panel.MouseSelectedId || _fromId == _panel.MouseSelectedId || _toId == _panel.MouseSelectedId)
            {
                color = ProcessNodeUI.WhiteColor;
            }
            else
            {
                if (_fromData.Status == NodeStatus.Off)
                {
                    color = _fromData.ExcuteTimes > 0 ? ProcessNodeUI.BrownColor : ProcessNodeUI.GreenColor;
                    if (_toData.Status == NodeStatus.In) color = ProcessNodeUI.RedColor;
                }
                else
                    color = ProcessNodeUI.RedColor;
            }

            if (_fromId == _panel.MouseSelectedId)
            {
                Vector2 v = to_pos - from_pos;
                from_pos += v.normalized * 10;
            }

            DrawLine(from_pos, to_pos, color);
        }


        /// <summary>
        /// 线段塑形-画任意线段。image，起点，终点，颜色
        /// </summary>
        public void DrawLine(Vector2 from, Vector2 to, Color color, float thickness = 3f)
        {
            img.color = color;
            var line = GetComponent<RectTransform>();
            // 计算两点之间的中心点
            Vector2 center = (from + to) / 2f;

            // 设置RectTransform的位置为中心点
            line.anchoredPosition = center;

            // 计算两点之间的距离（线段长度）
            float distance = Vector2.Distance(from, to);

            // 如果线段是12px高度
            var scale = thickness / 9;
            line.localScale = Vector3.one * scale;

            // 设置RectTransform的尺寸：长度为两点距离，宽度为线段厚度
            line.sizeDelta = new Vector2(distance / scale, 9);

            // 计算线段的角度（弧度转角度）
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

            // 设置旋转角度
            line.rotation = Quaternion.Euler(0, 0, angle);
        }

        // 选中线段，此线段变白
        public void OnPointerDown(PointerEventData eventData)
        {
            string pre = _panel.MouseSelectedId;
            _panel.MouseSelectedId = _id;
            // 通知刷新旧选中的ui 和新选中的ui
            _panel.RefreshUISelectedStatus(pre);
            _panel.RefreshUISelectedStatus(_panel.MouseSelectedId);

        }

    }
}