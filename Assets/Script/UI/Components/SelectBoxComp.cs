
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Components
{
    /// <summary>
    /// 选项盒子组件，不包括关键词提示窗
    /// 若没有序列化箭头，就默认取自身的Button组件
    /// </summary>
    public class SelectBoxComp : MonoBehaviour
    {

        [SerializeField] private Text Content;
        [SerializeField] private Image Bg;
        [SerializeField] private Button Arrow;

        List<string> _strList;
        Action<int> _onSelect;
        KeywordTipsComp _tipsComp;

        bool _folded = true;
        bool _locked = false;

        void OnEnable()
        {
            if (Arrow)
                Arrow.onClick.AddListener(OnArrowClick);
            else
                GetComponent<Button>().onClick.AddListener(OnArrowClick);
        }

        void OnDisable()
        {
            if (Arrow)
                Arrow.onClick.RemoveListener(OnArrowClick);
            else
                GetComponent<Button>().onClick.RemoveListener(OnArrowClick);
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
            _tipsComp = cueWordComp;
        }

        public void SetCurIndex(int index)
        {
            if (_strList != null && index >= 0 && index < _strList.Count)
            {
                Content.text = _strList[index];
                _onSelect?.Invoke(index);
            }
        }

        public void SetLock(bool locked)
        {
            _locked = locked;
            if (_locked)
                Bg.color = new Color(1, 1, 1, 1);
            else
                Bg.color = new Color(1, 1, 1, 0.5f);
        }

        void OnArrowClick()
        {
            if (_locked)
                return;

            _folded = !_folded;
            OnFold(_folded);
        }

        void OnFold(bool fold)
        {
            Utils.SetActive(_tipsComp, !fold);
            if (Arrow) Arrow.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, fold ? 0 : 180);
            if (fold)
            {
                Bg.color = new Color(1, 1, 1, 0.5f);
            }
            else
            {
                Bg.color = new Color(1, 1, 1, 1);
                var rectT = GetComponent<RectTransform>();
                _tipsComp.SetData(_strList, _onSelect);
                _tipsComp.SetPos(rectT, new Vector2(-rectT.rect.width / 2, -rectT.rect.height / 2));
                _tipsComp.SetCurIndex(_strList.IndexOf(Content.text));
            }
        }

        void Update()
        {
            if (_folded) return;
            // 点击区域外，就关闭提示窗
            if (Input.GetMouseButtonDown(0))
            {
                if (!Utils.IsPointerOverUIObject(gameObject, Root.Inst.Canvas)
                && !Utils.IsPointerOverUIObject(_tipsComp.gameObject, Root.Inst.Canvas))
                {
                    _folded = true;
                    OnFold(_folded);
                }

            }
        }
    }
}