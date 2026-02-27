
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{

    /// <summary>
    /// 1.增加右键点击功能
    /// 2.屏蔽 Submit 功能。原本是，焦点在Button上时，按任意键就会触发OnClick()
    /// </summary>
    public class ExButton : Button
    {
        private ButtonClickedEvent m_OnClickRight = new ButtonClickedEvent();

        public ButtonClickedEvent onClickRight
        {
            get { return m_OnClickRight; }
            set { m_OnClickRight = value; }
        }


        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                base.OnPointerClick(eventData);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                PressRight();
            }

        }


        private void PressRight()
        {
            if (!IsActive() || !IsInteractable())
                return;

            m_OnClickRight.Invoke();
        }


        public override void OnSubmit(BaseEventData eventData)
        {
        }
    }
}