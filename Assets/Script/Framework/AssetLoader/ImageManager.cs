
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Script.Framework.AssetLoader
{
    /// <summary>
    /// 管理加载 StreamingAssets下的图片
    /// 缓存 最多50张图片（可换最大容量），过容则淘汰最久未使用的，每个图片不使用超过5min也释放。
    /// 要根据外部使用情况，不能随意强制释放
    /// </summary>
    public class ImageManager
    {
        private static ImageManager _inst;
        public static ImageManager Inst
        { get { if (_inst == null) _inst = new ImageManager(); return _inst; } }

        /// <summary>
        /// Dic<路径，(精灵，缓存倒计时, 使用该图片的Image列表)>
        /// </summary>
        private Dictionary<string, ImageLoadItem> _cache = new Dictionary<string, ImageLoadItem>();


        private int _maxCacheCount = 50;
        private int _maxCacheDuration = 5 * 60;

        /// <summary>
        /// 路径传相对于StreamingAssets的路径。delegtor代理，销毁时会检查代理是否在使用
        /// </summary>
        public bool TryLoadSprite(string path, Image delegtor, out Sprite sprite, bool rgbaFormat = false, bool force = false)
        {

            if (!File.Exists(path))
            {
                sprite = null;
                return false;
            }

            if (!_cache.TryGetValue(path, out var item))
            {
                item = new ImageLoadItem();
                item.Sprite = LoadSpriteInStreaming(path, rgbaFormat);
                item.Refers = new HashSet<Image>();
                _cache[path] = item;
            }else if (force)
            {
                // 强制刷新, 旧的不管了。自己注意点
                var old = item.Sprite;
                item.Sprite = LoadSpriteInStreaming(path, rgbaFormat);
                UnityEngine.Object.Destroy(old);
                UnityEngine.Object.Destroy(old.texture);
            }

            item.CountDown = _maxCacheDuration;
            item.Refers.Add(delegtor);
            if (_cache.Count > _maxCacheCount)
                ReleaseOldest();

            sprite = item.Sprite;
            return true;
        }


        /// <summary>
        /// rgbaFormat：是否额外转成 RGBA32的Texture2D
        /// </summary>
        public Sprite LoadSpriteInStreaming(string path, bool rgbaFormat = false)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            // 需要时转换。
            if (rgbaFormat && tex.format == TextureFormat.ARGB32)
            {
                var old = tex;
                tex = ConvertARGB32ToRGBA32(old);
                UnityEngine.Object.Destroy(old);
            }

            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            return sprite;
        }

        Texture2D ConvertARGB32ToRGBA32(Texture2D source)
        {
            // 创建一个新的 RGBA32 格式的纹理
            Texture2D newTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1);

            // 获取原始像素数据
            Color32[] pixels = source.GetPixels32(0);

            // 调整通道顺序
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color = pixels[i];
                // ARGB32 to RGBA32
                // pixels[i] = new Color32(color.g, color.b, color.a, color.a);
            }

            // 将调整后的像素数据设置到新纹理
            newTexture.SetPixels32(pixels, 0);
            newTexture.Apply();

            return newTexture;
        }

        // 如果是队列的话：增删的时候麻烦，update省力。理解度低
        // 如果是字典的话：update的时候麻烦，增删省力。理解度高
        public void OnUpdate(float deltaTime)
        {
            foreach (var key in _cache.Keys.ToList())
            {
                var tuple = _cache[key];
                tuple.CountDown -= deltaTime;
                if (tuple.CountDown <= 0)
                {
                    // 释放资源
                    Release(key);
                }
            }
        }


        void Release(string imgPath)
        {
            if (!_cache.TryGetValue(imgPath, out var item))
                return;

            // 如果还有持有引用的Image，则重置倒计时    
            bool inUse = false;
            foreach (var img in item.Refers.ToList())
            {
                if (img != null && img.sprite == item.Sprite)
                {
                    inUse = true;
                    break;
                }
            }
            if (inUse)
            {
                item.CountDown = _maxCacheDuration;
                return;
            }

            _cache.Remove(imgPath);
            var sprite = item.Sprite;
            UnityEngine.Object.Destroy(sprite);         //原理：真正的销毁在帧末处理
            UnityEngine.Object.Destroy(sprite.texture);
        }

        void ReleaseOldest()
        {
            string name = "";
            ImageLoadItem oldest = null;
            foreach (var kv in _cache)
            {
                if (oldest == null || kv.Value.CountDown < oldest.CountDown)
                {
                    name = kv.Key;
                    oldest = kv.Value;
                }
            }

            if (name != "")
                Release(name);
        }

        /// <summary>
        /// 将 配置字段 拼接成完整路径。
        /// 管理图片都在 StreamingAssets下，配置字段存的是StreamingAssets后的路径
        /// </summary>
        public static string GetFullPath(string path)
        {
            return $"{Application.streamingAssetsPath}/{path}.png";
        }

        // public static string SimplifyPath(string path)
        // {
        //     return path.Replace(Application.streamingAssetsPath, "");
        // }
    }

    public class ImageLoadItem
    {
        public Sprite Sprite;
        public float CountDown;
        public HashSet<Image> Refers;
    }
}