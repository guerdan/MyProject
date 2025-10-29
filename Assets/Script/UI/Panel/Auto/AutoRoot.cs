


using System;
using System.Collections.Generic;
using System.Drawing;
using Script.Framework.UI;
using Script.Model.Auto;
using Script.Util;
using Unity.VisualScripting;
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

            // WU.Init();
            // WU.AddMouseListener(MouseRecord);
            // WU.AddKeyboardListener(KeyboardRecord);
        }

        void OnDestroy()
        {
            // WU.RemoveMouseListener(MouseRecord);
            // WU.RemoveKeyboardListener(KeyboardRecord);
            // WU.Release();

        }

        void Start()
        {
            // 获取 System.Drawing 的加载路径
            // string assemblyPath = typeof(Bitmap).Assembly.Location;
            // DU.LogWarning($"System.Drawing.dll is loaded from: {assemblyPath}");
        }

        void KeyboardRecord(KeyboardHookEnum action, WU.KBDLLHOOKSTRUCT hookStruct)
        {

            // 普通键，
            if (action == KeyboardHookEnum.KeyDown)
            {
                if (hookStruct.vkCode == (uint)KeyboardEnum.Esc)
                {
                    string id = DrawProcessPanel.LastOpenId;
                    AutoScriptManager.Inst.StopScript(id);
                }
            }

        }


        void Update()
        {
            base.Update();

            // 监听 右Ctrl + 小键盘1
            // if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Keypad1))
            // {
            //     UIManager.Inst.ShowPanel(PanelEnum.HeroDetailPanel, null);
            // }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Quit();
            }


            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.L))
            {
                UIManager.Inst.ShowPanel(PanelEnum.ProcessNodeInfoPanel, null);
            }

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.O))
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

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.A))
            {
                var data = new List<string>() { "MatchSource/folder.png", "MatchTemplate/folder_transparent.png" };
                UIManager.Inst.ShowPanel(PanelEnum.PicMatchFloat, data);
            }

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.S))
            {
                UIManager.Inst.ShowPanel(PanelEnum.ScriptManagerPanel, null);
            }

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.D))
            {
                string id = DrawProcessPanel.LastOpenId;
                if (id != null)
                {
                    AutoScriptManager.Inst.StopScript(id);
                    UIManager.Inst.ShowPanel(PanelEnum.DrawProcessPanel, id);
                }
            }
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.T))
            {
                UIManager.Inst.ShowPanel(PanelEnum.ImageMatchTestPanel, null);
            }

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
    }
}