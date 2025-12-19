
using System;
using System.Collections.Generic;
using Script.Framework.UI;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class EditNamePanelParam
    {
        /// <summary>
        /// 参照点，固定取(0.5,0.5)点
        /// </summary>
        public RectTransform Target;
        /// <summary>
        /// 参照点偏移
        /// </summary>
        public Vector2 Offset;
        public string PanelTitle;
        public string Region0Title;     // 第一栏标题
        public string Region0Text;      // 第一栏初始内容
        public string Region1Title;     // 第二栏标题
        public string Region1Text;      // 第二栏初始内容
        public Action<string, string> OnConfirm;// 确认按钮回调
    }
    /// <summary>
    /// 怎么把Change方法传进来
    /// </summary>
    public class EditNamePanel : BasePanel
    {

        [SerializeField] private Text PanelTitle;
        [SerializeField] private Text Region0Text;
        [SerializeField] private InputTextComp Region0Input;
        [SerializeField] private Text Region1Text;
        [SerializeField] private InputTextComp Region1Input;
        [SerializeField] private Button ConfirmBtn;

        EditNamePanelParam _param;

        void Awake()
        {
            ConfirmBtn.onClick.AddListener(OnConfirmBtnClick);
        }

        public override void SetData(object data)
        {
            _useScaleAnim = false;

            if (!(data is EditNamePanelParam param)) return;
            _param = param;
            PanelTitle.text = param.PanelTitle;

            bool useRegion0 = !string.IsNullOrEmpty(param.Region0Title);
            Utils.SetActive(Region0Text, useRegion0);
            Utils.SetActive(Region0Input, useRegion0);
            if (useRegion0)
            {
                Region0Text.text = param.Region0Title;
                Region0Input.SetData(param.Region0Text, null);
            }


            bool useRegion1 = !string.IsNullOrEmpty(param.Region1Title);
            Utils.SetActive(Region1Text, useRegion1);
            Utils.SetActive(Region1Input, useRegion1);
            if (useRegion1)
            {
                Region1Text.text = param.Region1Title;
                Region1Input.SetData(param.Region1Text, null);
            }



        }
        public override void BeforeShow()
        {
            SetPos();
            base.BeforeShow();
        }

        void SetPos()
        {
            if (_param == null) return;
            var rT = (RectTransform)transform;
            var pos = Utils.GetPos((RectTransform)transform, _param.Target, _param.Offset);
            pos.y = pos.y - rT.rect.height / 2;
            PanelDefine.InitPos = pos;
        }

        void OnConfirmBtnClick()
        {
            string name0 = Region0Input.GetText();
            string name1 = Region1Input.GetText();

            Close();
            _param?.OnConfirm?.Invoke(name0, name1);
        }
    }
}