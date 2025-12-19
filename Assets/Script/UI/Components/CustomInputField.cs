using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Script.UI.Components
{
    public class CustomInputField : InputField
    {
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

    }
}