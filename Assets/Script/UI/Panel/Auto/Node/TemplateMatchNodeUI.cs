
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto.Node
{
    public class TemplateMatchNodeUI : ProcessNodeUI
    {
        [SerializeField] private Text TitleText;
        [SerializeField] private Text DelayText;

        /// <summary>
        /// 刷新内容
        /// </summary>
        public override void RefreshContent()
        {
            TitleText.text = _data.Name;
            DelayText.text = $"{Math.Round(_data.Delay, 2)}s";
        }
    }
}