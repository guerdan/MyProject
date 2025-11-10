
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
    /// 参考坐标系是 左下角为原点，X轴向右，Y轴向上
    /// </summary>
    public class ImageDetailComp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Sprite DefaultSprite;
        [SerializeField] public ScrollRect ScrollRect;
        [SerializeField] public Image Image;
        [SerializeField] public RectTransform LineParent;
        [SerializeField] public RectTransform ColorParent;
        [SerializeField] public GameObject LinePre;
        [SerializeField] private Text SizeText;
        [SerializeField] private SquareFrameUI SelectPixelUI;             // 选中像素点的框
        [SerializeField] private ImageDetailCompFloat Float;            // 像素点的浮窗
        RectTransform _contentRT;   // Content的RectTransform
        RectTransform _imageRT;     // 图片的RectTransform
        Sprite TransparentSprite;
        string _path;
        float _edgeLen;             // 视区边长
        Vector2Int _imageSize;         // texture尺寸大小

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

        bool showLine => _sizeScale >= 12;          //显示参考线和选中像素的“缩放”临界点

        int startXIndex;            // 参考线X索引
        int endXIndex;              // 参考线X索引
        int startYIndex;            // 参考线Y索引
        int endYIndex;              // 参考线Y索引


        List<RectTransform> _verticalLines = new List<RectTransform>();         // 参考线实例缓存
        List<RectTransform> _horizonLines = new List<RectTransform>();          // 参考线实例缓存
        List<RectTransform> _verticalLines1 = new List<RectTransform>();         // 参考线实例缓存
        List<RectTransform> _horizonLines1 = new List<RectTransform>();          // 参考线实例缓存
        List<RectTransform> _verticalLines2 = new List<RectTransform>();         // 参考线实例缓存
        List<RectTransform> _horizonLines2 = new List<RectTransform>();          // 参考线实例缓存
        List<SquareFrameUI> _colorPixelObjs = new List<SquareFrameUI>();           // 颜色格子实例缓存

        Sprite _spr;
        Vector2Int _line_offset;
        Vector2 _cursor_down_pos;
        Vector2Int _select_pixel_pos;                      // 选中像素点的位置, 以左下角为原点

        Action<float> _onScaleChange;
        Action<Vector2> _onScroll;
        Action<Vector2Int> _onSelectPixel;
        // Dic<哪些格子，涂的颜色>
        Color[,] _colorData;
        Color[,] colorData
        {
            get
            {
                if (_colorData == null)
                {
                    _colorData = new Color[_imageSize.x, _imageSize.y];
                }
                return _colorData;
            }
        }

        Dictionary<Color, (int, float)> _colorIndex = new Dictionary<Color, (int, float)>();


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
            SelectPixelUI.gameObject.SetActive(false);
            Float.SelfRT.gameObject.SetActive(false);

            LinePre.SetActive(false);

            ScrollRect.onValueChanged.AddListener(OnScroll);
        }



        public void SetData(string path, string name = "")
        {
            if (!ImageManager.Inst.TryLoadSprite(path, Image, out var spr, true, true))
            {
                Image.sprite = TransparentSprite;
                if (SizeText) SizeText.text = "";
                return;
            }
            _path = path;
            SetData(spr, name);
        }

        public void SetData(Sprite spr, string name = "")
        {
            _spr = spr;
            spr.texture.filterMode = FilterMode.Point;
            Image.sprite = spr;
            _imageSize = new Vector2Int(spr.texture.width, spr.texture.height);
            if (SizeText)
            {
                var str = string.Format($"{_imageSize.x} * {_imageSize.y}");
                if (!string.IsNullOrEmpty(name))
                    str = $"{name} — " + str;
                SizeText.text = str;
            }


            MinScale = Math.Min(_edgeLen / _imageSize.x, _edgeLen / _imageSize.y);
            MinScale = Math.Min(MinScale, MaxScale);
            _sizeScale = MinScale;
            _select_pixel_pos = new Vector2Int(-1, -1);

            RefreshScale();
            ScrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);
        }

        public void SetLineOffset(Vector2Int offset)
        {
            _line_offset = offset;
        }


        public void ClearData()
        {
            _spr = null;
            Image.sprite = DefaultSprite;
            _imageSize = default;
            if (SizeText) SizeText.text = " * ";

            _sizeScale = 1;
            _select_pixel_pos = new Vector2Int(-1, -1);
            ScrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);

        }

        public void SyncOnScaleChange(Action<float> onScaleChange)
        {
            _onScaleChange = onScaleChange;

        }
        public void SyncOnScroll(Action<Vector2> onScroll)
        {
            _onScroll = onScroll;

        }
        public void SyncOnSelectPixel(Action<Vector2Int> onSelectPixel)
        {
            _onSelectPixel = onSelectPixel;

        }


        void Update()
        {
            if (_spr == null)
                return;

            // 检测鼠标滚轮滚动
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // 滚轮有滚动
            if (Mathf.Abs(scroll) > 0.01f && Utils.IsPointerOverUIObject(gameObject, Root.Inst.Canvas))
            {
                DoScale(scroll > 0);
            }

            if (Input.GetKeyDown(KeyCode.Equals))
            {
                DoScale(true);
            }

            if (Input.GetKeyDown(KeyCode.Minus))
            {
                DoScale(false);
            }
        }

        void DoScale(bool positive)
        {
            // 调整 _sizeScale
            _sizeScale *= positive ? ScaleStep : 1 / ScaleStep;
            _sizeScale = Mathf.Clamp(_sizeScale, MinScale, MaxScale); // 限制范围
            RefreshScale();
            _onScaleChange?.Invoke(_sizeScale);
        }

        void RefreshScale()
        {
            var size = new Vector2(_imageSize.x * _sizeScale, _imageSize.y * _sizeScale);
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
            RefreshLine(1, Vector2Int.zero, _verticalLines, _horizonLines, new Color(1, 1, 1, 0.25f), 1);
            RefreshLine(5, _line_offset, _verticalLines1, _horizonLines1, new Color(0, 1, 0, 0.3f), 2);
            // RefreshLine(20, _line_offset, _verticalLines2, _horizonLines2, new Color(1, 0, 0, 0.6f), 2, false);
        }
        void RefreshLine(int pixel_interval, Vector2Int line_offset, List<RectTransform> verticalList, List<RectTransform> horizonList
            , Color color, float thickness = 1, bool when_show_line = true)
        {
            var spacing = pixel_interval * _sizeScale;
            var offset = new Vector2(line_offset.x * _sizeScale, line_offset.y * _sizeScale); //大格子的偏移

            var normalPos = ScrollRect.normalizedPosition;
            // DU.Log($"normal {ScrollRect.normalizedPosition}");
            if (!when_show_line || showLine)
            {
                // 视窗的左下角在Content(以左下角为原点)下的坐标
                //
                var x = normalPos.x * (_contentW - _viewW);
                var y = normalPos.y * (_contentH - _viewH);


                {   // 绘制竖线
                    var del = 0f;
                    startXIndex = 0;
                    if (x - _viewW / 2 > 0)
                    {
                        del = (x - _viewW / 2) % spacing;
                        startXIndex = (int)Math.Floor((x - _viewW / 2 - del) / spacing);
                    }

                    endXIndex = _imageSize.x / pixel_interval;
                    if (x + _viewW / 2 < _imageW)
                    {
                        del = (x + _viewW / 2) % spacing;
                        endXIndex = (int)Math.Floor((x + _viewW / 2 - del) / spacing);
                    }

                    var count = endXIndex - startXIndex + 1 + 1;  //末尾是因为offset而加, offset是负的

                    Utils.RefreshItemListByCount<RectTransform>(verticalList, count, LinePre, LineParent, (rect, i) =>
                        {
                            rect.GetComponent<Image>().color = color;
                            rect.sizeDelta = new Vector2(thickness, _imageH);
                            rect.anchorMin = new Vector2(0, 0.5f);
                            rect.anchorMax = new Vector2(0, 0.5f);
                            rect.anchoredPosition = new Vector2((startXIndex + i) * spacing + _viewW / 2 + offset.x, 0);
                        });
                }

                {   // 绘制横线
                    var del = 0f;
                    startYIndex = 0;
                    if (y - _viewH / 2 > 0)
                    {
                        del = (y - _viewH / 2) % spacing;
                        startYIndex = (int)Math.Floor((y - _viewH / 2 - del) / spacing);
                    }

                    endYIndex = _imageSize.y / pixel_interval;
                    if (y + _viewH / 2 < _imageH)
                    {
                        del = (y + _viewH / 2) % spacing;
                        endYIndex = (int)Math.Floor((y + _viewH / 2 - del) / spacing);
                    }

                    var count = endYIndex - startYIndex + 1 + 1;
                    Utils.RefreshItemListByCount<RectTransform>(horizonList, count, LinePre, LineParent, (rect, i) =>
                        {
                            rect.GetComponent<Image>().color = color;
                            rect.sizeDelta = new Vector2(_imageW, thickness);
                            rect.anchorMin = new Vector2(0.5f, 0);
                            rect.anchorMax = new Vector2(0.5f, 0);
                            rect.anchoredPosition = new Vector2(0, (startYIndex + i) * spacing + _viewH / 2 + offset.y);
                        });

                }
            }
            else
            {
                Utils.RefreshItemListByCount<RectTransform>(verticalList, 0, LinePre, LineParent, null);
                Utils.RefreshItemListByCount<RectTransform>(horizonList, 0, LinePre, LineParent, null);
            }
        }

        void OnScroll(Vector2 cur)
        {
            // DU.Log($"OnScroll {cur}");

            if (_spr == null)
                return;

            RefreshLine();
            RefreshSelectPixel();
            RefreshPixelColor();
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
            var pos = new Vector2(x, y);
            var size = new Vector2(spacing, spacing);
            SelectPixelUI.SetAnchor(new Vector2(0, 0));
            SelectPixelUI.SetData(pos, size, Color.white);

            Float.SelfRT.gameObject.SetActive(true);
            // Float.SelfRT.SetParent(ScrollRect.content);
            // 父节点与SelectPixelUI的父节点不同
            //
            var floatRT = Float.SelfRT;
            var float_pos = pos + new Vector2(0, -spacing / 2 - floatRT.rect.height / 2 - 10)
                + new Vector2(-_contentW / 2, -_contentH / 2);
            Float.SelfRT.anchoredPosition = float_pos + ScrollRect.content.anchoredPosition;

            var tex = Image.sprite.texture;
            var color = tex.GetPixel((int)_select_pixel_pos.x, (int)_select_pixel_pos.y);
            Float.PosT.text = $"（{(int)_select_pixel_pos.x}   ,   {(int)_select_pixel_pos.y}）";
            Float.RT.text = $"{(int)(color.r * 255)} , ";
            Float.GT.text = $"{(int)(color.g * 255)} , ";
            Float.BT.text = $"{(int)(color.b * 255)}";
            Float.ColorImage.color = color;
        }
        #endregion

        #region  DrawColor

        /// <summary>
        /// index可选项为0,1,2
        /// </summary>
        public void SetPixelColor(List<Vector2Int> pixels, Color color, int index = 0, float border_width = 1)
        {
            foreach (var pos in pixels)
            {
                if (pos.x < 0 || pos.x >= _imageSize.x || pos.y < 0 || pos.y >= _imageSize.y)
                    continue;
                colorData[pos.x, pos.y] = color;
            }
            _colorIndex[color] = (index, border_width);
            RefreshPixelColor();
        }

        public void ClearPixelColor(Color color)
        {
            for (int i = 0; i < _imageSize.y; i++)
            {
                for (int j = 0; j < _imageSize.x; j++)
                {
                    var c = colorData[j, i];
                    if (c == color)
                        colorData[j, i] = default;
                }
            }
            _colorIndex.Remove(color);
            RefreshPixelColor();
        }
        void RefreshPixelColor()
        {
            if (!showLine)
            {
                Utils.RefreshItemListByCount(_colorPixelObjs, 0, SelectPixelUI.gameObject
                , ScrollRect.content, null);
                return;
            }
            // 格子颜色列表
            var pixels = new List<Vector2>();
            for (int i = startYIndex; i <= endYIndex; i++)
            {
                for (int j = startXIndex; j <= endXIndex; j++)
                {
                    var color = colorData[j, i];
                    if (color != default)
                        pixels.Add(new Vector2(j, i));
                }
            }


            Utils.RefreshItemListByCount<SquareFrameUI>(_colorPixelObjs, pixels.Count, SelectPixelUI.gameObject
                , ColorParent, (item, i) =>
                {
                    var indexPos = pixels[i];
                    var x = (int)indexPos.x;
                    var y = (int)indexPos.y;
                    var color = colorData[x, y];
                    var spacing = _sizeScale;
                    var sort_info = _colorIndex[color];
                    item.transform.SetParent(ColorParent.GetChild(sort_info.Item1));

                    var size = new Vector2(spacing, spacing);
                    item.SetAnchor(new Vector2(0, 0));
                    var pos = new Vector2(
                        _viewW / 2 + indexPos.x * spacing + spacing / 2,
                        _viewH / 2 + indexPos.y * spacing + spacing / 2);

                    var border_width = Mathf.Min(4, _sizeScale * 0.2f);
                    item.SetData(pos, size, color, border_width * sort_info.Item2);
                    int show_border = 0;
                    int x_end = _imageSize.x - 1;
                    int y_end = _imageSize.y - 1;


                    if (y == 0 || colorData[x, y - 1] != color)
                        show_border |= 1;
                    if (x == 0 || colorData[x - 1, y] != color)
                        show_border |= 2;
                    if (y == y_end || colorData[x, y + 1] != color)
                        show_border |= 4;
                    if (x == x_end || colorData[x + 1, y] != color)
                        show_border |= 8;
                    if (y == 0 || x == 0 || colorData[x - 1, y - 1] != color)
                        show_border |= 16;
                    if (y == y_end || x == 0 || colorData[x - 1, y + 1] != color)
                        show_border |= 32;
                    if (y == y_end || x == x_end || colorData[x + 1, y + 1] != color)
                        show_border |= 64;
                    if (y == 0 || x == x_end || colorData[x + 1, y - 1] != color)
                        show_border |= 128;

                    item.ShowBorder(show_border);
                });
        }
        #endregion

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
                _select_pixel_pos = new Vector2Int((int)x, (int)y);
                RefreshSelectPixel();
                _onSelectPixel?.Invoke(_select_pixel_pos);
            }
            else
            {
                Clear();
            }
        }


        void Clear()
        {
            SelectPixelUI.gameObject.SetActive(false);
            Float.SelfRT.gameObject.SetActive(false);
        }

        public void ScaleTo(float scale)
        {
            if (Image.sprite == null) return;
            _sizeScale = Mathf.Clamp(scale, MinScale, MaxScale);
            RefreshScale();
        }
        public void ScrollTo(Vector2 normalizedPos)
        {
            if (Image.sprite == null) return;
            ScrollRect.normalizedPosition = normalizedPos;
        }

        public void SelectPixel(Vector2Int pixel)
        {
            if (Image.sprite == null) return;
            if (pixel.x >= 0 && pixel.x < _imageSize.x && pixel.y >= 0 && pixel.y < _imageSize.y)
            {
                _select_pixel_pos = pixel;
                RefreshSelectPixel();
            }
        }
        /// <summary>
        /// 图片大小
        /// </summary>
        public Vector2Int GetSize()
        {
            return _imageSize;
        }
    }
}