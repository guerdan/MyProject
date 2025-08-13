using System;
using System.Collections.Generic;
using Script.Framework.AssetLoader;
using Script.UI.Component;
using Script.Util;
using UnityEngine;

namespace Script.Framework.UI
{
    // 场景代言人，由UIManager控制
    public class UISceneMixin : MonoBehaviour
    {
        public static UISceneMixin Inst;

        [HideInInspector] public Transform PanelCacheParent;
        [HideInInspector] public PanelStack GuideStack;
        [HideInInspector] public LoadingProgressUI LoadingProgressUI;
        private List<PanelStack> _panelStacks;

        private const string LoadingProgressUIPath = "Common/Prefabs/Component/LoadingProgressUI";
        //动画过程中，屏蔽点击事件
        // public GameObject _topMaskNode;

        void Awake()
        {
            Inst = this;
            AdjustRect(gameObject);

            // 界面缓存父节点
            var panelCacheNode = new GameObject("PanelCache");
            AdjustRect(panelCacheNode);
            PanelCacheParent = panelCacheNode.transform;
            PanelCacheParent.SetParent(transform, false);

            // 界面栈
            _panelStacks = new List<PanelStack>();
            for (int i = 0; i < 3; i++)
            {
                _panelStacks.Add(CreateStack($"PanelStack{i}"));
            }
            // 引导栈
            GuideStack = CreateStack($"GuideStack");

            //加载LoadingProgressUI
            var loadingProgressUIParent = new GameObject("LoadingProgressUIParent");
            AdjustRect(loadingProgressUIParent);
            loadingProgressUIParent.transform.SetParent(transform, false);
            AssetUtil.LoadPrefab(LoadingProgressUIPath, (prefab) =>
            {
                var go = Instantiate(prefab, loadingProgressUIParent.transform, false);
                LoadingProgressUI = go.GetComponent<LoadingProgressUI>();
                return go;
            });

        }

        PanelStack CreateStack(string name)
        {
            var node = new GameObject(name);
            node.AddComponent<RectTransform>();
            var stack = node.AddComponent<PanelStack>();
            node.transform.SetParent(transform, false);
            AdjustRect(node);

            return stack;
        }


        void AdjustRect(GameObject node)
        {
            var rect = node.GetComponent<RectTransform>();
            if (rect == null) rect = node.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 0);
        }

        // 加入界面
        public void PushPanel(IPanel panel, Action cb)
        {
            PanelDefine define = panel.PanelDefine;
            var stack = _panelStacks[define.Layer];
            stack.Push(panel, cb);
        }
        // 将底部界面置到顶部
        public void ToFirst(IPanel panel, Action cb)
        {
            PanelDefine define = panel.PanelDefine;
            var stack = _panelStacks[define.Layer];
            stack.ToFirst(panel, cb);
        }
        public IPanel PopPanel(int layer, Action cb)
        {
            var stack = _panelStacks[layer];
            return stack.Pop(cb);
        }

        public void PopPanel(IPanel panel, Action cb)
        {
            var stack = _panelStacks[panel.PanelDefine.Layer];
            stack.Pop(panel, cb);
        }


        public IPanel FindPanel(PanelEnum panelEnum)
        {
            PanelDefine define = PanelUtil.PanelDefineDic[panelEnum];
            var stack = _panelStacks[define.Layer];
            return stack.FindView(panelEnum);
        }
        public IPanel FindPanel(int layer, int topIndex)
        {
            var stack = _panelStacks[layer];
            return stack.GetTopIndexPanel(topIndex);
        }

        public IPanel PeekPanel(int layer)
        {
            var stack = _panelStacks[layer];
            return stack.Peek();
        }

        public int GetPanelCount(int layer)
        {
            var stack = _panelStacks[layer];
            return stack.GetCount();
        }


        public void ShowLoadingAnim()
        {
            if (LoadingProgressUI == null) return;
            LoadingProgressUI.Show();
        }
        public void HideLoadingAnim()
        {
            if (LoadingProgressUI == null) return;
            LoadingProgressUI.Hide();
        }
    }
}