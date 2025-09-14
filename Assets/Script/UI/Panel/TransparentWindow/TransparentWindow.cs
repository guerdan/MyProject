using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(Camera))]
public class TransparentWindow : MonoBehaviour
{
	public static TransparentWindow Main = null;
	public static Camera Camera = null; //Used instead of Camera.main

	[Tooltip("What GameObject layers should trigger window focus when the mouse passes over objects?")] //
	[SerializeField] LayerMask clickLayerMask = ~0;

	[Tooltip("Allows Input to be detected even when focus is lost")] //
	[SerializeField] bool useSystemInput = false;

	[Tooltip("Should the window be fullscreen?")] //
	[SerializeField] bool fullscreen = true;

	[Tooltip("Force the window to match ScreenResolution")] //
	[SerializeField] bool customResolution = true;

	[Tooltip("Resolution the overlay should run at")] //
	[SerializeField] Vector2Int screenResolution = new Vector2Int(1280, 720);

	[Tooltip("The framerate the overlay should try to run at")] //
	[SerializeField] int targetFrameRate = 30;


	/////////////////////
	//Windows DLL stuff//
	/////////////////////

	[DllImport("user32.dll")]
	static extern IntPtr GetActiveWindow();

	[DllImport("user32.dll")]
	static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
	static extern int SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, int dwFlags);

	[DllImport("user32.dll", EntryPoint = "GetWindowRect")]
	static extern bool GetWindowRect(IntPtr hwnd, out Rectangle rect);

	[DllImport("user32.dll")]
	static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
	[DllImport("user32.dll", EntryPoint = "PostMessage")]
	public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImportAttribute("user32.dll")]
	static extern bool ReleaseCapture();

	[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
	static extern int SetWindowPos(IntPtr hwnd, int hwndInsertAfter, int x, int y, int cx, int cy, int uFlags);

	[DllImport("Dwmapi.dll")]
	static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Rectangle margins);

	const int GWL_STYLE = -16;
	const uint WS_POPUP = 0x80000000;
	const uint WS_VISIBLE = 0x10000000;
	const int HWND_TOPMOST = -1;

	const int WM_SYSCOMMAND = 0x112;
	const int WM_MOUSE_MOVE = 0xF012;

	int fWidth;
	int fHeight;
	IntPtr hwnd = IntPtr.Zero;   //程序自身窗口句柄
	Rectangle margins;
	Rectangle windowRect;

	//BUG: Sometimes fails to SetResolution if not focused on startup - if using Start(), WindowBoundsCollider2D sometimes fails to set the correct size
	void Awake()
	{
		Main = this;

		Camera = GetComponent<Camera>();
		Camera.backgroundColor = new Color();
		Camera.clearFlags = CameraClearFlags.SolidColor;

		if (fullscreen && !customResolution)
		{
			screenResolution = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
		}

		Screen.SetResolution(screenResolution.x, screenResolution.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

		Application.targetFrameRate = targetFrameRate;
		Application.runInBackground = true;

#if !UNITY_EDITOR
		fWidth = screenResolution.x;
		fHeight = screenResolution.y;
		margins = new Rectangle() {Left = -1};
		hwnd = GetActiveWindow();

		if (GetWindowRect(hwnd, out windowRect))
		{
			Debug.LogError("Couldn't get Window Rect");
		}
        //WS_POPUP决定了窗口的无边框
		SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
		// SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, 32 | 64);
		SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, fWidth, fHeight, 32 | 64);
		DwmExtendFrameIntoClientArea(hwnd, ref margins);
#endif
	}

	void Update()
	{
		if (useSystemInput)
		{
			// 记录点击状态
			SystemInput.Process();
		}

		//若不点在UI上则让其点穿，通过切换窗口扩展样式。
		SetClickThrough();
	}

	//Returns true if the cursor is over a UI element or 2D physics object
	bool FocusForInput()
	{
		EventSystem eventSystem = EventSystem.current;
		if (eventSystem && eventSystem.IsPointerOverGameObject())
		{
			return true;
		}

		Vector2 pos = Camera.ScreenToWorldPoint(Input.mousePosition);
		return Physics2D.OverlapPoint(pos, clickLayerMask);
	}

	void SetClickThrough()
	{
		//是否聚焦在UI上
		var focusWindow = FocusForInput();

		//Get window position
		GetWindowRect(hwnd, out windowRect);

		//设置扩展样式。
		const int GWL_EXSTYLE = -20;
		//支持分层。作用是让窗口透明
		const uint WS_EX_LAYERED = (uint)524288;
		//忽略鼠标事件。作用是使鼠标点击可以穿透窗口
		const uint WS_EX_TRANSPARENT = (uint)32;
		const int LWA_COLORKEY = 0x00000001;  // 使用crKey为透明颜色，窗口默认白底都会变为透明.。bAlpha参数无效。
		const int LWA_ALPHA = 0x00000002;     // crKey参数无效，bAlpha参数有效

#if !UNITY_EDITOR
		if (focusWindow)
		{
			SetWindowLong (hwnd, GWL_EXSTYLE, ~(WS_EX_LAYERED | WS_EX_TRANSPARENT)); //扩展样式
			SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, 32 | 64);//大小
		}
		else
		{
			SetWindowLong (hwnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT); 
			SetLayeredWindowAttributes (hwnd, 0, 255, LWA_ALPHA); 
			SetWindowPos(hwnd, HWND_TOPMOST, windowRect.Left, windowRect.Top, fWidth, fHeight, 32 | 64);
		}
#endif
	}

	public void MinimizeWindow()
	{
		DU.Log("TransparentWindow MinimizeWindow");
		WU.ShowWindow(hwnd, WU.SW_MINIMIZE);
	}
	public void ShowWindow()
	{
		WU.ShowWindow(hwnd, WU.SW_SHOW);
	}

	public static void DragWindow()
	{
#if !UNITY_EDITOR
		if (Screen.fullScreenMode != FullScreenMode.Windowed)
		{
			return;
		}
		ReleaseCapture ();
		SendMessage(Main.hwnd, WM_SYSCOMMAND, WM_MOUSE_MOVE, 0);
		Input.ResetInputAxes();
#endif
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}