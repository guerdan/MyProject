
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// 选项盒子组件，不包括展开
    /// </summary>
    public class SelectBoxComp : MonoBehaviour
    {

        [SerializeField] private Text Content;
        [SerializeField] private Image Bg;
        [SerializeField] private Button Arrow;

        List<string> _strList;
        Action<int> _onSelect;
        KeywordTipsComp _cueWordComp;

        bool _folded = true;

        void OnEnable()
        {
            Arrow.onClick.AddListener(OnArrowClick);
        }

        void OnDisable()
        {
            Arrow.onClick.RemoveListener(OnArrowClick);
        }

        public void SetData(List<string> strList, Action<int> onSelect, KeywordTipsComp cueWordComp)
        {
            _strList = strList;
            _onSelect = (index) =>
            {
                Content.text = strList[index];
                _folded = true;
                OnFold(_folded);
                onSelect?.Invoke(index);
            };
            _cueWordComp = cueWordComp;
        }

        void OnArrowClick()
        {
            _folded = !_folded;
            OnFold(_folded);
        }

        void OnFold(bool fold)
        {
            Utils.SetActive(_cueWordComp, !fold);
            Arrow.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, fold ? 0 : 180);
            if (fold)
            {
                Bg.color = new Color(1, 1, 1, 128f / 255);
            }
            else
            {
                Bg.color = new Color(1, 1, 1, 1);
                var rectT = GetComponent<RectTransform>();
                _cueWordComp.SetData(_strList, _onSelect, 150);
                _cueWordComp.SetPos(rectT, new Vector2(0, -rectT.rect.height / 2));
                _cueWordComp.SetCurIndex(_strList.IndexOf(Content.text));
            }
        }
    }
}