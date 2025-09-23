
using System.Collections.Generic;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// 算出Image大小，再改变自身大小
    /// </summary>
    public class ImageLoadComp : MonoBehaviour
    {
        [SerializeField] public Image Image;
        [SerializeField] private float Border = 3;
        [SerializeField] private Text SizeText;
        [SerializeField] private Button Btn;


        private Sprite TransparentSprite;
        private Vector2 _ori_size;
        private Vector2 _size;
        private string _path;
        private bool _canClick;
        private float _scale;

        void Awake()
        {
            TransparentSprite = Image.sprite;
            Btn?.onClick.AddListener(OnClick);
            _ori_size = GetComponent<RectTransform>().sizeDelta;
            _ori_size = new Vector2(_ori_size.x - 2 * Border, _ori_size.y - 2 * Border);
        }

        public void SetData(string path, Vector2 max_size = default, bool canClick = true
                            , float preferred_scale = 1)
        {
            if (!ImageManager.Inst.TryLoadSprite(path, Image, out var spr))
            {
                GetComponent<RectTransform>().sizeDelta = _ori_size;
                Image.sprite = TransparentSprite;
                Image.raycastTarget = false;
                if (SizeText) SizeText.text = "";
                return;
            }

            _path = path;
            _canClick = canClick;

            Image.sprite = spr;
            Image.raycastTarget = true;
            float w = spr.rect.width;
            float h = spr.rect.height;
            if (max_size != default)
            {
                float scale = Mathf.Min(max_size.x / w, max_size.y / h);
                if (scale < 1)
                {
                    w *= scale;
                    h *= scale;
                    _scale = scale;
                }
                else if (scale > preferred_scale)
                {
                    w *= preferred_scale;
                    h *= preferred_scale;
                    _scale = preferred_scale;
                }
                else
                {
                    _scale = 1;
                }
            }

            GetComponent<RectTransform>().sizeDelta = new Vector2(w + 2 * Border, h + 2 * Border);
            _size = new Vector2(w, h);
            if (SizeText) SizeText.text = string.Format("{0} * {1}", (int)spr.rect.width, (int)spr.rect.height);
        }


        /// <summary>
        /// 图片大小
        /// </summary>
        public Vector2 GetSize()
        {
            if (_size == default)
                return _ori_size;
            else
                return _size;
        }

        public float GetScale()
        {
            return _scale;
        }



        void OnClick()
        {
            if (!_canClick) return;
            UIManager.Inst.ShowPanel(PanelEnum.ImageInfoPanel, _path);
        }


        // public void OnPointerDown(PointerEventData eventData)
        // {
        //     if (!_canClick)
        //     {
        //         // 将事件传递给下层对象
        //         PassEvent(eventData, ExecuteEvents.pointerDownHandler);
        //         return;
        //     }
        // }

        // public void OnPointerUp(PointerEventData eventData)
        // {
        //     if (!_canClick)
        //     {
        //         // 将事件传递给下层对象
        //         PassEvent(eventData, ExecuteEvents.pointerDownHandler);
        //         return;
        //     }
        //     if (RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera))
        //         OnClick();
        // }

        // // 将事件传递给下层对象的方法，还不完整
        // private void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> function) where T : IEventSystemHandler
        // {
        //     var results = new List<RaycastResult>();
        //     EventSystem.current.RaycastAll(data, results);
        //     foreach (var result in results)
        //     {
        //         if (result.gameObject == gameObject) continue; // 跳过当前对象，要排除它的子节点列表
        //         ExecuteEvents.Execute(result.gameObject, data, function);
        //         break;
        //     }
        // }
    }
}