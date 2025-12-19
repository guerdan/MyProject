using System.Collections.Generic;
using UnityEngine;

namespace Script.UI.Components
{
    /// <summary>
    /// 关键词提示词组件
    /// </summary>
    public class MenuComp : MonoBehaviour
    {

        [SerializeField] private VirtualListComp ListComp;
        [SerializeField] private GameObject Prefab;

        MenuSystem _system;
        List<MenuStrPack> _column;
        public float _itemWidth;

        void Awake()
        {

            Prefab.SetActive(false);
            ListComp.OnGetItemTemplate = i => Prefab;
            ListComp.OnGetItemSize = i => new Vector2(_itemWidth, Prefab.GetComponent<RectTransform>().rect.height);
            ListComp.OnUpdateItem = OnUpdateItem;

        }
        void OnUpdateItem(GameObject item, int index)
        {
            var comp = item.GetComponent<MenuItem>();
            var data = _column[index];
            comp.SetData(_system, this, data, index, _itemWidth);
        }

        /// <summary>
        /// 不传onSelect代表不用选项。
        /// </summary>
        public void SetData(List<MenuStrPack> column, MenuSystem system, float width = 100
            , int maxShowCount = 10)
        {
            _column = column;
            _system = system;
            _itemWidth = width - 6;
            // 初始化
            // SelectIndex = 0;

            int count = Mathf.Min(_column.Count, maxShowCount);
            float oneH = Prefab.GetComponent<RectTransform>().sizeDelta.y;
            float height = oneH * count + 16;
            var selfRect = GetComponent<RectTransform>();
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            selfRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            ListComp.SyncContentSize();
            ListComp.ReloadData(_column.Count);
        }
      

        public Vector2 GetItemScreenPos(int index)
        {
            return ListComp.GetItemScreenPos(index);
        }


        public void Refresh()
        {
            ListComp.UpdateData();
        }
       

    }
}