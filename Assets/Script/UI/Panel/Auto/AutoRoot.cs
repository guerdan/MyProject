


using System.Collections.Generic;
using Script.Framework;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Util;
using UnityEngine;

namespace Script.UI.Panel.Auto
{
    /// <summary>
    /// 每个大模块来一个Root
    /// </summary>
    public class AutoRoot : Root
    {
        protected static AutoRoot inst;
        public static AutoRoot Inst { get { return inst; } }


        void Awake()
        {
            base.Awake();
            inst = this;
        }


        void Start()
        {
            base.Start();

            Application.targetFrameRate = 30;
            AutoScriptManager.Inst.Init();

            // 打开桌宠
            PanelUtil.PanelDefineDic[PanelEnum.DeskPetMain].InitPos =
                new Vector2(100 - Screen.width / 2, Screen.height / 2 - 100);
            UIManager.Inst.ShowPanel(PanelEnum.DeskPetMain, null);
        }


        void Update()
        {
            base.Update();

            // 监听 右Ctrl + 小键盘1
            // if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Keypad1))
            // {
            //     UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
            // }

            float delta = Time.deltaTime;
            AutoScriptManager.Inst.OnUpdate(delta);



            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.C))
            {
                UIManager.Inst.ShowPanel(PanelEnum.ImageCompareTestPanel, null);
            }
        }


        public void Quit()
        {
            Application.Quit();
        }
        public void Minimize()
        {
            TransparentWindow.Main.MinimizeWindow();
        }
        public void Show()
        {
            TransparentWindow.Main.ShowWindow();
        }

        void OnDestroy()
        {
            AutoScriptManager.Inst.OnDestroy();
        }

    }
}