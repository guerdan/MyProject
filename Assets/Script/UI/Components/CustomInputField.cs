using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
namespace Script.UI.Components
{
    public class CustomInputField : InputField
    {
        public Action<PointerEventData> onPointerDownCustom;
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                eventData.Use();
                return;
            }

            base.OnUpdateSelected(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            onPointerDownCustom?.Invoke(eventData);
        }

    }
}