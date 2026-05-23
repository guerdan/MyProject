
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Script.Framework;
using Script.Model.Auto;
using Script.UI.Panel.Auto;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Components
{
    //提取关键词委托
    public delegate void ExtractKeywordDelegate(string resource, int caretPos, out string keyword, out int startIndex, out string back);


    /// <summary>
    /// 具有功能：变量关键词提示，检查合法性，Enter键/或鼠标点击 提示词可以应用补全
    /// 可以优化功能：1. 应用补全的提示词是方法时，自动补上{}
    /// 2. I18N配置表，菜单执行加密。这里提示方法信息，变量解释
    /// </summary>
    public class InputTextProComp : MonoBehaviour
    {

        [SerializeField] private CustomInputField InputText;
        [SerializeField] public CheckBox ValidCheck;


        Action<string> _onEndEditFunc;          //输入结束行为,一般要保存编辑后的数据
        Action<string> _onValueChangedFunc;     //输入每个字符时行为


        KeywordTipsComp _tipsComp;                      //提示词组件
        Func<string, List<TipMatchItem>> _matchFunc;    //提示词，搜索方法
        ExtractKeywordDelegate _extractKeyFunc;         //提示词，提取关键词方法
        List<TipMatchItem> _match_result;               //提示词，搜索结果
        string _keyword;                        //提示词，关键词根据光标位置提取
        int _keywordStart;                      //提示词，关键词起始位置
        string _keywordBack;                    //提示词的后一个词

        Vector2 _relativePos;                   //相对位置
        Func<string, bool> _checkFunc;          //检查方法

        Color ori_select_color;

        void Awake()
        {
            InputText.onValueChanged.AddListener(OnValueChanged);
            InputText.onEndEdit.AddListener(OnEndEdit);
            InputText.onPointerDownCustom = OnPointerDown;

            ori_select_color = InputText.selectionColor;
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

            InputText.selectionColor = ori_select_color;
        }

        /// <summary>
        /// 开启关键词提示功能
        /// </summary>
        public void UseKeywordTips(KeywordTipsComp keywordTipsComp, Func<string, List<TipMatchItem>> matchFunc
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
            if (str.IndexOf('\n') >= 0)
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
        // 先触发OnEndEdit(), 后帧再触发点击KeywordTipsComp的点击事件,以及Update的Enter键
        // 所以闪烁是不可避免了。因为InputTextSV依赖OnEndEdit()的瞬时性
        void OnEndEdit(string str)
        {
            _onEndEditFunc?.Invoke(str);
            // DU.Log($"OnEndEdit {Time.frameCount}");

            // InputTextSV会出问题
            // GameTimer.Inst.SetTimeOnce(this, () =>
            // {
            // }, 0.1f);
        }


        void TipConfirm(int index)
        {
            var item = _match_result[index];
            string result;
            if (item.Type == 1 && _keywordBack != "{")
                result = item.OriStr + "{}";
            else
                result = item.OriStr;

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

            InputText.ActivateInputField();
            InputText.text = temp;
            InputText.selectionColor = Utils.TransparentCol;

            // 晚一帧才有效，效果有限，会闪一下全选。点击事件（物理/UI） ➔ Update ➔ LateUpdate
            GameTimer.Inst.SetTimeOnce(this, () =>
            {
                InputText.caretPosition = start + result.Length;
                InputText.ForceLabelUpdate();
                InputText.selectionColor = ori_select_color;
            }, 0);
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
                , default, true);


            //没有提取函数，就用全部文本
            if (_extractKeyFunc == null)
            {
                _keyword = InputText.text;
                _keywordStart = 0;
            }
            else
            {
                _extractKeyFunc(InputText.text, caretPos, out _keyword, out _keywordStart, out _keywordBack);
            }

            // DU.LogWarning($"关键词{_keyword}  起点{_keywordStart}");


            _match_result = _matchFunc(_keyword.Trim());
            bool show = _match_result.Count > 0 && _match_result[0].OriStr != _keyword.Trim();
            if (show)
                StartCoroutine(DelayShowTipsComp());
            else
                Utils.SetActive(_tipsComp, false);
        }

        void RefreshCheckBox()
        {
            if (ValidCheck == null || _checkFunc == null) return;

            bool legal = _checkFunc(InputText.text);
            ValidCheck.SetData(legal);
        }

        /// <summary>
        /// 由于当text改变时就调用OnValueChanged，在渲染之前，就无法获得新字符的位置。故而延迟一帧
        /// caret的渲染内置到网格了(查看源码)，无法获取
        /// </summary>
        IEnumerator DelayShowTipsComp()
        {
            yield return null;
            Vector2 localPos = Vector2.zero;

            var textComp = InputText.textComponent;
            int caretPos = InputText.caretPosition;

            int draw_start = GetCaretDrawStart();
            int index = caretPos - 1 - draw_start;
            if (caretPos == 0 || index >= textComp.text.Length)
                yield break;


            Utils.SetActive(_tipsComp, true);
            var textGen = textComp.cachedTextGenerator;


            // 获取光标当前的占位, 为字符的右上角坐标
            var charInfo = textGen.characters[index];
            // 加点偏移，然后因字体缩放而除2。
            localPos = charInfo.cursorPos - new Vector2(0, textComp.fontSize + 6);
            localPos = localPos / 2;
            // DU.LogWarning(localPos);

            var tipsCompRectT = _tipsComp.GetComponent<RectTransform>();
            tipsCompRectT.anchoredPosition = _relativePos + localPos;


            // 点击搜索结果
            var strs = _match_result.ConvertAll((item) => item.OriStr);
            _tipsComp.SetData(strs,
            index =>
            {
                TipConfirm(index);

                _onEndEditFunc?.Invoke(InputText.text);
                // DU.Log($"TipConfirm {Time.frameCount}");
            });
            _tipsComp.SetAutoCloseWithoutArea(new List<GameObject>() { gameObject });
        }


        /// <summary>
        /// 获取光标相对于 InputField 的局部坐标（Local Position）
        /// </summary>
        public int GetCaretDrawStart()
        {

            // 1. 使用反射获取 InputField 内部私有的光标 RectTransform
            // PropertyInfo property = typeof(InputField).GetProperty("caretRectTrans", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fieldInfo = typeof(InputField).GetField("m_DrawStart", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                int draw_start = (int)fieldInfo.GetValue(InputText);
                return draw_start;
            }

            return 0;
        }



        void Update()
        {
            if (!InputText.isFocused)
                return;

            bool up_move = Input.GetKeyDown(KeyCode.UpArrow);
            bool down_move = Input.GetKeyDown(KeyCode.DownArrow);

            float scrollValue = Input.GetAxis("Mouse ScrollWheel");

            if (up_move || scrollValue > 0)
            {
                MoveUp(true);
                InputText.ForceLabelUpdate();
            }
            else if (down_move || scrollValue < 0)
            {
                MoveUp(false);  // 遇到尾巴时，caret会溢出，强刷就行了
                InputText.ForceLabelUpdate();
            }
        }

        // 反射
        private void MoveUp(bool isUp)
        {
            MethodInfo method = typeof(InputField).GetMethod(isUp ? "MoveUp" : "MoveDown",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(bool), typeof(bool) }, // 按顺序传入参数的 Type
                null);

            object[] param = new object[2];
            param[0] = false;
            param[1] = false;
            if (method != null)
            {
                method.Invoke(InputText, param);
            }
        }

        /// <summary>
        /// 上下选个最近的
        /// 想要准确知道未知行， 就得用Util模拟上下行。
        /// </summary>
        void MoveCaret(bool isUp)
        {
            DU.LogWarning("MoveCaret");
            int ori = InputText.caretPosition;
            int start = GetCaretDrawStart();
            int ori_index = ori - start;

            var textGen = InputText.textComponent.cachedTextGenerator;
            var lines = textGen.lines;
            var chars = textGen.characters;
            var ori_ci = chars[ori_index];
            int tLine = 0;      // target_line
            int end = 0;

            if (!isUp)
                for (int i = 1; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line.startCharIdx > ori_index)
                    {
                        tLine = i;
                        end = tLine + 1 < lines.Count ? lines[tLine + 1].startCharIdx : chars.Count;
                        break;
                    }
                }
            else
                for (int i = lines.Count - 1; i >= 1; i--)
                {
                    var line = lines[i];
                    if (line.startCharIdx < ori_index)
                    {
                        tLine = i - 1;
                        end = tLine + 1 < lines.Count ? lines[tLine + 1].startCharIdx : chars.Count;
                        break;
                    }
                }


            int target = 0;
            var find_start = lines[tLine].startCharIdx;
            for (int j = find_start; j < end; j++)
            {
                var ci = chars[j];
                if (ci.cursorPos.x > ori_ci.cursorPos.x)
                {
                    target = j + start;
                    if (j > find_start)
                    {
                        var delta1 = ci.cursorPos.x - ori_ci.cursorPos.x;
                        var delta2 = ori_ci.cursorPos.x - chars[j - 1].cursorPos.x;
                        if (delta2 < delta1)
                            target--;
                    }
                    break;
                }
            }

            // 说明在结尾
            if (target == 0)
                target = end - 1;

            InputText.caretPosition = target;
            InputText.ForceLabelUpdate();
        }
    }
}