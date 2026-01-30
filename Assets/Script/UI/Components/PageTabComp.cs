
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Components
{
    public class PageTabComp : MonoBehaviour
    {
        [SerializeField] private Button[] Btns;
        [SerializeField] private GameObject[] ActiveGO;
        [SerializeField] private GameObject[] UnactiveGO;
        [SerializeField] private GameObject[] TabNums;
        private (RectTransform, Text)[] RefTabNums;
        Action<int> _select_action;
        int _tab_count;
        int _current_index;
        List<int> _nums;

        void Awake()
        {
            for (int i = 0; i < Btns.Length; i++)
            {
                int close_pack = i;
                Btns[i].onClick.AddListener(() => OnClick(close_pack));
            }

            RefTabNums = new (RectTransform, Text)[TabNums.Length];
            for (int i = 0; i < TabNums.Length; i++)
            {
                var go = TabNums[i];
                var rectT = go.GetComponent<RectTransform>();
                var textUI = go.GetComponentInChildren<Text>();
                RefTabNums[i] = (rectT, textUI);
            }

        }

        public void SetData(Action<int> select_action, int tab_count, int init_index)
        {
            _select_action = select_action;
            _tab_count = tab_count;

            for (int i = 0; i < Btns.Length; i++)
                Btns[i].gameObject.SetActive(i < tab_count);
            for (int i = 0; i < ActiveGO.Length; i++)
                ActiveGO[i].SetActive(i < tab_count);
            for (int i = 0; i < UnactiveGO.Length; i++)
                UnactiveGO[i].SetActive(i < tab_count);

            OnClick(init_index);
        }

        public void SetNum(List<int> nums)
        {
            _nums = nums;
            RefreshNum();
        }

        public void RefreshNum()
        {
            if (_nums == null)
                return;
            for (int i = 0; i < _nums.Count; i++)
            {
                var num = _nums[i];
                Utils.SetActive(TabNums[i], num > 0);
                if (num == 0)
                    continue;

                var refs = RefTabNums[i];
                var rectT = refs.Item1;
                var textUI = refs.Item2;

                var pos = i == _current_index ? new Vector2(-7, -3) : new Vector2(-12, -7);
                var scale = i == _current_index ? new Vector3(1.2f, 1.2f, 1.2f) : Vector3.one;
                rectT.anchoredPosition = pos;
                rectT.localScale = scale;
                textUI.text = $"{num}";
            }
        }


        void OnClick(int index)
        {
            _current_index = index;
            for (int i = 0; i < _tab_count; i++)
            {
                ActiveGO[i].SetActive(i == index);
                UnactiveGO[i].SetActive(i != index);
            }
            _select_action?.Invoke(index);
            RefreshNum();
        }


        public int GetCurIndex()
        {
            return _current_index;
        }

    }
}