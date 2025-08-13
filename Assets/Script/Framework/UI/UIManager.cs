using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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
        //清理掉所有界面缓存，并调用AssetManager标记清除接口
        void ClearCache();

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

        private Dictionary<int, List<BasePanelWait>> _panelStackWaits;      //打开界面队列

        private Dictionary<PanelEnum, BasePanel> _panelCache;               //PanelEnum 最多缓存一份界面实例
        private Dictionary<PanelEnum, float> _panelRecycleTime;             //缓存回收时间
        private bool _canRecycle = false;             //触发资源管理器回收标志

        public UIManager()
        {
            _panelCache = new Dictionary<PanelEnum, BasePanel>();
            _panelRecycleTime = new Dictionary<PanelEnum, float>();
            _panelStackWaits = new Dictionary<int, List<BasePanelWait>>();
        }

        // 打开一个界面
        public void ShowPanel(PanelEnum panelKey, object data)
        {
            PanelDefine define = PanelUtil.PanelDefineDic[panelKey];
            if (define == null)
            {
                Debug.LogError($"{panelKey}未配置PanelDefine");
                return;
            }

            int layer = define.Layer;
            var type = define.Type;
            var time = DateTime.Now;

            // 如果是WindowsPopUp类型的界面，规则就不一样了。
            if (type == UITypeEnum.WindowsPopUp)
            {
                var findPanel = UISceneMixin.Inst.FindPanel(panelKey) as BasePanel;
                if (findPanel != null)
                {
                    findPanel.SetData(data);
                    // 对已经打开过的界面，把界面置到顶部
                    UISceneMixin.Inst.ToFirst(findPanel, null);
                    return;
                }
            }


            if (_panelCache.ContainsKey(panelKey))  //缓存
            {
                BasePanel panel = _panelCache[panelKey];
                DeleteCache(panelKey);
                AddPanelStackWaits(panel, data, layer, define);
                ShowPanelInternal(layer);
                Debug.Log($"打开界面 {define.Name} 缓存 耗时：{(DateTime.Now - time).TotalMilliseconds}ms");
            }
            else
            {
                UISceneMixin.Inst.ShowLoadingAnim();
                var waitResult = AddPanelStackWaits(null, data, layer, define);

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
                    // GameTimer.Inst.SetTimeOnce(this, () => UISceneMixin.Inst.HideLoadingAnim(), 1f);//延迟测试

                    Debug.Log($"打开界面 {define.Name} 新节点 耗时：{(DateTime.Now - time).TotalMilliseconds}ms");
                    return go;
                });

            }
        }
        private BasePanelWait AddPanelStackWaits(BasePanel panel, object data, int layer, PanelDefine define)
        {
            var wait = new BasePanelWait(panel, data, define);
            if (!_panelStackWaits.TryGetValue(layer, out var list))
            {
                list = new List<BasePanelWait>();
                _panelStackWaits[layer] = list;
            }

            // 加载中又重复点了，不弹两个，只覆盖数据
            var find = list.Find(x => x.PanelDefine.Key == define.Key);
            if (find != null)
                find.Data = data;
            else
                list.Add(wait);

            return wait;
        }

        //将等待队列一个个弹出再清空。
        private void ShowPanelInternal(int layer)
        {
            var list = _panelStackWaits[layer];
            if (list == null || list.Count == 0) return;
            foreach (var wait in list)
            {
                if (wait.Panel == null) break;
                wait.Panel.PanelDefine = wait.PanelDefine;
                wait.Panel.StackIndex = UISceneMixin.Inst.GetPanelCount(layer);
                wait.Panel.gameObject.SetActive(true);
                wait.Panel.SetData(wait.Data);

                var last = UISceneMixin.Inst.PeekPanel(layer);
                if (NeedHideLast(wait.PanelDefine, last))
                {
                    last.OnHideContent();
                }

                UISceneMixin.Inst.PushPanel(wait.Panel, null);
            }
            list.Clear();
        }


        // 需求效果： 因遮挡关系而隐藏，多界面情况下简洁并省性能。
        // 当最上方为Full界面时，其他界面全隐藏。
        // 当最上方为PopUp界面，栈中的界面除了最上方的Full界面其他都隐藏。
        public bool NeedHideLast(PanelDefine define, IPanel last)
        {
            int layer = define.Layer;
            if (layer == 0 && define.HideLastPanel && last != null)
            {
                if (define.Type == UITypeEnum.PopUp && last.PanelDefine.Type == UITypeEnum.PopUp)
                    return true;
                if (define.Type == UITypeEnum.Full)
                    return true;
            }
            return false;
        }

        // 关掉层级中最上的一个界面
        public void PopPanel(int layer = 0)
        {
            UISceneMixin.Inst.PopPanel(layer, null);
            PopPanelInternal(layer);
        }
        // 关闭指定界面
        public void PopPanel(PanelEnum panelEnum)
        {
            var panel = UISceneMixin.Inst.FindPanel(panelEnum);
            UISceneMixin.Inst.PopPanel(panel, null);
            PopPanelInternal(panel.PanelDefine.Layer);
        }

        private void PopPanelInternal(int layer = 0)
        {
            var last = UISceneMixin.Inst.PeekPanel(layer);
            if (layer == 0 && last != null)
                last.OnShowContent();
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



            //当Layer=0的界面栈的最后个界面被销毁时，_canRecycle置true。进而调用AssetManager标记清除。
            //因为连续打开的界面通常是一个系统功能模块，在一个Bundle中。所以只有在最后个界面被销毁后才有可能释放bundle。
            //是最合适的时间来进行标记清除。
            if (_canRecycle)
            {
                _canRecycle = false;
                AssetManager.Inst.ReleaseUnuseAsset();
            }
        }

        public void OnUpdate(float deltaTime)
        {
            List<PanelEnum> keys = new List<PanelEnum>(_panelRecycleTime.Keys);
            foreach (var key in keys)
            {
                _panelRecycleTime[key] -= deltaTime;
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
            if (panel.StackIndex == 0 && panel.PanelDefine.Layer == 0)
            {
                _canRecycle = true;
            }
        }
        private void DeleteCache(PanelEnum key)
        {
            _panelCache.Remove(key);
            _panelRecycleTime.Remove(key);
        }

        public void ClearCache()
        {
            List<PanelEnum> keys = new List<PanelEnum>(_panelRecycleTime.Keys);
            foreach (var key in keys)
            {
                Destroy(_panelCache[key]);
                DeleteCache(key);
            }

            _canRecycle = false;
            GameTimer.Inst.SetTimeOnce(this, () => AssetManager.Inst.ReleaseUnuseAsset(), 0);//Destroy在下帧才真正销毁,故延迟1帧
        }
    }
}