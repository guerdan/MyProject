
using System;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    public class KeywordTipsItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image Bg;
        [SerializeField] private Text Text;

        string _str;
        int _index;
        Action<int> _onSelect;
        KeywordTipsComp _parent;

        public void SetData(string str, int index, Action<int> onSelect, KeywordTipsComp parent)
        {
            _str = str;
            _index = index;
            _onSelect = onSelect;
            _parent = parent;
            Text.text = str;
        }

        public void Refresh()
        {
            if (_index == _parent.SelectIndex)
            {
                Bg.color = Utils.ParseHtmlString("#DDDDDD");
                Text.color = Utils.ParseHtmlString("#494949");
            }
            else
            {
                Bg.color = new Color(0, 0, 0, 0);
                Text.color = Utils.ParseHtmlString("#878787");
            }
        }

        //鼠标悬浮进入事件
        public void OnPointerEnter(PointerEventData eventData)
        {
            _parent.SelectIndex = _index;
            _parent.Refresh();
        }

        //鼠标悬浮离开事件
        public void OnPointerExit(PointerEventData eventData)
        {
           
        }
        //鼠标按下事件
        public void OnPointerDown(PointerEventData eventData)
        {

        }

        //鼠标抬起事件
        public void OnPointerUp(PointerEventData eventData)
        {
            _onSelect?.Invoke(_index);
        }
    }
}