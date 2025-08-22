using System;
using System.Collections.Generic;
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

        [SerializeField] private Transform Parent;
        [SerializeField] private GameObject Prefab;


        [HideInInspector] public int SelectIndex = -1;
        private List<KeywordTipsItem> _items = new List<KeywordTipsItem>();


        void Awake()
        {
            Prefab.SetActive(false);
        }

        public void SetData(List<string> strList, Action<int> onSelect, float width = 120)
        {
            Utils.RefreshItemListByCount(_items, strList.Count, Prefab, Parent, (item, index) =>
                {
                    item.SetData(strList[index], index, onSelect, this);
                });

            float oneH = Prefab.GetComponent<RectTransform>().sizeDelta.y;
            float height = oneH * strList.Count + 6;
            var selfRect = GetComponent<RectTransform>();
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        public void SetPos(RectTransform target, Vector2 offset)
        {
            var selfRect = GetComponent<RectTransform>();
            var pos = Utils.GetPos(selfRect, target, offset);
            selfRect.anchoredPosition = pos;
        }

        public void SetCurIndex(int index)
        {
            SelectIndex = index;
            Refresh();
        }

        public void Refresh()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Refresh();
            }
        }

    }
}