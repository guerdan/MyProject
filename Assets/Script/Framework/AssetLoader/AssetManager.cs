using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using static UnityEngine.AddressableAssets.Addressables;
using Object = UnityEngine.Object;


namespace Script.Framework.AssetLoader
{
    public enum UnloadMode
    {
        /// <summary>
        /// 手动管理资源释放
        /// </summary>
        NotAuto,
        /// <summary>
        /// 如果委托者被销毁，在回收周期中卸载
        /// </summary>
        WithDelegator,
        /// <summary>
        /// 当资源加载完成后卸载
        /// </summary>
        WhenComplete
    }

    // todo 缺点：Addressable得加载时长不是最优，每个资源都至少要1帧从协程中返回。

    /// <summary>
    /// 1 所有UnityEngine Object对象分为预制体和克隆体。
    /// 2 Destroy接口能销毁一切克隆体，但不能销毁预制体。如果是GameObject自动删除其下挂接的子节点和组件，但不会销毁引用资源（如材质、纹理等）
    /// 容易造成泄漏的点：
    /// 1.预制体的引用计数未正常删减。
    /// 2.游离的引用资源的克隆体。例如：SpriteAtlas取Sprite就是个克隆体
    /// ，它既不会被Addressables自动销毁，也不会被Destroy(GameObject)而销毁。
    /// 最保守但低效的做法是周期性地调用 Resources.UnloadUnusedAssets();
    /// 类似的克隆体还有：手动修改实例Material（非shareMaterial），手动修改实例Mesh。
    /// 
    /// 方案：只有完全熟悉了Addressables接口和Unity的资源与克隆体理念，才能做套适配方案。
    /// 克隆体走额外接口，接口（先复制出克隆体，绑定克隆体与预制体）
    /// 代理回收，就是延续引用计数方案，还适合跟踪。
    /// </summary>
    public interface IAssetManager
    {
        #region 接口

        /// <summary>
        /// 异步加载资源，代理接口。已加载过的资源，可以同步调用。
        /// 1.加载期间代理对象若被销毁，则不执行回调。2.代理对象被销毁，将释放对此资源的引用计数
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="address">可以是资源名也可以是lable</param>
        /// <param name="loadComplete"></param>
        /// <param name="delegtor"> 代理对象最好就是持有者本身，也可以是更高层的管理者 </param>
        /// <param name="mode"> WithDelegator模式下delegtor不为null。其他情况delegtor为null </param>
        void LoadAssetAsync<T>(string address, Action<T> loadComplete
       , Object delegtor, UnloadMode mode = UnloadMode.WithDelegator) where T : Object;
        void LoadAssetAsync<T>(string address, Action<T, AsyncOperationHandle> loadComplete
        , Object delegtor, UnloadMode mode = UnloadMode.WithDelegator) where T : Object;


        #endregion

        #region 批量加载
        /// <summary>
        /// 批量加载全部类型的资源,addresses的并集,手动管理资源。每个资源至少一帧
        /// </summary>
        AsyncOperationHandle LoadAssetsAsync(List<string> addresses, Action<Object> loadComplete);
        /// <summary>
        /// 会筛选指定泛型资源，其他类型资源不会加载。
        /// </summary>
        AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(List<string> addresses, Action<T> loadComplete) where T : Object;
        #endregion

        #region 自定义绑定方式(绑定实例)
        /// <summary>
        /// 绑定代理与handle
        /// </summary>
        void BindDelegator(string address, Object delagtor, AsyncOperationHandle handle);

        #endregion
        #region 释放资源
        /// <summary>
        /// 清理资源缓存。
        /// 用过或熟悉 Addressable 的同学应该都知道，Addressable使用引用计数来维护资源的使用，
        /// 当某个资源引用计数为0时，便会将其卸载，待整个bundle引用计数为0时，再将整个bundle卸载（此时释放资源内存）。
        /// </summary>
        void ReleaseUnuseAsset();
        // 自定义释放，清理delagtor的所有依赖handle。
        void ReleaseAsset(Object delegtor);

        void ReleaseAsset(AsyncOperationHandle handle);

        #endregion

    }

    public class AssetManager : IAssetManager
    {


        private static IAssetManager _inst;
        public static IAssetManager Inst
        {
            get
            {
                if (_inst == null) _inst = new AssetManager();
                return _inst;


            }
        }


        // <实例, <资源地址, handle>字典> 代理对象销毁时释放相关handle。
        // handle代表资源唯一性。同个资源Addressables会返回相同的handle。<代理对象, handle>代表一次引用计数。同个代理与同个handle，在_delegatorToHandles最多占一次。
        private Dictionary<Object, Dictionary<string, AsyncOperationHandle>> _delegatorToHandles;

        // <图集资源, <精灵名, Sprite(Clone)>字典> 用于缓存图集中的精灵，当资源被销毁时就销毁它的克隆体。
        private Dictionary<Object, Dictionary<string, Sprite>> _atlasToSprites;

        // <精灵路径, 图集地址> 用于优化动态加载图集图片的流程，自动转换加载方式。
        private Dictionary<string, string[]> _sprite2AtlasAddress;
        private const string Sprite2AtlasAddressPath = "Other/Sprite2AtlasAddress";

        public AssetManager()
        {
            _delegatorToHandles = new Dictionary<Object, Dictionary<string, AsyncOperationHandle>>();
            _atlasToSprites = new Dictionary<Object, Dictionary<string, Sprite>>();
        }



        /// <summary>
        /// 代理接口。通过判断代理对象是否destroy，来决定释放加载过的资源
        /// 原理：Addressables是通过引用计数来管理资源的。引用计数为0时，资源会被释放。
        /// 一个AsyncOperationHandle对象代表 delagtor对"待加载资源"的一次引用计数。
        /// Addressables会自动统计"待加载资源"对其依赖资源的引用计数，而无法统计外界对"待加载资源"的依赖。
        /// 于是框架做的事情就等价于 image.sprite = sprite;时，sprite的引用计数+1。
        /// </summary>
        public void LoadAssetAsync<T>(string address, Action<T> loadComplete
               , Object delagtor, UnloadMode mode = UnloadMode.WithDelegator) where T : Object
        {
            LoadAssetAsync<T>(address, (asset, _) => loadComplete.Invoke(asset), delagtor, mode);
        }
        public void LoadAssetAsync<T>(string address, Action<T, AsyncOperationHandle> loadComplete
        , Object delagtor, UnloadMode mode = UnloadMode.WithDelegator) where T : Object
        {
            if (mode == UnloadMode.WithDelegator && delagtor == null)
            {
                Debug.LogWarning("[AssetManager] LoadAsync delagtor is null, 接口调用错误");
                return;
            }

            Action<string, T, AsyncOperationHandle> loadCompleteAction =
            (address, asset, handle) =>
            {
                if (mode == UnloadMode.NotAuto)
                {
                    loadComplete.Invoke(asset, handle);
                }
                else if (mode == UnloadMode.WithDelegator)
                {
                    // 说明代理对象已经被销毁，不用向后执行并释放资源。设计上，保证回调安全性。
                    if (delagtor == null)
                    {
                        ReleaseAsset(handle);
                        return;
                    }

                    loadComplete.Invoke(asset, handle);
                    BindDelegator(address, delagtor, handle);
                }
                else if (mode == UnloadMode.WhenComplete)
                {
                    loadComplete.Invoke(asset, handle);
                    ReleaseAsset(handle);
                }
            };

            LoadAssetInType<T>(address, loadCompleteAction);
        }



        public void BindDelegator(string address, Object delegator, AsyncOperationHandle handle)
        {
            if (!_delegatorToHandles.TryGetValue(delegator, out var handleDic))
            {
                handleDic = new Dictionary<string, AsyncOperationHandle>();
                _delegatorToHandles.Add(delegator, handleDic);
            }

            if (!handleDic.TryGetValue(address, out var _))
            {
                handleDic.Add(address, handle);
            }
            else
            {
                //同个代理与同个handle，只占一次计数。于是就释放。
                ReleaseAsset(handle);
            }
        }



        private void LoadAssetInType<T>(string address, Action<string, T, AsyncOperationHandle> loadCompleteAction) where T : Object
        {
            if (typeof(T) == typeof(Sprite))
            {
                if (_sprite2AtlasAddress == null)
                {
                    LoadAssetInternal<TextAsset>(Sprite2AtlasAddressPath,
                    (_, textAsset, handle) =>
                    {
                        _sprite2AtlasAddress = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(textAsset.text);
                        ReleaseAsset(handle);
                        LoadSprite(address, loadCompleteAction);
                    });
                }
                else
                {
                    LoadSprite(address, loadCompleteAction);
                }
            }
            else
            {
                LoadAssetInternal<T>(address, loadCompleteAction);
            }

        }

        public void LoadSprite<T>(string address, Action<string, T, AsyncOperationHandle> loadCompleteAction) where T : Object
        {
            string[] spriteInfo = _sprite2AtlasAddress[address];
            string atlasAddress = spriteInfo[0];
            string spriteName = spriteInfo[1];

            LoadAssetInternal<SpriteAtlas>(atlasAddress, (_, atlas, handle) =>
            {
                if (!_atlasToSprites.TryGetValue(atlas, out var sprites))
                {
                    sprites = new Dictionary<string, Sprite>();
                    _atlasToSprites.Add(atlas, sprites);
                }

                if (!sprites.TryGetValue(spriteName, out var sprite))
                {
                    sprite = atlas.GetSprite(spriteName);
                    sprites.Add(spriteName, sprite);
                }

                loadCompleteAction.Invoke(address, sprite as T, handle);
            });
        }

        /// <summary>
        /// 有缓存则同步执行回调。无缓存则异步加载
        /// </summary>
        private void LoadAssetInternal<T>(string address, Action<string, T, AsyncOperationHandle> loadCompleteAction) where T : Object
        {
            AsyncOperationHandle<T> operateHandle = Addressables.LoadAssetAsync<T>(address);
            bool hasCache = operateHandle.Status == AsyncOperationStatus.Succeeded;
            if (hasCache)
            {
                loadCompleteAction.Invoke(address, operateHandle.Result, operateHandle);
            }
            else
            {
                operateHandle.Completed +=
                //两个handle是同一个
                (handle) =>
                {
                    if (operateHandle.Status != AsyncOperationStatus.Succeeded || operateHandle.Result == null)
                    {
                        Debug.LogError($"Load Asset Async Failed: {address}.");
                        return;
                    }
                    loadCompleteAction.Invoke(address, operateHandle.Result, operateHandle);
                };
            }



        }

        public void ReleaseUnuseAsset()
        {
            var keys = _delegatorToHandles.Keys.ToArray();

            //当UnityEngine.Object被销毁时， 对象==null为true，原理是引擎重写了其==运算符
            foreach (var delegator in keys)
            {
                if (delegator == null)
                    ReleaseAsset(delegator);
            }

            // 清理图集精灵缓存
            foreach (var atlas in _atlasToSprites.Keys.ToArray())
            {
                if (atlas == null)
                {
                    foreach (var sprite in _atlasToSprites[atlas].Values)
                    {
                        Object.Destroy(sprite);
                    }
                    _atlasToSprites.Remove(atlas);
                }
            }


            // GC.Collect();  // GC看有没有必要

            // 能够销毁野对象，原理是暴力遍历指针的全局表，如同标记清除-垃圾回收机制。
            // 安全但是耗性能，最好是把克隆体的引用计数也维护进来。
            // Resources.UnloadUnusedAssets();
        }


        public void ReleaseAsset(AsyncOperationHandle handle)
        {
            Addressables.Release(handle);
        }
        public void ReleaseAsset(Object delegator)
        {
            if (_delegatorToHandles.TryGetValue(delegator, out var handleDic))
            {
                foreach (var handle in handleDic.Values)
                {
                    ReleaseAsset(handle);
                }
                _delegatorToHandles.Remove(delegator);
            }
        }

        public AsyncOperationHandle LoadAssetsAsync(List<string> addresses, Action<Object> loadComplete)
        {
            AsyncOperationHandle handle = Addressables.LoadAssetsAsync<Object>(addresses, (obj) =>
            {
                loadComplete?.Invoke(obj);
            }, MergeMode.Union);
            return handle;
        }

        // 这个只加载指定泛型资源，其他类型资源不会加载。addresses的并集(理所当然消除重复)
        public AsyncOperationHandle<IList<T>> LoadAssetsAsync<T>(List<string> addresses, Action<T> loadComplete) where T : Object
        {
            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(addresses, (obj) =>
            {
                loadComplete?.Invoke(obj);
            }, MergeMode.Union);
            return handle;
        }


    }
}