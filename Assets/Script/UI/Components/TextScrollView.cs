
using System.Collections.Generic;
using Script.Framework;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Components
{

    public struct TextUIData
    {
        public string Content;
        public float Height;

        public TextUIData(string content)
        {
            Content = content;
            Height = -1;
        }
    }

    // 滑动位置
    public enum ScrollToPos
    {
        StayStill,
        Top,
        Bottom,
    }

    public class TextScrollView : MonoBehaviour
    {

        [SerializeField] private VirtualListComp ListComp;
        [SerializeField] private GameObject itemPrefab;

        List<TextUIData> _logs;
        float _spacing;
        float _text_width;
        int _text_fontSize;

        void Awake()
        {
            _text_width = itemPrefab.GetComponent<RectTransform>().rect.size.x;
            _text_fontSize = itemPrefab.GetComponentInChildren<Text>().fontSize;

            itemPrefab.SetActive(false);
            ListComp.OnGetItemSize = GetItemSize;
            ListComp.OnGetItemTemplate = (int index) => itemPrefab;
            ListComp.OnUpdateItem = UpdateItem;
        }

        public void SetData(string[] list, ScrollToPos pos = ScrollToPos.Top, float spacing = 12)
        {
            _logs = new List<TextUIData>();
            _spacing = spacing;
            if (list != null)
                for (int i = 0; i < list.Length; i++)
                {
                    _logs.Add(new TextUIData(list[i]));
                }

            ReloadData(pos);
        }

        public void SetData(List<TextUIData> list, ScrollToPos pos = ScrollToPos.Top, float spacing = 12)
        {
            _logs = list;
            _spacing = spacing;
            ReloadData(pos);
        }

        public void Clear()
        {
            _logs = new List<TextUIData>();
            ReloadData(ScrollToPos.Top);
        }

        void ReloadData(ScrollToPos pos)
        {
            if (pos == ScrollToPos.Top)
                ListComp.ReloadData(_logs.Count, true);
            else if (pos == ScrollToPos.StayStill)
                ListComp.ReloadData(_logs.Count, false);
            else if (pos == ScrollToPos.Bottom)
            {
                ListComp.ReloadData(_logs.Count, false);
                ListComp.ScrollToBottom();
            }
        }



        Vector2 GetItemSize(int index)
        {
            TextUIData data = _logs[index];

            // 未初始化则初始化
            if (data.Height < 0)
            {
                var height = SceneTool.Inst.GetTextPreferHeight(_text_width, _text_fontSize, data.Content);
                data.Height = height;
                _logs[index] = data;
            }

            return new Vector2(_text_width, data.Height + _spacing);
        }

        void UpdateItem(GameObject item, int index)
        {
            TextUIData data = _logs[index];
            float height = data.Height;
            string content = data.Content;

            var rectT = item.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(rectT.sizeDelta.x, height + 12);      //高度
            item.GetComponentInChildren<Text>().text = content;                 //内容

            Utils.SetActive(item.transform.GetChild(0), index % 2 == 0);        //背景
        }
    }
}