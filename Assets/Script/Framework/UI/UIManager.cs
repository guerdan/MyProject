using System;
using System.Collections.Generic;
using System.Linq;
using Script.Framework.AssetLoader;
using Script.Util;
using UnityEngine;

namespace Script.Framework.UI
{
    public interface IUIManager
    {
        // 打开一个界面
        void ShowPanel(PanelEnum panelEnum, object data);
        // 关掉层级中最上一个界面
        void PopPanel(int layer = 0);
        // 关闭指定界面
        void PopPanel(PanelEnum panelEnum);
        void Recycle(BasePanel panel);

    }
    public class BasePanelWait
    {
        public BasePanel Panel;
        public object Data;
        public PanelDefine PanelDefine;
        public BasePanelWait(BasePanel panel, object data, PanelDefine panelDefine)
        {
            Panel = panel;
            Data = data;
            PanelDefine = panelDefine;
        }
    }

    public class UIManager : IUIManager
    {
        private static IUIManager _inst;
        public static IUIManager Inst
        {
            get
            {
                if (_inst == null) _inst = new UIManager();
                return _inst;
            }
        }

        private Dictionary<int, List<BasePanelWait>> _panelWaits;              //打开界面队列

        private Dictionary<PanelEnum, BasePanel> _panelCache;    //PanelEnum 最多缓存一份界面实例
        private Dictionary<PanelEnum, float> _panelRecycleTime;  //缓存回收时间

        public UIManager()
        {
            _panelCache = new Dictionary<PanelEnum, BasePanel>();
            _panelRecycleTime = new Dictionary<PanelEnum, float>();
            _panelWaits = new Dictionary<int, List<BasePanelWait>>();
        }

        // 打开一个界面
        public void ShowPanel(PanelEnum panelKey, object data)
        {
            PanelDefine define = PanelUtil.PanelDefineDic[panelKey];
            int layer = define.Layer;
            if (define == null)
            {
                Debug.LogError($"{panelKey}未配置PanelDefine");
                return;
            }


            var time = DateTime.Now;
            if (_panelCache.ContainsKey(panelKey))
            {
                BasePanel panel = _panelCache[panelKey];
                DeleteCache(panelKey);
                AddPanelWait(panel, data, layer, define);
                ShowPanelInternal(layer);
                Debug.Log($"打开界面 {define.Name} 缓存 耗时：{(DateTime.Now - time).TotalMilliseconds}ms");
            }
            else
            {
                UISceneMixin.Inst.ShowLoadingAnim();
                var waitResult = AddPanelWait(null, data, layer, define);

                AssetUtil.LoadPrefab(define.Path, (prefab) =>
                {
                    GameObject go = GameObject.Instantiate(prefab);
                    var panel = go.GetComponent<BasePanel>();
                    if (panel == null)
                    {
                        Debug.LogError($"{define.Path} 界面上不存在BasePanel");
                    }
                    waitResult.Panel = panel;
                    ShowPanelInternal(layer);

                    UISceneMixin.Inst.HideLoadingAnim();
                    // GameTimer.Inst.SetTimeOnce(this, () => UISceneMixin.Inst.HideLoadingAnim(), 1f);

                    Debug.Log($"打开界面 {define.Name} 新节点 耗时：{(DateTime.Now - time).TotalMilliseconds}ms");
                    return go;
                });

            }
        }
        private BasePanelWait AddPanelWait(BasePanel panel, object data, int layer, PanelDefine define)
        {
            var wait = new BasePanelWait(panel, data, define);
            if (!_panelWaits.TryGetValue(layer, out var list))
            {
                list = new List<BasePanelWait>();
                _panelWaits[layer] = list;
            }
            list.Add(wait);
            return wait;
        }

        //将等待队列一个个弹出再清空。
        private void ShowPanelInternal(int layer)
        {
            var list = _panelWaits[layer];
            if (list == null || list.Count == 0) return;
            foreach (var wait in list)
            {
                if (wait.Panel == null) break;
                wait.Panel.PanelDefine = wait.PanelDefine;
                wait.Panel.gameObject.SetActive(true);
                wait.Panel.SetData(wait.Data);
                UISceneMixin.Inst.PushPanel(wait.Panel);
            }
            list.Clear();
        }
        // 关掉层级中最上的一个界面
        public void PopPanel(int layer = 0)
        {
            UISceneMixin.Inst.PopPanel(layer);
        }
        // 关闭指定界面
        public void PopPanel(PanelEnum panelEnum)
        {
            var panel = UISceneMixin.Inst.FindPanel(panelEnum);
            UISceneMixin.Inst.PopPanel(panel);
        }


        public void Recycle(BasePanel panel)
        {
            PanelDefine define = panel.PanelDefine;

            switch (define.RecycleType)
            {
                case UIRecycleTypeEnum.Once:
                    Destroy(panel);
                    break;
                case UIRecycleTypeEnum.Normal:
                case UIRecycleTypeEnum.Frequent:
                    if (_panelCache.ContainsKey(define.Key))
                    {
                        Destroy(panel);
                        break;
                    }
                    //加入缓存
                    panel.gameObject.SetActive(false);
                    panel.transform.SetParent(UISceneMixin.Inst.PanelCacheParent, false);
                    int recycleTime = define.RecycleType == UIRecycleTypeEnum.Normal ? PanelUtil.RecycleTimeNormal : PanelUtil.RecycleTimeFrequent;
                    _panelCache[define.Key] = panel;
                    _panelRecycleTime[define.Key] = recycleTime;

                    break;
            }



            //当关闭Layer=0的界面栈的最后个界面时，调用AssetManager.ReleaseUnuseAsset真正回收资源
            //因为连续打开的界面通常是一个系统功能模块，在同一个Bundle中。所以最后个界面关闭才有可能释放bundle。
            if (define.Layer == 0 && UISceneMixin.Inst.PeekPanel(0) == null)
            {
                AssetManager.Inst.ReleaseUnuseAsset();
            }
        }

        public void OnUpdate(float delta)
        {
            List<PanelEnum> keys = new List<PanelEnum>(_panelRecycleTime.Keys);
            foreach (var key in keys)
            {
                _panelRecycleTime[key] -= delta;
                if (_panelRecycleTime[key] <= 0)
                {
                    Destroy(_panelCache[key]);
                    DeleteCache(key);
                }
            }
        }

        private void Destroy(BasePanel panel)
        {
            UnityEngine.Object.Destroy(panel.gameObject);
        }
        private void DeleteCache(PanelEnum key)
        {
            _panelCache.Remove(key);
            _panelRecycleTime.Remove(key);
        }
    }
}