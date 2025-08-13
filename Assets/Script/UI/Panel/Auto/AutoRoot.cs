


using System.Collections.Generic;
using Script.Framework.UI;
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


        void Update()
        {
            base.Update();

            // 监听 右Ctrl + 小键盘1
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Keypad1))
            {
                UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Quit();
            }

            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.D))
            {
                UIManager.Inst.ShowPanel(PanelEnum.DrawProcessPanel, null);
            }
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.L))
            {
                UIManager.Inst.ShowPanel(PanelEnum.ProcessNodePanel, null);
            }

            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.O))
            {
                //模拟数据
                var data = new List<string>();
                for (int i = 0; i < 100; i++)
                {
                    string str = $"Item {i}";
                    if (i % 2 == 0)
                    {
                        str += " - 这是一个测试文本，用于展示虚拟列表的功能。";
                    }
                    else
                    {
                        str += " - 这是一个测试文本，用于展示虚拟列表的功能。内容会翻个几倍，看看效果如何。内容会翻个几倍，看看效果如何。内容会翻个几倍，看看效果如何。内容会翻个几倍，看看效果如何。";
                    }
                    data.Add(str);
                }
                UIManager.Inst.ShowPanel(PanelEnum.LogPrintPanel, data);
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
    }
}