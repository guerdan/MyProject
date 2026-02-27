
using System;
using Script.Framework.UI;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public enum ConfirmPanelType
    {
        Confirm,            // 确定与取消
        Tip,                // 只有确定。本弹窗不产生影响
        EditInput,          // 有1个编辑栏
        TwoEditInput,       // 有2个编辑栏
    }
    public class ConfirmPanelParam
    {
        public ConfirmPanelType Type;   // 先确定类型, 弹窗会根据Type产生不同的形式
        public string PanelTitle;       // 弹窗标题
        public string Content;          // 提示的文本内容
        public string Region0Title;     // 第一栏标题
        public string Region0Text;      // 第一栏初始内容
        public string Region1Title;     // 第二栏标题
        public string Region1Text;      // 第二栏初始内容
        public Action<string, string> OnConfirm;    // 确认按钮回调
        public Action OnCancel;    // 确认按钮回调

    }
    /// <summary>
    /// 怎么把Change方法传进来
    /// </summary>
    public class ConfirmPanel : BasePanel
    {

        [SerializeField] private Text PanelTitle;
        [SerializeField] private Text ContentText;              // 纯文本
        [SerializeField] private Text Region0Text;              // 输入栏0标题
        [SerializeField] private InputTextComp Region0Input;    // 输入栏0
        [SerializeField] private Text Region1Text;              // 输入栏1标题
        [SerializeField] private InputTextComp Region1Input;    // 输入栏1
        [SerializeField] private Button CancelBtn;
        [SerializeField] private Button ConfirmBtn;
        RectTransform Region0;
        RectTransform Region1;

        ConfirmPanelParam _param;
        ConfirmPanelType _type;

        void Awake()
        {
            _useScaleAnim = false;
            ConfirmBtn.onClick.AddListener(OnClickConfirmBtn);
            CancelBtn.onClick.AddListener(OnClickCancelBtn);

            Region0 = Region0Text.transform.parent.GetComponent<RectTransform>();
            Region1 = Region1Text.transform.parent.GetComponent<RectTransform>();
        }

        public override void SetData(object data)
        {
            if (!(data is ConfirmPanelParam param))
                return;

            _param = param;
            _type = _param.Type;
            PanelTitle.text = param.PanelTitle;

            Clear();

            if (_type == ConfirmPanelType.EditInput || _type == ConfirmPanelType.TwoEditInput)
            {
                bool useRegion1 = _type == ConfirmPanelType.TwoEditInput;

                Utils.SetActive(Region0, true);
                Region0Text.text = param.Region0Title;
                Region0Input.SetData(param.Region0Text, null);

                Utils.SetActive(Region1, useRegion1);
                if (useRegion1)
                {
                    Region1Text.text = param.Region1Title;
                    Region1Input.SetData(param.Region1Text, null);
                }

                if (useRegion1)
                {
                    Region0.anchoredPosition = new Vector2(Region0.anchoredPosition.x, 55);
                    Region1.anchoredPosition = new Vector2(Region1.anchoredPosition.x, -15);
                }
                else
                    Region0.anchoredPosition = new Vector2(Region0.anchoredPosition.x, 20);
            }
            else if (_type == ConfirmPanelType.Confirm || _type == ConfirmPanelType.Tip)
            {
                Utils.SetActive(ContentText, true);
                ContentText.text = _param.Content;

                bool is_tip = _type == ConfirmPanelType.Tip;
                Utils.SetActive(CancelBtn, !is_tip);
            }

        }

        void Clear()
        {
            Utils.SetActive(ContentText, false);
            Utils.SetActive(Region0, false);
            Utils.SetActive(Region1, false);
            Utils.SetActive(CancelBtn, true);
            Utils.SetActive(ConfirmBtn, true);

        }
        void OnClickConfirmBtn()
        {
            string name0 = Region0Input.GetText();
            string name1 = Region1Input.GetText();

            Close();
            _param?.OnConfirm?.Invoke(name0, name1);
        }

        void OnClickCancelBtn()
        {
            Close();
            _param?.OnCancel?.Invoke();
        }
    }
}