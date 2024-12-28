using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.Framework.UI
{
    public class PanelStack : MonoBehaviour
    {
        private Stack<IPanel> _views = new Stack<IPanel>();

        public void Push(IPanel view, Action cb)
        {
            if (_views.Count > 0)
            {
                var top = _views.Peek();
                if (top == view)
                {
                    cb?.Invoke();
                    return;
                }
            }

            ShowNext(view, cb);
        }

        /// <summary>
        /// 可以实现，先存入，关闭动画播完后再打开。
        /// </summary>
        public void ShowNext(IPanel view, Action cb)
        {
            _views.Push(view);
            view.Transform.SetParent(this.transform, false);
            view.BeforeShow();
            view.OnShow(() =>
            {
                view.AfterShow();
                cb?.Invoke();
            });
        }

        public IPanel Pop(Action cb)
        {
            if (_views.Count == 0)
            {
                cb?.Invoke();
                return null;
            }

            var view = _views.Pop();
            view.BeforeHide();
            view.OnHide(() =>
            {
                view.AfterHide();
                view.Recycle();
                cb?.Invoke();
            });

            return view;
        }
        public void Pop(IPanel view, Action cb)
        {
            if (!_views.Contains(view))
            {
                cb?.Invoke();
                return;
            }

            List<IPanel> tempList = new List<IPanel>(_views);
            tempList.Remove(view);
            _views.Clear();
            for (int i = tempList.Count - 1; i >= 0; i--)
            {
                _views.Push(tempList[i]);
            }

            view.BeforeHide();
            view.OnHide(() =>
            {
                view.AfterHide();
                view.Recycle();
                cb?.Invoke();
            });        
        }

        public IPanel FindView(PanelEnum key)
        {
            foreach(var view in _views)
            {
                if (view.PanelDefine.Key == key)
                {
                    return view;
                }
            }

            return null;
        }

        public IPanel Peek()
        {
            return _views.Count > 0 ? _views.Peek() : null;
        }
    }
}