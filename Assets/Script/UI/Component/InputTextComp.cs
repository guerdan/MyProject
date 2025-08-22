
using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    /// <summary>
    /// 输入文本组件
    /// todo
    /// 1.点空地取消
    /// </summary>
    public class InputTextComp : MonoBehaviour, IPointerDownHandler
    {

        [SerializeField] private InputField InputText;

        Action<string> _onEndEditFunc;          //输入结束行为,一般要保存编辑后的数据
        Action<string> _onValueChangedFunc;     //输入每个字符时行为


        KeywordTipsComp _tipsComp;              //提示词组件
        Func<string, List<string>> _matchFunc;  //提示词搜索方法

        List<string> _match_list;               //提示词搜索结果


        void Awake()
        {
            InputText.onValueChanged.AddListener(OnValueChanged);
            InputText.onEndEdit.AddListener(OnEndEdit);
        }

        public void SetData(string text, Action<string> onEndEditFunc
            , Action<string> onValueChangedFunc = null)
        {
            InputText.text = text;
            _onValueChangedFunc = onValueChangedFunc;
            _onEndEditFunc = onEndEditFunc;
            // Refresh();
        }

        /// <summary>
        /// 开启关键词提示功能
        /// </summary>
        public void OpenKeywordTips(KeywordTipsComp keywordTipsComp, Func<string, List<string>> matchFunc)
        {
            _tipsComp = keywordTipsComp;
            _matchFunc = matchFunc;
        }

        void RefreshTipsComp()
        {
            if (_tipsComp != null)
            {
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
                        Utils.SetActive(_tipsComp, false);
                    }, 150);
                    _tipsComp.SetPos(rectT, new Vector2(0, -rectT.rect.height / 2));
                    _tipsComp.SetCurIndex(0);
                }
            }
        }


        // InputText.text发送变换时触发
        void OnValueChanged(string str)
        {
            DU.Log("编辑改变");
            _onValueChangedFunc?.Invoke(str);
            RefreshTipsComp();
        }

        // 触发时机：InputText失焦 或 Enter键
        // 先触发OnEndEdit(), 再触发点击KeywordTipsComp的点击事件,以及Update的Enter键
        // 所以只能在 _tipsComp.onSelect 里调用OnEndEdit()，调用两遍
        void OnEndEdit(string str)
        {
            DU.Log("编辑结束");
            _onEndEditFunc?.Invoke(str);
        }

        void Update()
        {
            // 按Enter键 选中第一个搜索结果
            if (_match_list != null && _match_list.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    DU.Log("Enter");
                    InputText.text = _match_list[0];
                    OnEndEdit(InputText.text);
                    Utils.SetActive(_tipsComp, false);
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.currentSelectedGameObject != InputText.gameObject)
                {
                    // 点击了节点外部
                    Utils.SetActive(_tipsComp, false);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            DU.Log("编辑开始");
            RefreshTipsComp();
        }
    }
}