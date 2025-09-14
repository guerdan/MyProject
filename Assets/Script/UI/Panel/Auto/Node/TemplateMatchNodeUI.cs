
using System;
using Script.Framework.AssetLoader;
using Script.Model.Auto;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class TemplateMatchNodeUI : ProcessNodeUI
    {
        [SerializeField] private Text TitleText;
        [SerializeField] private Text DelayText;

        [SerializeField] private ImageLoadComp TemplateImage;
        [SerializeField] private Text RegionText;

        protected override void OnEnable()
        {
            base.OnEnable();
            DrawProcessPanel.OnMouseSelected += RefreshTemplateImageBtn;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DrawProcessPanel.OnMouseSelected -= RefreshTemplateImageBtn;
        }


        /// <summary>
        /// 刷新内容
        /// </summary>
        public override void RefreshContent()
        {
            var data = _data as TemplateMatchOperNode;
            TitleText.text = data.Name;
            DelayText.text = $"{Math.Round(data.Delay, 2)}s";
            TemplateImage.SetData(ImageManager.GetFullPath(data.TemplatePath), new Vector2(90, 90), true, 2);
            RegionText.text = data.RegionExpression;
            RefreshTemplateImageBtn();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            RefreshTemplateImageBtn();
        }


        void RefreshTemplateImageBtn()
        {
            bool selected = _data.Id == _panel.MouseSelectedId;
            Utils.SetCanClick(TemplateImage.gameObject, selected && _finishOneClick);

            // DU.LogWarning($"interactable {selected && _finishOneClick}");
        }

    }
}