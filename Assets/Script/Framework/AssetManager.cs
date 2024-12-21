using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Object = UnityEngine.Object;


namespace Script.Framework
{
    /// <summary>
    /// 1 所有UnityEngine Object对象分为预制体和克隆体。
    /// 2 Destroy接口能销毁一切克隆体，但不能销毁预制体。如果是GameObject自动删除其下挂接的子节点和组件，但不会销毁引用资源（如材质、纹理等）
    /// 容易造成泄漏的点：
    /// 1.预制体的引用计数未正常删减。
    /// 2.游离的引用资源的克隆体。例如：SpriteAtlas的精灵是个克隆体，它既不会被Addressables自动销毁，也不会被Destroy(GameObject)而销毁。最保守但低效的做法是周期性地调用 Resources.UnloadUnusedAssets();
    /// 
    /// 方案：只有完全熟悉了Addressables接口和Unity的资源与克隆体理念，才能做套适配方案。
    /// 代理回收，就是延续引用计数方案，还适合跟踪。
    /// </summary>
    public interface IAssetManager
    {

        /// <summary>
        /// 异步加载资源，代理接口。已加载过的资源，可以同步调用。
        /// 1.加载期间代理对象若被销毁，则不执行回调。2.代理对象被销毁，将释放对此资源的引用计数
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="address">配置资源名</param>
        /// <param name="loadComplete"></param>
        /// <param name="delegtor"> 代理对象最好就是持有者本身，也可以是更高层的管理者 </param>
        void LoadAsync<T>(string address, Action<T> loadComplete, Object delegtor) where T : Object;
        // 异步加载资源，原始接口。应用层自行决定何时释放资源。暂存handle并调用ReleaseAsset接口释放引用计数
        void LoadAsyncEmpty<T>(string address, Action<T, AsyncOperationHandle> loadComplete) where T : Object;
        // 绑定代理与handle
        void BindDelegator(string address, Object delagtor, AsyncOperationHandle handle);

        /// <summary>
        /// 清理资源缓存。
        /// 用过或熟悉 Addressable 的同学应该都知道，Addressable使用引用计数来维护资源的使用，
        /// 当某个资源引用计数为0时，便会将其卸载，待整个bundle引用计数为0时，再将整个bundle卸载（此时释放资源内存）。
        /// </summary>
        void ReleaseUnuseAsset();
        // 清理handle资源缓存。
        void ReleaseAsset(AsyncOperationHandle handle);
        // 清理delagtor的所有依赖handle。
        void ReleaseAsset(Object delegtor);
    }

    public partial class AssetManager : IAssetManager
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


        // <代理对象, <资源地址, handle>字典> 代理对象销毁时释放相关handle。
        // handle代表资源唯一性。同个资源Addressables会返回相同的handle。<代理对象, handle>代表一次引用计数。同个代理与同个handle，在_delegatorToHandles最多占一次。
        private Dictionary<Object, Dictionary<string, AsyncOperationHandle>> _delegatorToHandles;

        // <图集资源, <精灵名, Sprite(Clone)>字典> 用于缓存图集中的精灵，待图集资源被销毁时销毁Sprite克隆体。
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
        public void LoadAsync<T>(string address, Action<T> loadComplete, Object delagtor) where T : Object
        {
            if (delagtor == null)
            {
                Debug.LogWarning("[AssetManager] delagtor is null, 接口调用错误");
                return;
            }

            Action<string, T, AsyncOperationHandle> loadCompleteAction =
            (address, asset, handle) =>
            {
                // 说明代理对象已经被销毁，不用向后执行并释放资源。设计上，保证回调安全性。
                if (delagtor == null)
                {
                    ReleaseAsset(handle);
                    return;
                }

                loadComplete.Invoke(asset);
                BindDelegator(address, delagtor, handle);
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

        /// <summary>
        /// 生接口。应用层自行决定何时释放资源。调用ReleaseAsset接口释放
        /// </summary>
        public void LoadAsyncEmpty<T>(string address, Action<T, AsyncOperationHandle> loadComplete) where T : Object
        {

            LoadAssetInType<T>(address, (assetAddress, asset, handle) => loadComplete.Invoke(asset, handle));
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
                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    {
                        Debug.LogError($"Load Asset Async Failed: {address}.");
                        return;
                    }
                    loadCompleteAction.Invoke(address, handle.Result, handle);
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
    }
}