
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
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
        private Dictionary<string, (Sprite, int, HashSet<Image>)> _cache = new Dictionary<string, (Sprite, int, HashSet<Image>)>();


        private int _maxCacheCount = 50;
        private int _maxCacheDuration = 5 * 60;

        /// <summary>
        /// 路径传相对于StreamingAssets的路径。delegtor代理，销毁时会检查代理是否在使用
        /// </summary>
        public bool TryLoadSprite(string path, Image delegtor, out Sprite sprite)
        {

            if (!File.Exists(path))
            {
                sprite = null;
                return false;
            }

            if (!_cache.TryGetValue(path, out var tuple))
            {
                tuple.Item1 = LoadSpriteInStreaming(path);
                tuple.Item3 = new HashSet<Image>();
                _cache[path] = tuple;
            }

            tuple.Item2 = _maxCacheDuration;
            tuple.Item3.Add(delegtor);
            if (_cache.Count > _maxCacheCount)
                ReleaseOldest();

            sprite = tuple.Item1;
            return true;
        }



        private Sprite LoadSpriteInStreaming(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            var sprite = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            return sprite;
        }

        // 如果是队列的话：增删的时候麻烦，update省力。理解度低
        // 如果是字典的话：update的时候麻烦，增删省力。理解度高
        public void OnUpdate(float deltaTime)
        {
            foreach (var key in _cache.Keys.ToList())
            {
                var tuple = _cache[key];
                tuple.Item2 -= (int)deltaTime;
                if (tuple.Item2 <= 0)
                {
                    // 释放资源
                    Release(key);
                }
            }
        }


        void Release(string imgPath)
        {
            if (!_cache.TryGetValue(imgPath, out var tuple))
                return;

            // 如果还有持有引用的Image，则重置倒计时    
            bool inUse = false;
            foreach (var img in tuple.Item3.ToList())
            {
                if (img != null && img.sprite == tuple.Item1)
                {
                    inUse = true;
                    break;
                }
            }
            if (inUse)
            {
                tuple.Item2 = _maxCacheDuration;
                return;
            }
            
            _cache.Remove(imgPath);
            var sprite = tuple.Item1;
            UnityEngine.Object.Destroy(sprite);         //原理：真正的销毁在帧末处理
            UnityEngine.Object.Destroy(sprite.texture);
        }

        void ReleaseOldest()
        {
            string name = "";
            (Sprite, int, HashSet<Image>) oldest = (null, int.MaxValue, null);
            foreach (var kv in _cache)
            {
                if (kv.Value.Item2 < oldest.Item2)
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
}