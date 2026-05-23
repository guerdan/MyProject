
using System;
using System.Collections.Generic;
using Script.Framework;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.UI.Components;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panel.Auto
{
    public class ScriptManagerSettingsPanel : BasePanel
    {
        public AutoScriptManager Manager => AutoScriptManager.Inst;


        [SerializeField] private InputTextSV InputTextSV;
        [SerializeField] private Button PipeSettingsBtn;


        InputTextSVItemData[] _inParam_edit_list;


        void Awake()
        {
            PipeSettingsBtn.onClick.AddListener(OnClickPipeSettingsBtn);
            Utils.SetActive(InputTextSV, false);
        }

        void OnDisable()
        {
            Manager.SaveAutoScriptSettings();
        }

        public override void SetData(object data)
        {
            Init();
        }


        void Init()
        {

        }


        void OnClickPipeSettingsBtn()
        {
            Utils.SetActive(InputTextSV, true);
            var settings = Manager.Settings;
            var edit_list = new List<InputTextSVItemData>();
            foreach (var k in settings.PipeMapping)
            {
                edit_list.Add(new InputTextSVItemData($"{k[0]}:", k[1]));
            }

            InputTextSV.SetData(edit_list, OnClosePipeSettingsBtn, 500, 7);

            var rectT = InputTextSV.GetComponent<RectTransform>();
            var targetR = PipeSettingsBtn.GetComponent<RectTransform>();
            var offset = new Vector2(targetR.rect.width / 2, targetR.rect.height / 2) + new Vector2(5, -5);
            var pos = Utils.GetPos(rectT, targetR, offset, true);
            rectT.anchoredPosition = pos;
        }

        void OnClosePipeSettingsBtn(List<InputTextSVItemData> data)
        {
            var mapping = Manager.Settings.PipeMapping;
            for (int i = 0; i < data.Count; i++)
            {
                mapping[i][1] = data[i].Content;
            }

            // 因为我全部重刷。相同的pipe必须先销毁，再创建。 
            // 而 DestroyNamePipe()慢于InitNamePipe() 故给InitNamePipe()加延迟
            // 
            Manager.DestroyNamePipe();
            Manager.InitNamePipe();

        }

    }
}