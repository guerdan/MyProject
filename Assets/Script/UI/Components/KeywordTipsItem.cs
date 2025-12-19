
using System;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{
    public class KeywordTipsItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image SelectedBg;
        [SerializeField] private Text Text;

        string _str;
        int _index;
        Action<int, bool> _onSelect;
        KeywordTipsComp _parent;
        // 多级菜单中，如果悬浮在一个项时，只有进入到另个项才会取消悬浮状态。
        bool _hover;

        public void SetData(string str, int index, Action<int, bool> onSelect, float width, KeywordTipsComp parent)
        {
            _index = index;
            _onSelect = onSelect;
            _parent = parent;
            _hover = false;

            _str = str;
            Text.text = str;

            var rect = GetComponent<RectTransform>();
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            Refresh();
        }

        public void Refresh()
        {
            if (_parent.UseSelectColor && _index == _parent.SelectIndex)
            {
                Utils.SetActive(SelectedBg, true);
            }
            else if (_hover)
            {
                Utils.SetActive(SelectedBg, true);
            }
            else
            {
                Utils.SetActive(SelectedBg, false);

            }
        }

        //鼠标悬浮进入事件
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            Refresh();
        }

        //鼠标悬浮离开事件
        public void OnPointerExit(PointerEventData eventData)
        {

            _hover = false;
            Refresh();
        }

        public void ChangeHoverStatus()
        {

        }
        //鼠标按下事件
        public void OnPointerDown(PointerEventData eventData)
        {

        }

        //鼠标抬起事件
        public void OnPointerUp(PointerEventData eventData)
        {
            _onSelect?.Invoke(_index, true);
        }
    }
}