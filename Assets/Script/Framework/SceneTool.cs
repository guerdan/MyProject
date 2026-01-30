
using UnityEngine;
using UnityEngine.UI;

namespace Script.Framework
{
    public class SceneTool : MonoBehaviour
    {
        protected static SceneTool inst;
        public static SceneTool Inst { get { return inst; } }

        [SerializeField] private Text ToolText;

        RectTransform _toolTextR;

        protected void Awake()
        {
            inst = this;
            _toolTextR = ToolText.GetComponent<RectTransform>();
        }

        public float GetTextPreferHeight(float width, int fontSize,string content)
        {
            _toolTextR.sizeDelta = new Vector2(width, _toolTextR.sizeDelta.y);
            ToolText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ToolText.fontSize = fontSize;
            ToolText.text = content;
            return ToolText.preferredHeight;
        }
        public float GetTextPreferWidth(int fontSize,string content)
        {
            ToolText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ToolText.fontSize = fontSize;
            ToolText.text = content;
            return ToolText.preferredWidth;
        }

    }
}