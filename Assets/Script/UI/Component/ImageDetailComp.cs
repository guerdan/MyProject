
using System;
using System.Collections.Generic;
using Script.Framework.AssetLoader;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    [Serializable]
    public class ImageDetailCompFloat
    {
        [SerializeField] public RectTransform SelfRT;
        [SerializeField] public Text PosT;
        [SerializeField] public Text RT;
        [SerializeField] public Text GT;
        [SerializeField] public Text BT;
        [SerializeField] public Image ColorImage;

    }
    /// <summary>
    /// 算出Image大小，再改变自身大小
    /// </summary>
    public class ImageDetailComp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] public ScrollRect ScrollRect;
        [SerializeField] public Image Image;
        [SerializeField] public RectTransform LineParent;
        [SerializeField] public GameObject LinePre;
        [SerializeField] private Text SizeText;
        [SerializeField] private SquareFrameUI SelectPixelUI;             // 选中像素点的框
        [SerializeField] private ImageDetailCompFloat Float;            // 像素点的浮窗
        RectTransform _contentRT;   // Content的RectTransform
        RectTransform _imageRT;     // 图片的RectTransform
        Sprite TransparentSprite;
        string _path;
        float _edgeLen;             // 视区边长
        Vector2 _imageSize;         // texture尺寸大小

        float _viewW;               // 视区宽
        float _viewH;               // 视区高
        float _contentW;            // Content宽
        float _contentH;            // Content高
        float _imageW;              // Image组件宽
        float _imageH;              // Image组件高

        float _sizeScale;           // 图片放大倍数
        float MinScale;             // 最小缩放比例，是能完全显示图片
        float MaxScale;             // 最大缩放比例，_edgeLenX_edgeLen的视窗下显示 10X10个像素，_edgeLen/10=30
        float ScaleStep = 1.1f;     // 步长, 次方的底数

        bool showLine => _sizeScale >= 12;

        List<RectTransform> _verticalLines = new List<RectTransform>();        // 参考线实例缓存
        List<RectTransform> _horizonLines = new List<RectTransform>();        // 参考线实例缓存

        Vector2 _cursor_down_pos;
        Vector2 _select_pixel_pos;   // 选中像素点的位置, 以左下角为原点

        Action<float> _onScaleChange;
        Action<Vector2> _onScroll;
        Action<Vector2> _onSelectPixel;
        void Awake()
        {
            _edgeLen = 500;
            TransparentSprite = Image.sprite;
            var viewSize = ScrollRect.viewport.rect.size;
            _viewW = viewSize.x;
            _viewH = viewSize.y;
            _contentRT = ScrollRect.content;
            MaxScale = _edgeLen / 10f;
            _imageRT = Image.GetComponent<RectTransform>();
            _imageRT.anchoredPosition = default;
            LinePre.SetActive(false);
            SelectPixelUI.gameObject.SetActive(false);
            Float.SelfRT.gameObject.SetActive(false);

            ScrollRect.onValueChanged.AddListener(OnScroll);
        }



        public void SetData(string path, Action<float> onScaleChange = null
            , Action<Vector2> onScroll = null, Action<Vector2> onSelectPixel = null)
        {
            if (!ImageManager.Inst.TryLoadSprite(path, Image, out var spr))
            {
                Image.sprite = TransparentSprite;
                if (SizeText) SizeText.text = "";
                return;
            }
            _onScaleChange = onScaleChange;
            _onScroll = onScroll;
            _onSelectPixel = onSelectPixel;

            _path = path;

            spr.texture.filterMode = FilterMode.Point;
            Image.sprite = spr;
            _imageSize = new Vector2(spr.rect.width, spr.rect.height);
            if (SizeText) SizeText.text = string.Format("{0} * {1}", (int)_imageSize.x, (int)_imageSize.y);


            MinScale = Math.Min(_edgeLen / _imageSize.x, _edgeLen / _imageSize.y);
            MinScale = Math.Min(MinScale, MaxScale);
            _sizeScale = MinScale;
            _select_pixel_pos = new Vector2(-1, -1);

            RefreshScale();
            ScrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);
        }

        public void Change(Action<float> onScaleChange
            , Action<Vector2> onScroll, Action<Vector2> onSelectPixel)
        {
            _onScaleChange = onScaleChange;
            _onScroll = onScroll;
            _onSelectPixel = onSelectPixel;
        }

        void Update()
        {
            // 检测鼠标滚轮滚动
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // 滚轮有滚动
            if (Mathf.Abs(scroll) > 0.01f && Utils.IsPointerOverUIObject(gameObject, Root.Inst.Canvas))
            {
                // 调整 _sizeScale
                _sizeScale *= scroll > 0 ? ScaleStep : 1 / ScaleStep;
                _sizeScale = Mathf.Clamp(_sizeScale, MinScale, MaxScale); // 限制范围
                RefreshScale();
                _onScaleChange?.Invoke(_sizeScale);
            }
        }

        void RefreshScale()
        {
            var size = _imageSize * _sizeScale;
            _imageW = size.x;
            _imageH = size.y;
            _contentH = _imageH + _viewH;
            _contentW = _imageW + _viewW;


            _imageRT.sizeDelta = size;
            var old_normal_pos = ScrollRect.normalizedPosition;
            _contentRT.sizeDelta = new Vector2(_contentW, _contentH);
            // 真巧，算出来就是old_normal_pos
            ScrollRect.normalizedPosition = old_normal_pos;

            // RefreshLine();
        }

        #region  Line

        void RefreshLine()
        {

            var normalPos = ScrollRect.normalizedPosition;
            // DU.Log($"normal {ScrollRect.normalizedPosition}");
            if (showLine)
            {
                var spacing = _sizeScale;
                // 视窗的左下角在Content(以左下角为原点)下的坐标
                //
                var x = normalPos.x * (_contentW - _viewW);
                var y = normalPos.y * (_contentH - _viewH);


                {   // 绘制竖线
                    var del = 0f;
                    var startXIndex = 0;
                    if (x - _viewW / 2 > 0)
                    {
                        del = (x - _viewW / 2) % spacing;
                        startXIndex = (int)Math.Ceiling((x - _viewW / 2 - del) / spacing);
                    }

                    var endXIndex = (int)_imageSize.x - 1;
                    if (x + _viewW / 2 < _imageW)
                    {
                        del = (x + _viewW / 2) % spacing;
                        endXIndex = (int)Math.Floor((x + _viewW / 2 - del) / spacing);
                    }

                    var count = endXIndex - startXIndex + 1;
                    Utils.RefreshItemListByCount<RectTransform>(_verticalLines, count, LinePre, LineParent, (rect, i) =>
                        {
                            rect.sizeDelta = new Vector2(1, _imageH);
                            rect.anchorMin = new Vector2(0, 0.5f);
                            rect.anchorMax = new Vector2(0, 0.5f);
                            rect.anchoredPosition = new Vector2((startXIndex + i) * spacing + _viewW / 2, 0);
                        });
                }

                {   // 绘制横线
                    var del = 0f;
                    var startYIndex = 0;
                    if (y - _viewH / 2 > 0)
                    {
                        del = (y - _viewH / 2) % spacing;
                        startYIndex = (int)Math.Ceiling((y - _viewH / 2 - del) / spacing);
                    }

                    var endYIndex = (int)_imageSize.y - 1;
                    if (y + _viewH / 2 < _imageH)
                    {
                        del = (y + _viewH / 2) % spacing;
                        endYIndex = (int)Math.Floor((y + _viewH / 2 - del) / spacing);
                    }

                    var count = endYIndex - startYIndex + 1;
                    Utils.RefreshItemListByCount<RectTransform>(_horizonLines, count, LinePre, LineParent, (rect, i) =>
                        {
                            rect.sizeDelta = new Vector2(_imageW, 1);
                            rect.anchorMin = new Vector2(0.5f, 0);
                            rect.anchorMax = new Vector2(0.5f, 0);
                            rect.anchoredPosition = new Vector2(0, (startYIndex + i) * spacing + _viewH / 2);
                        });

                }
            }
            else
            {
                Utils.RefreshItemListByCount<RectTransform>(_verticalLines, 0, LinePre, LineParent, null);
                Utils.RefreshItemListByCount<RectTransform>(_horizonLines, 0, LinePre, LineParent, null);
            }
        }

        void OnScroll(Vector2 cur)
        {
            // DU.Log($"OnScroll {cur}");

            RefreshLine();
            RefreshSelectPixel();
            _onScroll?.Invoke(cur);
        }

        #endregion


        #region  SelectPixel
        void RefreshSelectPixel()
        {
            if (_select_pixel_pos.x < 0)
                return;

            var spacing = _sizeScale;
            var normalPos = ScrollRect.normalizedPosition;
            // 参照的是Content的左下角为原点
            var x = _viewW / 2 + _select_pixel_pos.x * spacing + spacing / 2;
            var y = _viewH / 2 + _select_pixel_pos.y * spacing + spacing / 2;
            var in_range = x > normalPos.x * (_contentW - _viewW) && x < normalPos.x * (_contentW - _viewW) + _viewW
                 && y > normalPos.y * (_contentH - _viewH) && y < normalPos.y * (_contentH - _viewH) + _viewH;

            if (!showLine || _select_pixel_pos.x < 0 || !in_range)
            {
                Clear();
                return;
            }


            SelectPixelUI.gameObject.SetActive(true);
            SelectPixelUI.transform.SetParent(ScrollRect.content);
            var pos = new Vector2(
               _viewW / 2 + _select_pixel_pos.x * spacing - _contentW / 2 + spacing / 2,
               _viewH / 2 + _select_pixel_pos.y * spacing - _contentH / 2 + spacing / 2);

            var size = new Vector2(spacing, spacing);
            SelectPixelUI.SetData(pos, size, Color.white);

            Float.SelfRT.gameObject.SetActive(true);
            // Float.SelfRT.SetParent(ScrollRect.content);
            var floatRT = Float.SelfRT;
            var float_pos = pos + new Vector2(0, -spacing / 2 - floatRT.rect.height / 2 - 10);
            Float.SelfRT.anchoredPosition = float_pos + ScrollRect.content.anchoredPosition;

            var tex = Image.sprite.texture;
            var color = tex.GetPixel((int)_select_pixel_pos.x, (int)_select_pixel_pos.y);
            var show_y = _imageSize.y - 1 - _select_pixel_pos.y;
            Float.PosT.text = $"（{(int)_select_pixel_pos.x}   ,   {(int)show_y}）";
            Float.RT.text = $"{(int)(color.r * 255)} , ";
            Float.GT.text = $"{(int)(color.g * 255)} , ";
            Float.BT.text = $"{(int)(color.b * 255)}";
            Float.ColorImage.color = color;
        }


        public void OnPointerDown(PointerEventData eventData)
        {
            // 这个接口按pivot为原点，所以是画布(0,1)为原点下的坐标

            RectTransformUtility.ScreenPointToLocalPointInRectangle(ScrollRect.content, eventData.position
                , eventData.pressEventCamera, out _cursor_down_pos);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!showLine) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ScrollRect.content, eventData.position
                , eventData.pressEventCamera, out localPoint);

            if ((_cursor_down_pos - localPoint).magnitude > 12)  // 移动了，不算点击
                return;

            var spacing = _sizeScale;
            float x = (localPoint.x + _contentW / 2 - _viewW / 2) / spacing;
            float y = (localPoint.y + _contentH / 2 - _viewH / 2) / spacing;
            if (x >= 0 && x < _imageSize.x && y >= 0 && y < _imageSize.y)
            {
                _select_pixel_pos = new Vector2((int)x, (int)y);
                RefreshSelectPixel();
                _onSelectPixel?.Invoke(_select_pixel_pos);
            }
            else
            {
                Clear();
            }
        }

        #endregion

        void Clear()
        {
            SelectPixelUI.gameObject.SetActive(false);
            Float.SelfRT.gameObject.SetActive(false);
        }

        public void ScaleTo(float scale)
        {
            if (string.IsNullOrEmpty(_path)) return;
            _sizeScale = Mathf.Clamp(scale, MinScale, MaxScale);
            RefreshScale();
        }
        public void ScrollTo(Vector2 normalizedPos)
        {
            if (string.IsNullOrEmpty(_path)) return;
            ScrollRect.normalizedPosition = normalizedPos;
        }

        public void SelectPixel(Vector2 pixel)
        {
            if (string.IsNullOrEmpty(_path)) return;

            if (pixel.x >= 0 && pixel.x < _imageSize.x && pixel.y >= 0 && pixel.y < _imageSize.y)
            {
                _select_pixel_pos = pixel;
                RefreshSelectPixel();
            }
        }
        /// <summary>
        /// 图片大小
        /// </summary>
        public Vector2 GetSize()
        {
            return _imageSize;
        }
    }
}