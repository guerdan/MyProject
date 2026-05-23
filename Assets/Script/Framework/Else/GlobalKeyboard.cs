
using System;
using System.Collections.Generic;
using System.Linq;
using Script.Util;

namespace Script.Framework.Else
{
    public class GlobalKeyboardItem
    {
        public KeyboardEnum Vk;
        public bool IsDown;
        public bool LastFrameIsDown;

        public Action DownAction;       // 按压那一刻调用
        public Action UpAction;         // 松开那一刻调用


        public GlobalKeyboardItem(KeyboardEnum vk)
        {
            Vk = vk;
            IsDown = false;
            LastFrameIsDown = false;
            DownAction = null;
            UpAction = null;
        }
    }

    public class GlobalKeyboardManager
    {
        private static GlobalKeyboardManager _inst;
        public static GlobalKeyboardManager Inst
        { get { if (_inst == null) _inst = new GlobalKeyboardManager(); return _inst; } }


        Dictionary<KeyboardEnum, GlobalKeyboardItem> _listener = new Dictionary<KeyboardEnum, GlobalKeyboardItem>();

        public void AddListener(KeyboardEnum vk, bool isDown, Action action)
        {
            if (!_listener.TryGetValue(vk, out var item))
            {
                item = new GlobalKeyboardItem(vk);
                _listener.Add(vk, item);
            }

            if (isDown)
                item.DownAction += action;
            else
                item.UpAction += action;
        }

        public void RemoveListener(KeyboardEnum vk, bool isDown, Action action)
        {
            if (_listener.TryGetValue(vk, out var item))
            {
                if (isDown)
                    item.DownAction -= action;
                else
                    item.UpAction -= action;
            }
        }

        public bool GetStatus(KeyboardEnum vk)
        {
            if (!_listener.TryGetValue(vk, out var item))
            {
                item = new GlobalKeyboardItem(vk);
                _listener.Add(vk, item);
                item.IsDown = (WU.GetAsyncKeyState((int)vk) & 0x8000) != 0;
            }

            return item.IsDown;
        }

        public void OnUpdate()
        {
            var list = _listener.Values.ToArray();
            // 先更新IsDown状态，因为有些需求是按住Ctrl键不放再按下其他键触发的。
            foreach (var item in list)
            {
                item.LastFrameIsDown = item.IsDown;
                item.IsDown = (WU.GetAsyncKeyState((int)item.Vk) & 0x8000) != 0;
            }

            foreach (var item in list)
            {
                if (item.IsDown && !item.LastFrameIsDown)
                {
                    item.DownAction?.Invoke();
                }
                else if (!item.IsDown && item.LastFrameIsDown)
                {
                    item.UpAction?.Invoke();
                }
            }
        }
    }
}