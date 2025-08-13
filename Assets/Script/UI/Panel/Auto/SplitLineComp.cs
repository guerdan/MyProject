
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// 滑到哪里是哪里，视野之外的不显示也不加载
    /// </summary>
    public class SplitLineComp : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private GameObject textPrefab;

        private RectTransform _trans;
        private List<RectTransform> _wLineList = new List<RectTransform>();
        private List<RectTransform> _wTextList = new List<RectTransform>();
        private List<RectTransform> _hLineList = new List<RectTransform>();
        private List<RectTransform> _hTextList = new List<RectTransform>();

        float _viewW = 0;
        float _viewH = 0;
        float _contentW = 0;
        float _contentH = 0;
        float _spacing = 0;
        float _thickness = 0;
        [SerializeField] float _lineLong = 40;

        void Awake()
        {
            scrollRect.onValueChanged.AddListener(OnScrolling);
            _trans = GetComponent<RectTransform>();


            linePrefab.SetActive(false);
            textPrefab.SetActive(false);
            //先初始化 预制件的参数
            var trans_line = linePrefab.GetComponent<RectTransform>();
            trans_line.anchorMin = new Vector2(0, 0);
            trans_line.anchorMax = new Vector2(0, 0);
            var trans = textPrefab.GetComponent<RectTransform>();
            trans.anchorMin = new Vector2(0, 0);
            trans.anchorMax = new Vector2(0, 0);
            trans.pivot = new Vector2(0, 1);

        }

        public void SetData(float spacing, float thickness = 2)
        {
            var contentR = scrollRect.content.GetComponent<RectTransform>();
            _contentW = contentR.rect.width;
            _contentH = contentR.rect.height;
            var viewR = scrollRect.viewport.GetComponent<RectTransform>();
            _viewW = viewR.rect.width;
            _viewH = viewR.rect.height;
            _spacing = spacing;
            _thickness = thickness;

            scrollRect.normalizedPosition = new Vector2(0, 1);
            OnScrolling(scrollRect.normalizedPosition);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"> 原点在Content的左下角，描述Viewport左上角点在Content的位置 </param>
        void OnScrolling(Vector2 pos)
        {
            if (_viewW == 0) return;
            // DU.Log($"{pos.x}   {pos.y}");

            //pos的含义是距离的比例。给它转换到正常坐标系下, 也就是把比例换算进去
            var nPos = new Vector2(pos.x * (_contentW - _viewW), pos.y * (_contentH - _viewH) + _viewH);


            var startXIndex = (int)Math.Ceiling(nPos.x / _spacing);
            var endXIndex = (int)Math.Floor((nPos.x + _viewW) / _spacing);
            var count = endXIndex - startXIndex + 1;

            // 绘制横轴
            // 先生成节点
            Utils.RefreshItemListByCount<RectTransform>(_wLineList, count, linePrefab, _trans, null);
            Utils.RefreshItemListByCount<RectTransform>(_wTextList, count, textPrefab, _trans, null);

            for (int i = startXIndex; i <= endXIndex; i++)
            {
                var x = _spacing * i - nPos.x;
                var trans_line = _wLineList[i - startXIndex];
                trans_line.pivot = new Vector2(0.5f, 1);
                trans_line.sizeDelta = new Vector2(_thickness, _lineLong);
                trans_line.anchoredPosition = new Vector2(x, _viewH);

                var trans_text = _wTextList[i - startXIndex];
                trans_text.anchoredPosition = new Vector2(x + 6, _viewH + 3);
                trans_text.GetComponent<Text>().text = $"{_spacing * i / 100}";
            }

            // 绘制纵轴
            var startYIndex = (int)Math.Ceiling(MapConvertY(nPos.y) / _spacing);
            var endYIndex = (int)Math.Floor(MapConvertY(nPos.y - _viewH) / _spacing);
            count = endYIndex - startYIndex + 1;

            Utils.RefreshItemListByCount<RectTransform>(_hLineList, count, linePrefab, _trans, null);
            Utils.RefreshItemListByCount<RectTransform>(_hTextList, count, textPrefab, _trans, null);

            for (int i = startYIndex; i <= endYIndex; i++)
            {
                var y = _viewH - (nPos.y - MapConvertY(_spacing * i));

                var trans_line = _hLineList[i - startYIndex];
                trans_line.pivot = new Vector2(0, 0.5f);
                trans_line.sizeDelta = new Vector2(_lineLong, _thickness);
                trans_line.anchoredPosition = new Vector2(0, y);

                var trans_text = _hTextList[i - startYIndex];
                trans_text.anchoredPosition = new Vector2(0, y - 3);
                //排除掉竖行的0
                trans_text.GetComponent<Text>().text = i > 0 ? $"{_spacing * i / 100}" : "";
            }

        }

        /// <summary>
        /// ui坐标系转画布坐标系，或反转
        /// </summary>
        public Vector2 MapConvert(Vector2 p)
        {
            return new Vector2(p.x, _contentH - p.y);
        }
        public float MapConvertY(float p)
        {
            return _contentH - p;
        }

        /// <summary>
        /// 返回ui坐标系下
        /// </summary>
        public Vector2 GetCenter()
        {
            var x = scrollRect.horizontalNormalizedPosition;
            var y = scrollRect.verticalNormalizedPosition;

            var nPos = new Vector2(x * (_contentW - _viewW), y * (_contentH - _viewH) + _viewH);
            var center = new Vector2(nPos.x + _viewW / 2, nPos.y - _viewH / 2);
            return center;
        }
    }
}