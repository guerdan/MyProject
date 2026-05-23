
using System;
using System.Collections.Generic;
using OpenCvSharp.Aruco;
using Script.Framework;
using Script.Framework.AssetLoader;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{
    [Serializable]
    public class ImageDetailCompFloat
    {
        [SerializeField] public RectTransform SelfRT;
        [SerializeField] public Image ColorImage;
        [SerializeField] public Text PosT;
        [SerializeField] public Text RT;        // RedText
        [SerializeField] public Text GT;
        [SerializeField] public Text BT;
        [SerializeField] public Text CRT;       // ConvolutionRedText
        [SerializeField] public Text CGT;
        [SerializeField] public Text CBT;

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
        [SerializeField] public Image MaskImage;
        [SerializeField] public RectTransform LineParent;
        [SerializeField] public GameObject LinePre;                         // 辅助线预制件
        [SerializeField] private Text SizeText;
        [SerializeField] private SquareFrameUI SelectPixelUI;               // 选中像素点的框
        [SerializeField] private ImageDetailCompFloat Float;                // 像素点的浮窗

        [SerializeField] public RectTransform TextParent;
        [SerializeField] private GameObject TextPre;                        // 文本预制件
        public bool IsEmpty => Image.sprite == DefaultSprite;

        [HideInInspector] public string _path;                              // 图片路径
        RectTransform _contentRT;   // Content的RectTransform
        RectTransform _imageRT;     // 图片的RectTransform
        RectTransform _maskRT;     // 图片的RectTransform
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

        bool showLine => _sizeScale >= 12;                  //显示参考线和选中像素的“缩放”临界点
        bool showText => _sizeScale >= _textUI_config.Item2;    //显示参考线和选中像素的“缩放”临界点

        int startXIndex;            // 参考线X索引
        int endXIndex;              // 参考线X索引
        int startYIndex;            // 参考线Y索引
        int endYIndex;              // 参考线Y索引

        Sprite _maskSpr;            // mask，给 Image 进行遮罩。组件内部维护
        Vector2Int _line_offset;
        Vector2 _cursor_down_pos;
        Vector2Int _select_pixel_pos;                      // 选中像素点的位置, 以左下角为原点

        Action<float> _onScaleChange;
        Action<Vector2> _onScroll;
        Action<Vector2Int> _onSelectPixel;

        (int, float, Color) _textUI_config;

        SPool _lineUIPool;
        SPool _textUIPool;

        // 辅助线实例字典。一种像素间距作为一类。
        Dictionary<int, List<GameObject>> _lineUIs = new Dictionary<int, List<GameObject>>();
        // <组件，像素位置>
        List<(GameObject, string, Vector2Int)> _textUIs = new List<(GameObject, string, Vector2Int)>();

        void Awake()
        {
            var viewSize = ScrollRect.viewport.rect.size;
            _viewW = viewSize.x;
            _viewH = viewSize.y;

            // _edgeLen = 500;
            _edgeLen = _viewW;
            _contentRT = ScrollRect.content;
            MaxScale = _edgeLen / 10f;
            _imageRT = Image.GetComponent<RectTransform>();
            _imageRT.anchoredPosition = default;
            _maskRT = MaskImage.GetComponent<RectTransform>();
            _maskRT.anchoredPosition = default;

            ClearPixInfoFloat();
            Utils.SetActive(LinePre, false);
            Utils.SetActive(TextPre, false);
            _lineUIPool = new SPool(LinePre, 0, "Line");
            _textUIPool = new SPool(TextPre, 0, "T");

            ScrollRect.onValueChanged.AddListener(OnScroll);
        }

        public void SetData(string path, string name = "")
        {
            if (!ImageManager.Inst.TryLoadSprite(path, Image, out var spr, true, true))
            {
                Image.sprite = DefaultSprite;
                if (SizeText) SizeText.text = "";
                return;
            }
            _path = path;
            SetData(spr, name);
        }

        public void SetData(Sprite spr, string name = "", bool reset = true, Vector2Int hold_offset = default)
        {
            ClearPixInfoFloat();
            ClearMask();

            spr.texture.filterMode = FilterMode.Point;
            Image.sprite = spr;
            _imageSize = new Vector2Int(spr.texture.width, spr.texture.height);
            if (SizeText)
            {
                var str = string.Format($"({_imageSize.x}*{_imageSize.y})");
                if (!string.IsNullOrEmpty(name))
                    str = $"{str} {name}" ;
                SizeText.text = str;
            }


            MinScale = Math.Min(_edgeLen / _imageSize.x, _edgeLen / _imageSize.y);
            MinScale = Math.Min(MinScale, MaxScale);

            // 重置 或者 必须的初始化
            if (reset || _sizeScale == 0)
            {
                _sizeScale = MinScale;
                RefreshScale();
                ScrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);

                _select_pixel_pos = new Vector2Int(-1, -1);
            }
            else
            {
                // if (hold_offset != default)
                // {
                //     DU.LogWarning(hold_offset);
                // }

                var normalPos = ScrollRect.normalizedPosition;
                var pix_x = (normalPos.x * (_contentW - _viewW) - _viewW / 2) / _sizeScale;
                var pix_y = (normalPos.y * (_contentH - _viewH) - _viewH / 2) / _sizeScale;
                RefreshScale();
                var x = ((pix_x + hold_offset.x) * _sizeScale + _viewW / 2) / (_contentW - _viewW);
                var y = ((pix_y + hold_offset.y) * _sizeScale + _viewH / 2) / (_contentH - _viewH);
                ScrollRect.normalizedPosition = new Vector2(x, y);

                _select_pixel_pos = _select_pixel_pos + hold_offset;
            }

        }

        public void SetLineOffset(Vector2Int offset)
        {
            _line_offset = offset;
        }

        /// <summary>
        /// <文本，像素位置>
        /// </summary>
        public void SetTextUI(List<(string, Vector2Int)> texts
                            , int font_size = 20, float show_text_scale = 3, Color color = default)
        {
            ClearTextUI();
            _textUIs.Clear();
            foreach (var tuple in texts)
                _textUIs.Add((null, tuple.Item1, tuple.Item2));

            color = color == default ? Color.white : color;
            _textUI_config = (font_size, show_text_scale, color);
            RefreshTextUI();
        }

        public void ClearTextUI()
        {
            for (int i = 0; i < _textUIs.Count; i++)
            {
                var tuple = _textUIs[i];
                if (tuple.Item1 != null)
                {
                    _textUIPool.Push(tuple.Item1);
                    tuple.Item1 = null;
                    _textUIs[i] = tuple;
                }
            }
        }


        public void ClearData()
        {
            // _imageRT.sizeDelta = new Vector2(100, 100);
            // _maskRT.sizeDelta = new Vector2(100, 100);
            Image.sprite = DefaultSprite;
            // _imageSize = default;
            if (SizeText) SizeText.text = " * ";

            // _sizeScale = 1;
            _select_pixel_pos = new Vector2Int(-1, -1);
            // ScrollRect.normalizedPosition = new Vector2(0.5f, 0.5f);

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
            if (IsEmpty)
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
            _maskRT.sizeDelta = size;
            var old_normal_pos = ScrollRect.normalizedPosition;
            _contentRT.sizeDelta = new Vector2(_contentW, _contentH);
            // 真巧，算出来就是old_normal_pos
            ScrollRect.normalizedPosition = old_normal_pos;

            // RefreshLine();
        }

        #region  Line

        void RefreshLine()
        {
            RefreshLine(1, Vector2Int.zero, new Color(1, 1, 1, 0.25f), 1);
            RefreshLine(5, _line_offset, new Color(0, 1, 0, 0.3f), 2);
            // RefreshLine(20, _line_offset, new Color(1, 0, 0, 0.6f), 2);
        }
        void RefreshLine(int pixel_interval, Vector2Int line_offset, Color color
                        , float thickness = 1)
        {
            // 先全部回收
            // inst = instance
            if (!_lineUIs.TryGetValue(pixel_interval, out var insts))
            {
                insts = new List<GameObject>();
                _lineUIs[pixel_interval] = insts;
            }

            foreach (var line in insts)
                _lineUIPool.Push(line);

            insts.Clear();

            if (!showLine)
                return;

            var spacing = pixel_interval * _sizeScale;
            var offset = new Vector2(line_offset.x * _sizeScale, line_offset.y * _sizeScale); //大格子的偏移

            var normalPos = ScrollRect.normalizedPosition;
            // DU.Log($"normal {ScrollRect.normalizedPosition}");

            // 视窗的左下角在Content节点(以左下角为原点)下的坐标
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

                var count = endXIndex - startXIndex + 1;

                for (int i = 0; i < count; i++)
                {
                    var line = _lineUIPool.Pop();
                    insts.Add(line);
                    var rect = line.GetComponent<RectTransform>();
                    rect.SetParent(LineParent, false);
                    rect.GetComponent<Image>().color = color;
                    rect.sizeDelta = new Vector2(thickness, _imageH);
                    rect.anchorMin = new Vector2(0, 0.5f);
                    rect.anchorMax = new Vector2(0, 0.5f);
                    rect.anchoredPosition = new Vector2((startXIndex + i) * spacing + _viewW / 2 + offset.x, 0);
                }

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

                var count = endYIndex - startYIndex + 1;

                for (int i = 0; i < count; i++)
                {
                    var line = _lineUIPool.Pop();
                    insts.Add(line);
                    var rect = line.GetComponent<RectTransform>();
                    rect.SetParent(LineParent, false);
                    rect.GetComponent<Image>().color = color;
                    rect.sizeDelta = new Vector2(_imageW, thickness);
                    rect.anchorMin = new Vector2(0.5f, 0);
                    rect.anchorMax = new Vector2(0.5f, 0);
                    rect.anchoredPosition = new Vector2(0, (startYIndex + i) * spacing + _viewH / 2 + offset.y);
                }

            }

        }


        void RefreshTextUI()
        {
            if (_textUIs.Count == 0)
                return;

            if (!showText)
            {
                ClearTextUI();
                return;
            }

            var normalPos = ScrollRect.normalizedPosition;
            var x = normalPos.x * (_contentW - _viewW);
            var y = normalPos.y * (_contentH - _viewH);
            var px_start = (x - _viewW / 2) / _sizeScale;
            var py_start = (y - _viewH / 2) / _sizeScale;
            var px_end = (x + _viewW / 2) / _sizeScale;
            var py_end = (y + _viewH / 2) / _sizeScale;

            for (int i = 0; i < _textUIs.Count; i++)
            {
                var tuple = _textUIs[i];
                var pix_pos = tuple.Item3;
                if (px_start < pix_pos.x && pix_pos.x < px_end
                    && py_start < pix_pos.y && pix_pos.y < py_end)
                {
                    var go = tuple.Item1;
                    if (go == null)
                    {
                        go = _textUIPool.Pop();
                        go.transform.SetParent(TextParent);
                        var textUI = go.GetComponent<Text>();
                        textUI.text = tuple.Item2;
                        textUI.fontSize = _textUI_config.Item1;
                        textUI.color = _textUI_config.Item3;

                        tuple.Item1 = go;
                        _textUIs[i] = tuple;
                    }

                    var rect = go.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(
                        pix_pos.x * _sizeScale + _sizeScale / 2 + _viewW / 2,
                        pix_pos.y * _sizeScale + _sizeScale / 2 + _viewH / 2
                    );

                }
                else
                {
                    if (tuple.Item1 != null)
                    {
                        _textUIPool.Push(tuple.Item1);
                        tuple.Item1 = null;
                        _textUIs[i] = tuple;
                    }
                }
            }


        }

        void OnScroll(Vector2 cur)
        {
            // DU.Log($"OnScroll {cur}");

            if (IsEmpty)
                return;

            RefreshLine();
            RefreshTextUI();
            RefreshSelectPixel();
            _onScroll?.Invoke(cur);
        }

        #endregion


        #region  SelectPixel

        Vector3Int _center_pix;                 // 中心点颜色
        Vector3Int _convolution_pix;            // 卷积颜色
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
                ClearPixInfoFloat();
                return;
            }

            Utils.SetActive(SelectPixelUI, true);
            SelectPixelUI.transform.SetParent(ScrollRect.content);
            var pos = new Vector2(x, y);
            var size = new Vector2(spacing, spacing);
            SelectPixelUI.SetAnchor(new Vector2(0, 0));
            SelectPixelUI.SetData(pos, size, Color.white);

            Utils.SetActive(Float.SelfRT, true);
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
            _center_pix = new Vector3Int((int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255));

            Float.RT.text = $"{_center_pix.x} , ";
            Float.GT.text = $"{_center_pix.y} , ";
            Float.BT.text = $"{_center_pix.z}";
            Float.ColorImage.color = color;

            if (_select_pixel_pos.x > 0 && _select_pixel_pos.x < _imageSize.x - 1
                && _select_pixel_pos.y > 0 && _select_pixel_pos.y < _imageSize.y - 1)
            {
                // 卷积后颜色
                var r = 0f;
                var g = 0f;
                var b = 0f;
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        var c = tex.GetPixel((int)_select_pixel_pos.x + i, (int)_select_pixel_pos.y + j);
                        r += c.r * 255;
                        g += c.g * 255;
                        b += c.b * 255;
                    }

                _convolution_pix = new Vector3Int((int)r / 9, (int)g / 9, (int)b / 9);
                Float.CRT.text = $"{_convolution_pix.x} , ";
                Float.CGT.text = $"{_convolution_pix.y} , ";
                Float.CBT.text = $"{_convolution_pix.z}";
            }
            else
            {
                _convolution_pix = Vector3Int.zero;
                Float.CRT.text = $"";
                Float.CGT.text = $"";
                Float.CBT.text = $"";
            }
        }

        public void GetSelectPixelInfo(out Vector3Int center, out Vector3Int convolution)
        {
            center = _center_pix;
            convolution = _convolution_pix;
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
                ClearPixInfoFloat();
            }
        }


        void ClearPixInfoFloat()
        {
            Utils.SetActive(SelectPixelUI, false);
            Utils.SetActive(Float.SelfRT, false);
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


        #region Mask
        public void SetMask(Color32[] pixs)
        {
            ClearMask();
            if (pixs == null)
                return;

            int w = _imageSize.x;
            int h = _imageSize.y;
            if (pixs.Length != w * h)
            {
                DU.LogError($"[ImageDetailComp]{Utils.SpaceStr}Mask与Image不匹配");
                return;
            }

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixs);
            texture.Apply();

            _maskSpr = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            MaskImage.sprite = _maskSpr;
            MaskImage.color = new Color(1, 1, 1, 1f);

        }

        void ClearMask()
        {
            if (_maskSpr != null)
            {
                MaskImage.sprite = null;
                MaskImage.color = new Color(0, 0, 0, 0);
                Destroy(_maskSpr);
                Destroy(_maskSpr.texture);
                _maskSpr = null;
            }
        }

        void OnDestroy()
        {
            ClearMask();
        }

        #endregion

        #region  Other
        public Color32[] GetColor32(out int w, out int h)
        {
            var tex = Image.sprite.texture;
            w = tex.width;
            h = tex.height;
            var pixs = tex.GetPixelData<Color32>(0);
            var result = new Color32[pixs.Length];
            for (int i = 0; i < pixs.Length; i++)
            {
                result[i] = pixs[i];
            }
            return result;
        }
        #endregion
    }
}