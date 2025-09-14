using System;
using System.Collections.Generic;
using Script.UI.Panel.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// 关键词提示词组件
    /// </summary>
    public class KeywordTipsComp : MonoBehaviour
    {

        [SerializeField] private VirtualListComp ListComp;
        [SerializeField] private GameObject Prefab;
        [SerializeField] private Text NumText;


        [HideInInspector] public int SelectIndex = -1;
        List<string> _strList;
        Action<int> _onSelect;
        float _itemWidth;
        List<GameObject> _withoutArea;
        int _setAutoCloseFrame;

        void Awake()
        {
            _withoutArea = new List<GameObject>() { gameObject };

            Prefab.SetActive(false);
            ListComp.OnGetItemTemplate = i => Prefab;
            ListComp.OnGetItemSize = i => new Vector2(_itemWidth, Prefab.GetComponent<RectTransform>().rect.height);
            ListComp.OnUpdateItem = OnUpdateItem;

            // 按键参数初始化
            _upArrowTimer = _keyPressThreshold;
            _downArrowTimer = _keyPressThreshold;
        }
        void OnEnable()
        {
            _setAutoCloseFrame = Time.frameCount;
        }

        void OnUpdateItem(GameObject item, int index)
        {
            var comp = item.GetComponent<KeywordTipsItem>();
            comp.SetData(_strList[index], index, _onSelect, _itemWidth, this);

        }
        public void SetData(List<string> strList, Action<int> onSelect, float width = 160
            , int maxShowCount = 5)
        {
            _strList = strList;
            _itemWidth = width;
            _onSelect = i =>
            {
                onSelect?.Invoke(i);
                gameObject.SetActive(false);
            };
            // 初始化
            SelectIndex = 0;

            int count = Mathf.Min(strList.Count, maxShowCount);
            float oneH = Prefab.GetComponent<RectTransform>().sizeDelta.y;
            float height = oneH * count + 6;
            var selfRect = GetComponent<RectTransform>();
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + 10);

            ListComp.SyncContentSize();
            ListComp.ReloadData(_strList.Count);
            Utils.SetActive(NumText.transform.parent, _strList.Count > maxShowCount);
            NumText.text = $"{_strList.Count}";
        }

        public void SetPos(RectTransform target, Vector2 offset)
        {
            var selfRect = GetComponent<RectTransform>();
            var pos = Utils.GetPos(selfRect, target, offset);
            selfRect.anchoredPosition = pos;
        }

        //想着以后，用上下键来切换。Enter也封进去
        public void SetCurIndex(int index)
        {
            SelectIndex = index;
            Refresh();
        }
        // 是否开启点击区域外关闭
        public void SetAutoCloseWithoutArea(List<GameObject> area)
        {
            _withoutArea = area;
            _withoutArea.Add(gameObject);
        }

        public void Refresh()
        {
            ListComp.UpdateData();
        }

        // 点击本区域外，就关闭提示窗
        // 接口OnPointUp > Update
        void Update()
        {

            if (Input.GetMouseButtonDown(0) && _setAutoCloseFrame != Time.frameCount)
            {
                bool close = true;
                foreach (var go in _withoutArea)
                {
                    if (Utils.IsPointerOverUIObject(go, AutoRoot.Inst.Canvas))
                    {
                        close = false;
                        break;
                    }
                }
                if (close)
                    gameObject.SetActive(false);
            }



            // 按Enter键 选中第一个搜索结果。上下键能切换
            if (_strList != null && _strList.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    _onSelect?.Invoke(SelectIndex);
                }

                ListComp._scrollView.verticalScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

                bool up_move = false;
                bool down_move = false;

                if (Input.GetKeyDown(KeyCode.UpArrow)) up_move = true;
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    _upArrowTimer -= Time.deltaTime;
                    if (_upArrowTimer <= 0)
                    {
                        up_move = true;
                        _upArrowTimer = _keyPressInterval; 
                    }
                }
                else if (Input.GetKeyUp(KeyCode.UpArrow))
                {
                    _upArrowTimer = _keyPressThreshold; 
                }

                if (up_move)
                {
                    SetCurIndex((SelectIndex - 1 + _strList.Count) % _strList.Count);
                    ListComp.AdjustItemInViewVertical(SelectIndex);
                }

                if (Input.GetKeyDown(KeyCode.DownArrow)) down_move = true;
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    _downArrowTimer -= Time.deltaTime;
                    if (_downArrowTimer <= 0)
                    {
                        down_move = true;
                        _downArrowTimer = _keyPressInterval;
                    }
                }
                else if (Input.GetKeyUp(KeyCode.DownArrow))
                {
                    _downArrowTimer = _keyPressThreshold;
                }
                
                if (down_move)
                {
                    SetCurIndex((SelectIndex + 1) % _strList.Count);
                    ListComp.AdjustItemInViewVertical(SelectIndex);
                }
            }
        }

        private float _keyPressInterval = 0.13f; // 间隔
        private float _keyPressThreshold = 0.3f; // 阈值
        private float _upArrowTimer = 0f;
        private float _downArrowTimer = 0f;
    }
}