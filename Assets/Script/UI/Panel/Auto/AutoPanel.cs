using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Script.Framework;
using Script.Framework.UI;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;


namespace Script.UI.Panel.Auto
{
    public class AutoPanel : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Button startBtn;
        [SerializeField] private Button miniBtn;
        [SerializeField] private Button closeBtn;
        [SerializeField] private Button syncOperBtn;
        [SerializeField] private Button captureBtn;
        [SerializeField] private Text infoText;
        [SerializeField] private Text consoleText;
        [SerializeField] private VirtualListComp listComp;
        [SerializeField] private GameObject template;

        private bool selectSwitch = false;
        private bool syncOperSwitch = false;
        private IntPtr selectedWin = IntPtr.Zero;    //已选择的窗口

        void Start()
        {
            button?.onClick.AddListener(OnClick);
            startBtn?.onClick.AddListener(OnStartBtnClick);
            syncOperBtn?.onClick.AddListener(OnSyncOperBtnClick);
            miniBtn?.onClick.AddListener(OnMiniBtnClick);
            closeBtn?.onClick.AddListener(OnCloseBtnClick);
            captureBtn?.onClick.AddListener(OnCaptureBtnClick);
            // AU.Init();
            // AU.AddMouseListener(MouseRecord);
            // AU.AddKeyboardListener(KeyboardRecord);
            GameTimer.Inst.SetTimeOnce(this, () =>
            {
            }, 1);

            OCVExample.Inst.Init();

            // template.SetActive(false);
            // listComp.OnGetItemSize = GetItemSize;
            // listComp.OnGetItemTemplate = GetItemTemplate;
            // listComp.OnUpdateItem = UpdateItem;
            // ShowList();
        }
        void OnDestroy()
        {
            button?.onClick.RemoveListener(OnClick);
            startBtn?.onClick.RemoveListener(OnStartBtnClick);
            syncOperBtn?.onClick.RemoveListener(OnSyncOperBtnClick);
            miniBtn?.onClick.RemoveListener(OnMiniBtnClick);
            closeBtn?.onClick.RemoveListener(OnCloseBtnClick);
            captureBtn?.onClick.RemoveListener(OnCaptureBtnClick);
            // AU.Release();
            // AU.RemoveMouseListener(MouseRecord);
            // AU.RemoveKeyboardListener(KeyboardRecord);

        }


        void OnClick()
        {
            TestMouseClick();
        }
        void OnStartBtnClick()
        {
            selectSwitch = true;
            var img = startBtn.GetComponent<Image>();
            img.color = selectSwitch ? Color.red : Color.white;
        }
        void OnSyncOperBtnClick()
        {
            syncOperSwitch = !syncOperSwitch;
            var img = syncOperBtn.GetComponent<Image>();
            img.color = syncOperSwitch ? Color.red : Color.white;
        }

        void TestMouseClick()
        {
            AU.EnumWindows((hWnd, lParam) =>
            {
                // 检查窗口是否可见
                if (AU.IsWindowVisible(hWnd))
                {
                    StringBuilder sb = new StringBuilder(256);
                    AU.GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    if (!string.IsNullOrEmpty(title))
                    {
                        IntPtr exStyle = AU.GetWindowLong(hWnd, AU.GWL_EXSTYLE);
                        // 筛选应用程序窗口
                        if (exStyle.ToInt64() == AU.WS_EX_APPWINDOW)
                        {
                            Debug.Log($"任务栏窗口句柄: {hWnd}, 标题: {title}");
                        }
                        // Debug.Log($"窗口句柄: {hWnd}, 样式: {exStyle}, 标题: {title}");
                    }
                }
                return true; // 返回 true 继续枚举
            }, IntPtr.Zero);



            IntPtr hWnd = AU.FindWindow(null, "win接口.md - Typora");
            if (hWnd == IntPtr.Zero)
            {
                Debug.Log("未找到窗口");
                return;
            }
            Debug.Log($"窗口句柄: {hWnd}");
            DU.Log(AU.GetWindowRect(hWnd));
            AU.SimulateMouseClick(hWnd, 220, 170);
        }


        #region 

        void Update()
        {
            
        }




        #endregion

        #region 

        void MouseRecord(MouseHookEnum action, AU.MSLLHOOKSTRUCT hookStruct)
        {

            // 判断鼠标事件类型
            if (action == MouseHookEnum.LeftUp)
            {
                IntPtr win = AU.WindowFromPoint(hookStruct.pt);
                string p = AU.PrintHandle(win);

                if (selectSwitch)
                {
                    selectedWin = win;
                    selectSwitch = false;
                    var img = startBtn.GetComponent<Image>();
                    img.color = selectSwitch ? Color.red : Color.white;
                    infoText.text = p;
                }

                if (selectedWin == IntPtr.Zero)
                {
                    infoText.text = p;
                }

                // DU.Log($"左键 ({hookStruct.pt.x}, {hookStruct.pt.y})");
            }
            else if (action == MouseHookEnum.RightUp)
            {
                // DU.Log($"右键 ({hookStruct.pt.x}, {hookStruct.pt.y})");
            }
        }

        /// <summary>
        /// 监听几个按键
        /// </summary>
        private KeyboardKeyEnum[] list = new KeyboardKeyEnum[]
        {
            // KeyboardKeyEnum.W,
            // KeyboardKeyEnum.A,
            KeyboardKeyEnum.S,
            KeyboardKeyEnum.D,
            KeyboardKeyEnum.C,
            KeyboardKeyEnum.I,
            KeyboardKeyEnum.P,
            KeyboardKeyEnum.O,
            KeyboardKeyEnum.Esc,
            KeyboardKeyEnum.Up,
            KeyboardKeyEnum.Down,
            KeyboardKeyEnum.Left,
            KeyboardKeyEnum.Right,
            KeyboardKeyEnum.V,
        };
        private HashSet<KeyboardKeyEnum> repeatKeySet = new HashSet<KeyboardKeyEnum>();

        // 按键回调
        void KeyboardRecord(KeyboardHookEnum action, AU.KBDLLHOOKSTRUCT hookStruct)
        {
            if (!syncOperSwitch) return;

            if (selectedWin != IntPtr.Zero)
            {
                if (action == KeyboardHookEnum.KeyDown)
                {
                    foreach (var key in list)
                    {
                        KeyOper(key, hookStruct);
                    }

                }
                if (action == KeyboardHookEnum.KeyUp)
                {
                    foreach (var key in list)
                    {
                        KeyOper(key, hookStruct, false);
                    }
                }
            }

            // 普通键，
            if (action == KeyboardHookEnum.KeyDown)
            {
                infoText.text = $"按键 ({hookStruct.vkCode})";
                // DU.Log($"按键 ({hookStruct.vkCode})");
            }


        }

        private void KeyOper(KeyboardKeyEnum key, AU.KBDLLHOOKSTRUCT hookStruct, bool isDown = true)
        {
            if (hookStruct.vkCode != (uint)key) return;
            if (isDown)
            {
                if (key == KeyboardKeyEnum.V)
                {
                    key = KeyboardKeyEnum.W;
                    // AU.SendKeyPress((int)KeyboardKeyEnum.W);
                }

                bool repeat = repeatKeySet.Contains(key);
                // AU.PostMessagePacked(selectedWin, key, repeat);
                AU.keybd_eventPacked((int)key);
                repeatKeySet.Add(key);
            }
            else
            {
                if (key == KeyboardKeyEnum.V)
                {
                    key = KeyboardKeyEnum.W;
                    // AU.SendInputKeyDown((int)KeyboardKeyEnum.A, false);
                }
                // else
                // {
                // AU.PostMessagePacked(selectedWin, key, true, false);
                AU.keybd_eventPacked((int)key, false);
                repeatKeySet.Remove(key);
                // }

            }

            var msg = $"按键按下 ({key})";
            consoleText.text = msg;
            DU.Log(msg);
        }


        #endregion


        void OnMiniBtnClick()
        {
            AutoRoot.Inst.Minimize();
        }
        void OnCloseBtnClick()
        {
            AutoRoot.Inst.Quit();
        }
        void OnCaptureBtnClick()
        {
            // var stopwatch = new Stopwatch();
            // stopwatch.Start();
            // stopwatch.Stop();
            // Debug.Log("step 2: " + stopwatch.ElapsedMilliseconds + " ms");


            string path = Path.Combine(Application.streamingAssetsPath, "pic_chuan.png");
            // 15ms  耗时还好
            var bitmap = AU.CaptureWindow(selectedWin, path);
            // 15ms
            var mat = OCVExample.BitmapToMat(bitmap);

            OCVExample.ShowMat(mat);
        }




        // 前台点击
        // AU.MessageBox(IntPtr.Zero, "Hello from Win32 API!", "Win32 MessageBox", 0);
        // AU.SetCursorPos(21, 44);
        // AU.mouse_event(AU.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        // AU.mouse_event(AU.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);


    }

}