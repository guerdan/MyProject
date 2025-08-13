

using Script.Framework.UI;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.UI.Component
{
    public class DragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private Vector2 offset;
        private BasePanel panel;   

        public void SetData(BasePanel panel)
        {
            this.panel = panel;
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            if (panel == null) return;

            // 将点击处的屏幕坐标转换为父容器的本地坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)panel.transform.parent,
                eventData.position,
                eventData.pressEventCamera,
                out offset
            );
            // 相对坐标。拖拽过程中，物体与鼠标的相对关系不变
            offset = panel.PanelDefine.InitPos - offset;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (panel == null) return;

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)panel.transform.parent,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint))
            {
                // 界面节点位置 => 依赖InitPos  
                panel.PanelDefine.InitPos = localPoint + offset;
                panel.RefreshPos(); // 更新位置
            }
        }

    }
}