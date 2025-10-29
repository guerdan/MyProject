using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if IGNORE_COMPILE_TIP
using zFramework.IO;
#endif


namespace Script.Util
{
    /// <summary>
    /// Win-Utils，简称AU，负责封装win接口
    /// </summary>
    public static class WU
    {
        #region 鼠标API

        /// <summary>
        /// 获取当前鼠标位置
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out System.Drawing.Point lpPoint);
        /// <summary>
        /// 设置鼠标位置
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);
        /// <summary>
        /// 执行鼠标操作。左键/右键/中键按下、抬起，滚轮滑动等操作
        /// </summary>
        /// <param name="dwFlags">操作事件枚举</param>
        /// <param name="dx">如果 MOUSEEVENTF_MOVE，则为水平移动量。如果 MOUSEEVENTF_ABSOLUTE，则为绝对值</param>
        /// <param name="dy">同上，不同的是垂直方向</param>
        /// <param name="dwData">如果 MOUSEEVENTF_WHEEL，则 dwData 表示滚轮滚动的距离</param>
        /// <param name="dwExtraInfo">应用程序定义的附加信息，通常为 UIntPtr.Zero</param>
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        /// <summary>
        /// 鼠标操作事件常量,dwFlags参数
        /// </summary>
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point point);
        /// <summary>
        /// 设置鼠标光标的可见性
        /// </summary>
        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool bShow);

        #endregion

        #region 键盘API

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("Kernel32.dll", EntryPoint = "GetTickCount", CharSet = CharSet.Auto)]
        internal static extern int GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            // 还有MOUSEINPUT等，这里省略
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;// 虚拟键码（Virtual-Key Code）
            public ushort wScan;//硬件扫描码（Scan Code），对应物理键盘上的按键编码。通常可以设为 0，或者用 MapVirtualKey 转换。
            public uint dwFlags;//行为标志位。常用值：0:按下（key down）; 0x0002：抬起（key up）;0x0008：使用扫描码而不是虚拟键码
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const int INPUT_KEYBOARD = 1;
        const int KEYEVENTF_KEYUP = 0x0002;
        const int KEYEVENTF_SCANCODE = 0x0008;
        public static void SendInputKeyDown(int vkCode, bool isDown = true)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)vkCode; // 推荐为0
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = (uint)(isDown ? 0 : KEYEVENTF_KEYUP);
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendKeyPress(int vkCode)
        {
            // SendInputKeyDown(vkCode);
            // System.Threading.Thread.Sleep(200);
            // SendInputKeyDown(vkCode, false);

            // 按下
            keybd_event((byte)vkCode, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(200); // 可根据需要调整延迟
                                                // 抬起
            keybd_event((byte)vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // 2 = KEYEVENTF_KEYUP
        }


        public static void PostMessagePacked(IntPtr handle, KeyboardEnum key, bool isRepeat, bool isDown = true)
        {
            int repeatCount = 1;
            int scanCode = (int)MapVirtualKey((uint)key, 0);  /* 你的扫描码 */
            int extended = 0; // 一般为0，扩展键如右侧Ctrl/Alt/方向键为1
            int repeat = isRepeat ? 1 : 0; //之前是否被按下过

            int lParam = (repeatCount & 0xFFFF) | ((scanCode & 0xFF) << 16) | (repeat << 30) | (extended << 24);
            var flag = isDown ? KeyboardHookEnum.KeyDown : KeyboardHookEnum.KeyUp;
            PostMessage(handle, (uint)flag, (IntPtr)key, (IntPtr)lParam);
        }

        public static void keybd_event_packed(int vkCode, bool isDown = true)
        {
            var flag = isDown ? 0 : KEYEVENTF_KEYUP;
            keybd_event((byte)vkCode, 0, (uint)flag, UIntPtr.Zero);
        }

        #endregion
        #region 窗口API

        /// <summary>
        /// 设置焦点窗口
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// 对后台窗口发送消息。鼠标操作或键盘操作
        /// WM_SETTEXT	0x000C	设置窗口的文本（标题或内容）。
        /// WM_GETTEXT	0x000D	获取窗口的文本（标题或内容）。
        /// WM_CLOSE	0x0010	关闭窗口。
        /// WM_COMMAND	0x0111	发送命令消息（如按钮点击）。
        /// WM_KEYDOWN	0x0100	模拟按键按下。
        /// WM_KEYUP	0x0101	模拟按键释放。
        /// WM_LBUTTONDOWN	0x0201	模拟鼠标左键按下。
        /// WM_LBUTTONUP	0x0202	模拟鼠标左键释放。
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="Msg">事件类型</param>
        /// <param name="wParam">附加参数，word parameter</param>
        /// <param name="lParam">附加参数，long parameter</param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", EntryPoint = "PostMessage")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        /// <summary>
        /// 查找窗口句柄
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        /// <summary>
        /// 遍历系统内所有顶层窗口（不包括子窗口）
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        /// <summary>
        /// 查找某窗口的子窗口，"Ex"是"Extended"的简称
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        /// <summary>
        /// 获取窗口标题
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
        /// <summary>
        /// 获取可见的窗口
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public const int GWL_EXSTYLE = -20; // 获取扩展窗口样式
        public const int WS_EX_APPWINDOW = 256; // 应用程序窗口

        public static Vector4 GetWindowRect(IntPtr hWnd)
        {
            if (GetWindowRect(hWnd, out RECT rect))
            {
                return new Vector4(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
            else
            {
                DU.LogError("获取Rect失败");
                return new Vector4(0, 0, 0, 0);
            }
        }



        #endregion
        #region 资源管理器
        public static string OpenFileDialog(string title, string initialPath, params string[] extension)
        {
            var paths = FileDialog.SelectFile(title, initialPath, extension);
            if (paths == null)
                return "";
            string path = paths.Count > 0 ? paths[0] : "";
            path = path.Replace('\\', '/');
            return path;
        }

        public static string SaveFileDialog(string title, string initialPath, string extension)
        {
            string msg = string.Empty;
            string path = "";
            path = FileDialog.SaveDialog(title, initialPath, extension);

            if (!string.IsNullOrEmpty(path))
                path = path.Replace('\\', '/');
            return path;
        }


        #endregion


        #region 鼠标监听
        //录制用户操作
        private static event Action<MouseHookEnum, MSLLHOOKSTRUCT> MouseEvent = null;
        private const int WH_MOUSE_LL = 14; // 鼠标全局钩子
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static LowLevelProc _mouseProc = MouseHookCallback;

        // 定义回调函数类型
        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // 鼠标事件结构体
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public Point pt; // 鼠标位置
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int x;
            public int y;
        }

        // 初始化
        public static void Init()
        {
            // 设置鼠标钩子
            _mouseHookID = SetHook(WH_MOUSE_LL, _mouseProc);
            // 设置键盘钩子
            _keyboardHookID = SetHook(WH_KEYBOARD_LL, _keyboardProc);
        }
        // 卸载
        public static void Release()
        {
            // 卸载钩子
            UnhookWindowsHookEx(_mouseHookID);
            UnhookWindowsHookEx(_keyboardHookID);
        }

        public static void AddMouseListener(Action<MouseHookEnum, MSLLHOOKSTRUCT> action)
        {
            MouseEvent += action;
        }
        public static void RemoveMouseListener(Action<MouseHookEnum, MSLLHOOKSTRUCT> action)
        {
            MouseEvent -= action;
        }

        // 钩子函数是挂在当前进程（你的程序）上的，用于捕获全局或本进程的鼠标/键盘事件。
        private static IntPtr SetHook(int idHook, LowLevelProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(idHook, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        /// <summary>
        /// 鼠标钩子回调函数
        /// </summary>
        /// <param name="nCode">状态码</param>
        /// <param name="wParam">鼠标事件类型</param>
        /// <param name="lParam">指向包含事件附加信息的结构体。
        /// 这里指向一个 MSLLHOOKSTRUCT 结构体，包含鼠标事件的详细信息</param>
        /// <returns></returns>
        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode >= 0)
            {
                // 获取鼠标事件信息
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                //如果MouseEvent为空则系统因钩子报错而杀其进程，故而改为安全的"?."写法
                MouseEvent?.Invoke((MouseHookEnum)wParam, hookStruct);
            }

            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }


        #endregion
        #region 键盘监听
        //录制用户操作
        private static event Action<KeyboardHookEnum, KBDLLHOOKSTRUCT> KeyboardEvent = null;
        private const int WH_KEYBOARD_LL = 13; // 键盘全局钩子
        private static IntPtr _keyboardHookID = IntPtr.Zero;
        private static LowLevelProc _keyboardProc = KeyboardHookCallback;

        // 键盘事件结构体
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;        // 虚拟键码  在wParma
            public uint scanCode;      // 硬件扫描码 在lParam
            public uint flags;         // 事件标志  通过flag传
            public uint time;          // 时间戳
            public IntPtr dwExtraInfo; // 附加信息
        }

        public static void AddKeyboardListener(Action<KeyboardHookEnum, KBDLLHOOKSTRUCT> action)
        {
            KeyboardEvent += action;
        }
        public static void RemoveKeyboardListener(Action<KeyboardHookEnum, KBDLLHOOKSTRUCT> action)
        {
            KeyboardEvent -= action;
        }


        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                KeyboardEvent?.Invoke((KeyboardHookEnum)wParam, hookStruct);
            }
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        #endregion

        #region  封装API
        /// <summary>
        /// 后台鼠标点击。x和y是相对于窗口的坐标,原点是窗口左上角
        /// </summary>
        public static void SimulateMouseClick(IntPtr hWnd, int x, int y)
        {
            const uint WM_MOUSEMOVE = 0x0200; // 鼠标左键按下
            const uint WM_LBUTTONDOWN = 0x0201; // 鼠标左键按下
            const uint WM_LBUTTONUP = 0x0202;   // 鼠标左键释放

            // 将 x 和 y 坐标打包到 lParam
            IntPtr lParam = (IntPtr)((y << 16) | x);

            SendMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            SendMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
            // SendMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)0x0001, lParam);
            // SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        public static string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(256); // 创建缓冲区
            int length = GetWindowText(hWnd, sb, sb.Capacity); // 获取窗口标题
            return length > 0 ? sb.ToString() : string.Empty; // 返回标题或空字符串
        }

        #endregion

        #region  截屏

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        public static Bitmap CaptureWindowByHWnd(IntPtr hWnd, string filePath)
        {
            // IntPtr hdcSrc = GetWindowDC(hWnd);
            // IntPtr hdcDest = CreateCompatibleDC(hdcSrc);
            // IntPtr hBitmap = CreateCompatibleBitmap(hdcSrc, width, height);
            // IntPtr hOld = SelectObject(hdcDest, hBitmap);

            // BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, 0x00CC0020); // SRCCOPY

            // Bitmap bmp = Image.FromHbitmap(hBitmap);

            // SelectObject(hdcDest, hOld);
            // DeleteObject(hBitmap);
            // DeleteDC(hdcDest);
            // ReleaseDC(hWnd, hdcSrc);
            // return bmp;


            // 1. 获取窗口区域
            RECT rect;
            GetWindowRect(hWnd, out rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            // 2. 截取全屏
            Bitmap screenBmp = new Bitmap(width, height);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(screenBmp))
            {
                //第1、2个参数：屏幕起始坐标（如 0,0）
                //第3、4个参数：目标 Bitmap 的起始坐标（如 0,0）
                //第5个参数：复制的区域大小
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, screenBmp.Size);
            }

            return screenBmp;
        }


     /// <summary>
     /// 全屏25ms, bitmap.PixelFormat = Format32bppArgb
     /// </summary>
        public static Bitmap CaptureWindow(CVRect rect)
        {
            int screenW = Screen.width;
            int screenH = Screen.height;

            int left = rect.x;
            int top = rect.y;
            int width = rect.w;
            int height = rect.h;

            // 合法性
            //
            left = Math.Clamp(left, 0, screenW - 1);
            top = Math.Clamp(top, 0, screenH - 1);
            width = Math.Clamp(width, 1, screenW - left);
            height = Math.Clamp(height, 1, screenH - top);

            Bitmap screenBmp = new Bitmap(width, height);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(screenBmp))
            {
                //第1、2个参数：屏幕起始坐标（如 0,0）
                //第3、4个参数：目标 Bitmap 的起始坐标（如 0,0）
                //第5个参数：复制的区域大小
                g.CopyFromScreen(left, top, 0, 0, screenBmp.Size);
            }

            return screenBmp;
        }


        #endregion

        #region  桌面绘图API
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
                    int dwExStyle,
                    string lpClassName,
                    string lpWindowName,
                    int dwStyle,
                    int x,
                    int y,
                    int nWidth,
                    int nHeight,
                    IntPtr hWndParent,
                    IntPtr hMenu,
                    IntPtr hInstance,
                    IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_LAYERED = 0x00080000;


        public const int SW_SHOW = 5; // 显示窗口
        public const int SW_MINIMIZE = 6; // 最小化窗口
        const int SW_RESTORE = 9;

        public static IntPtr CreateFullscreenOverlay()
        {
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_VISIBLE = unchecked((int)0x10000000);

            // 创建一个透明的顶层窗口
            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            IntPtr hWnd = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_LAYERED, // 扩展样式：顶层窗口 + 透明
                "STATIC",                     // 窗口类名
                "PopDrawWin",                         // 窗口标题
                WS_POPUP | WS_VISIBLE,                     // 普通样式：无边框弹出窗口
                0, 0,                         // 窗口位置
                screenWidth, screenHeight,  // 窗口大小（全屏）
                IntPtr.Zero,                  // 父窗口句柄
                IntPtr.Zero,                  // 菜单句柄
                IntPtr.Zero,                  // 实例句柄
                IntPtr.Zero                   // 参数
            );

            if (hWnd == IntPtr.Zero)
            {
                DU.LogError("创建窗口失败");
                return hWnd;
            }

            const uint LWA_COLORKEY = 0x00000001;  // 使用crKey为透明颜色，窗口默认白底都会变为透明.。bAlpha参数无效。
            const uint LWA_ALPHA = 0x00000002;     // crKey参数无效，bAlpha参数有效
            // 设置窗口为透明
            SetLayeredWindowAttributes(hWnd, 0x00FFFFFF, 0, LWA_COLORKEY);
            // 显示窗口
            ShowWindow(hWnd, SW_SHOW);
            // 更新窗口
            UpdateWindow(hWnd);



            return hWnd;
        }

        public const int WM_PAINT = 0x000F;
        // 定义窗口过程的委托
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public const int GWL_WNDPROC = -4;

        #endregion

        #region 打印测试
        public static string PrintHandle(IntPtr hWnd)
        {
            // 测试屏幕绘制
            IntPtr exStyle = WU.GetWindowLong(hWnd, WU.GWL_EXSTYLE);
            string p = $"窗口句柄: {hWnd}, 样式: {exStyle}, 标题: {GetWindowTitle(hWnd)}"
            + $", 区域: {GetWindowRect(hWnd)}, 可见: {IsWindowVisible(hWnd)}";

            DU.Log(p);
            return p;
        }
        #endregion

    }
    #region  枚举

    public enum MouseHookEnum
    {
        LeftDown = 0x0201, // 左键按下
        LeftUp = 0x0202,   // 左键抬起
        RightDown = 0x0204, // 右键按下
        RightUp = 0x0205,   // 右键抬起
        Move = 0x0200,   // 鼠标移动
    }
    public enum KeyboardHookEnum
    {
        KeyDown = 0x0100, // 按键按下
        KeyUp = 0x0101,   // 按键抬起
        SystemDown = 0x0104, // 系统按键（如 Alt）按下
        SystemUp = 0x0105,   // 系统按键释放
    }

    public enum KeyboardEnum
    {
        Esc = 0x1B,
        Tab = 0x09,
        CapsLock = 0x14,
        Shift = 0x10,
        Ctrl = 0x11,
        Alt = 0x12,
        Space = 0x20,
        Enter = 0x0D,
        Backspace = 0x08,
        Delete = 0x2E,
        Insert = 0x2D,
        Home = 0x24,
        End = 0x23,
        PageUp = 0x21,
        PageDown = 0x22,
        LeftWin = 0x5B,
        RightWin = 0x5C,
        Menu = 0x5D,
        // 方向键
        Up = 0x26,
        Down = 0x28,
        Left = 0x25,
        Right = 0x27,

        // 主键盘上方数字
        D0 = 0x30, // '0'
        D1 = 0x31, // '1'
        D2 = 0x32, // '2'
        D3 = 0x33, // '3'
        D4 = 0x34, // '4'
        D5 = 0x35, // '5'
        D6 = 0x36, // '6'
        D7 = 0x37, // '7'
        D8 = 0x38, // '8'
        D9 = 0x39, // '9'

        // 字母A-Z
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A
    }

    #endregion

}