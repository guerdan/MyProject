using System;
using Script.Framework.AssetLoader;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;


namespace Script.Util
{
    /// <summary>
    /// LoadAsset 
    /// 有个场景。组件对象池，需要不断地更换加载图片。怎么卸载
    /// 解决方案：在它放回对象池时，手动清空所有引用并释放代理对象的handle。
    /// 1. 封装有着更精确delegator的常用接口
    /// </summary>
    public static class AssetUtil
    {

        /// <summary>
        /// 加载预制件。绑定实例与资源。
        /// 测试了 LoadPrefab接口，从内存监视器上看没问题
        /// </summary>
        public static void LoadPrefab(string path, Func<GameObject, GameObject> callback)
        {
            AssetManager.Inst.LoadAssetAsync<GameObject>(path, (prefab, handle) =>
            {
                GameObject go = callback(prefab);
                AssetManager.Inst.BindDelegator(path, go, handle);
            }, null, UnloadMode.NotAuto);
        }


        /// <summary>
        /// 加载图片精灵。绑定Image与资源
        /// 图片路径是：图集资源的路径+[图片名]
        /// </summary>
        public static void SetImage(string path, Image image)
        {
            AssetManager.Inst.LoadAssetAsync<Sprite>(path, (sprite, _) =>
            {
                image.sprite = sprite;
            }, image);
        }
    }
}