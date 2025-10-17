
using System;
using System.Collections;
using System.Collections.Generic;
using Script.UI.Panel.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    //提取关键词委托
    public delegate void ExtractKeywordDelegate(string resource, int caretPos, out string keyword, out int startIndex);
    /// <summary>
    /// 具有功能：变量关键词提示，检查合法性，enter选中提示词并输入
    /// </summary>
    public class InputTextProComp : MonoBehaviour, IPointerDownHandler
    {

        [SerializeField] private CustomInputField InputText;
        [SerializeField] public CheckBox ValidCheck;


        Action<string> _onEndEditFunc;          //输入结束行为,一般要保存编辑后的数据
        Action<string> _onValueChangedFunc;     //输入每个字符时行为


        KeywordTipsComp _tipsComp;              //提示词组件
        Func<string, List<string>> _matchFunc;  //提示词，搜索方法
        ExtractKeywordDelegate _extractKeyFunc;    //提示词，提取关键词方法
        List<string> _match_list;               //提示词，搜索结果
        string _keyword;                        //提示词，关键词根据光标位置提取
        int _keywordStart;                   //提示词，关键词起始位置

        Vector2 _relativePos;                   //相对位置
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
            RefreshCheckBox();
        }

        /// <summary>
        /// 开启关键词提示功能
        /// </summary>
        public void UseKeywordTips(KeywordTipsComp keywordTipsComp, Func<string, List<string>> matchFunc
        , ExtractKeywordDelegate extractKeyFunc = null)
        {
            _tipsComp = keywordTipsComp;
            _matchFunc = matchFunc;
            _extractKeyFunc = extractKeyFunc;
        }

        public void UseCheckBox(Func<string, bool> checkFunc)
        {
            _checkFunc = checkFunc;
            RefreshCheckBox();
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



        string _oldStr = "";
        // InputText.text 赋值时触发
        void OnValueChanged(string str)
        {
            // Enter键会自动加\n
            if (str.IndexOf("\n") >= 0)
            {
                InputText.text = str.Replace("\n", "");
                return;
            }
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


        void TipConfirm(int index)
        {
            string result = _match_list[index];
            string temp = InputText.text;

            int front_space = 0;
            int back_space = 0;
            bool meet_content = false;
            for (int i = 0; i < _keyword.Length; i++)  // 只统计开头结尾连续空格
            {
                if (_keyword[i] == ' ')
                {
                    if (meet_content) back_space++;
                    else front_space++;
                }
                else
                {
                    if (!meet_content) meet_content = true;
                }
            }

            int start = _keywordStart + front_space;

            temp = temp.Remove(start, _keyword.Length - front_space - back_space);
            temp = temp.Insert(start, result);

            InputText.text = temp;
            InputText.caretPosition = start + result.Length;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // DU.Log("编辑开始");
            // _foucs = true;
            RefreshTipsComp();
        }


        void RefreshTipsComp()
        {
            if (_tipsComp == null) return;
            if (!gameObject.activeInHierarchy) return;

            var textComp = InputText.textComponent;
            int caretPos = InputText.caretPosition;

            if (caretPos == 0)
            {
                Utils.SetActive(_tipsComp, false);
                return;
            }

            // DU.LogWarning(caretPos);

            // 计算位置
            var tipsCompRectT = _tipsComp.GetComponent<RectTransform>();
            if (_relativePos == Vector2.zero)
                _relativePos = Utils.GetPos(tipsCompRectT, textComp.GetComponent<RectTransform>()
                , Vector2.zero, true);


            //没有提取函数，就用全部文本
            if (_extractKeyFunc == null)
            {
                _keyword = InputText.text;
                _keywordStart = 0;
            }
            else
            {
                _extractKeyFunc(InputText.text, caretPos, out _keyword, out _keywordStart);
            }

            // DU.LogWarning($"关键词{_keyword}  起点{_keywordStart}");


            _match_list = _matchFunc(_keyword.Trim());
            bool show = _match_list.Count > 0 && _match_list[0] != _keyword.Trim();
            if (show)
            {
                // 点击搜索结果
                _tipsComp.SetData(_match_list,
                index =>
                {
                    // DU.Log("选择结束");
                    TipConfirm(index);
                });
                _tipsComp.SetAutoCloseWithoutArea(new List<GameObject>() { gameObject });
                StartCoroutine(DelayShowTipsComp());
            }
            else
            {
                Utils.SetActive(_tipsComp, false);
            }
        }

        void RefreshCheckBox()
        {
            if (ValidCheck == null || _checkFunc == null) return;

            bool legal = _checkFunc(InputText.text);
            ValidCheck.SetData(legal);
        }

        /// <summary>
        /// 由于当text改变时就调用OnValueChanged，在渲染之前，就无法获得新字符的位置。故而延迟一帧
        /// </summary>
        IEnumerator DelayShowTipsComp()
        {
            yield return null;
            Vector2 localPos = Vector2.zero;

            var textComp = InputText.textComponent;
            int caretPos = InputText.caretPosition;
            if (caretPos == 0 || caretPos > textComp.text.Length)
                yield break;


            Utils.SetActive(_tipsComp, true);
            var textGen = textComp.cachedTextGenerator;

            // 获取光标当前的占位, 为字符的右上角坐标
            var charInfo = textGen.characters[caretPos - 1];
            // 加点偏移，然后因字体缩放而除2。
            localPos = charInfo.cursorPos - new Vector2(0, textComp.fontSize + 6);
            localPos = localPos / 2;
            // DU.LogWarning(localPos);

            var tipsCompRectT = _tipsComp.GetComponent<RectTransform>();
            tipsCompRectT.anchoredPosition = _relativePos + localPos;
        }
    }
}