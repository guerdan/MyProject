
using System;
using System.Collections.Generic;
using Script.UI.Panel.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{
    /// <summary>
    /// 具有功能：全文本作为关键词的提示，检查合法性，enter选中提示词并替换
    /// </summary>
    public class InputTextComp : MonoBehaviour, IPointerDownHandler
    {

        [SerializeField] private InputField InputText;

        bool _foucs = false;                    //是否获得焦点

        Action<string> _onEndEditFunc;          //输入结束行为,一般要保存编辑后的数据
        Action<string> _onValueChangedFunc;     //输入每个字符时行为


        KeywordTipsComp _tipsComp;              //提示词组件
        Func<string, List<string>> _matchFunc;  //提示词搜索方法

        List<string> _match_list;               //提示词搜索结果

        CheckBox _checkBox;                     //检查组件
        Func<string, bool> _checkFunc;          //检查方法

        void Awake()
        {
            InputText.onValueChanged.AddListener(OnValueChanged);
            InputText.onEndEdit.AddListener(OnEndEdit);
        }

        public void SetData(string text, Action<string> onEndEditFunc
            , Action<string> onValueChangedFunc = null)
        {
            // 阻止 OnValueChanged 调用
            //
            _oldStr = text;

            InputText.text = text;
            _onValueChangedFunc = onValueChangedFunc;
            _onEndEditFunc = onEndEditFunc;
        }

        /// <summary>
        /// 开启关键词提示功能
        /// </summary>
        public void UseKeywordTips(KeywordTipsComp keywordTipsComp, Func<string, List<string>> matchFunc)
        {
            _tipsComp = keywordTipsComp;
            _matchFunc = matchFunc;
        }

        public void UseCheckBox(CheckBox checkBox, Func<string, bool> checkFunc)
        {
            _checkBox = checkBox;
            _checkFunc = checkFunc;
        }

        public void SetText(string text)
        {
            InputText.text = text;
            RefreshCheckBox();
        }
        public string GetText()
        {
            return InputText.text;
        }

        void RefreshTipsComp()
        {
            if (_tipsComp == null) return;

            _match_list = _matchFunc(InputText.text);
            var rectT = InputText.GetComponent<RectTransform>();

            Utils.SetActive(_tipsComp, _match_list.Count > 0);
            if (_match_list.Count > 0)
            {
                // 点击搜索结果
                _tipsComp.SetData(_match_list,
                index =>
                {
                    // DU.Log("选择结束");
                    var result = _match_list[index];
                    InputText.text = result;
                    OnEndEdit(InputText.text);
                    Unused();
                });
                // 本身的中心点在 x = 0
                _tipsComp.SetPos(rectT, new Vector2(-rectT.rect.width / 2, -rectT.rect.height / 2));
                _tipsComp.SetAutoCloseWithoutArea(new List<GameObject>() { gameObject });
            }
        }

        void RefreshCheckBox()
        {
            if (_checkBox == null) return;

            bool legal = _checkFunc(InputText.text);
            _checkBox.SetData(legal);
        }


        string _oldStr = "";
        // InputText.text发送变换时触发
        void OnValueChanged(string str)
        {
            if (str == _oldStr)
                return;

            _oldStr = str;

            // DU.Log("编辑改变");
            RefreshTipsComp();
            RefreshCheckBox();
            _onValueChangedFunc?.Invoke(str);
        }


        // 触发时机：InputText失焦 或 Enter键
        // 先触发OnEndEdit(), 再触发点击KeywordTipsComp的点击事件,以及Update的Enter键
        // 所以只能在 _tipsComp.onSelect 里调用OnEndEdit()，调用两遍
        void OnEndEdit(string str)
        {
            // DU.Log("编辑结束");
            _onEndEditFunc?.Invoke(str);
        }

        // 不使用后
        void Unused()
        {
            Utils.SetActive(_tipsComp, false);
            _foucs = false;
        }


        public void OnPointerDown(PointerEventData eventData)
        {
            // DU.Log("编辑开始");
            _foucs = true;
            RefreshTipsComp();
        }
    }
}