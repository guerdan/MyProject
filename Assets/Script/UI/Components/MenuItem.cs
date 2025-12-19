
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{
    public class MenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image SelectedBg;
        [SerializeField] private GameObject MenuIcon;
        [SerializeField] private Text Text;
        MenuSystem _system;
        MenuComp _parent;
        MenuStrPack _data;
        int _index;


        public void SetData(MenuSystem system, MenuComp parent, MenuStrPack data, int index, float width)
        {
            _system = system;
            _parent = parent;
            _data = data;
            _index = index;

            Text.text = _data.Name;
            Utils.SetActive(MenuIcon, _data.IsMenu);
            var rect = GetComponent<RectTransform>();
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            Refresh();
        }

        public void Refresh()
        {
            Utils.SetActive(SelectedBg, _index == _system.HoverRecord[_data.Level - 1]);
        }

        //鼠标悬浮进入事件
        public void OnPointerEnter(PointerEventData eventData)
        {
            _system.ChangeHoverRecord(_data.Level, _index);

            if (_data.IsMenu)
                _system.Hover(_data);

        }

        //鼠标悬浮离开事件
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_data.IsMenu)
                _system.ChangeHoverRecord(_data.Level, -1);
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
            if (!_data.IsMenu)
                _system.Click(_data);

        }
    }
}