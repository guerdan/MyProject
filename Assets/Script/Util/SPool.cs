
using System.Collections.Generic;
using UnityEngine;

namespace Script.Util
{
    /// <summary>
    /// Simple-Pool,简单的对象池
    /// </summary>
    public class SPool
    {

        private GameObject _prefab;
        private List<GameObject> _cache;

        public SPool(GameObject prefab, int init_count = 0)
        {
            _prefab = prefab;
            _cache = new List<GameObject>();

            for (int i = 0; i < init_count; i++)
            {
                GameObject newOne = Generate();
                Push(newOne);
            }
        }


        /// <summary>
        /// 取出一个对象
        /// </summary>
        public GameObject Pop()
        {
            if (_cache.Count > 0)
            {
                var last = _cache[_cache.Count - 1];
                _cache.RemoveAt(_cache.Count - 1);
                last.SetActive(true);
                return last;
            }
            GameObject newOne = Generate();
            newOne.SetActive(true);
            return newOne;
        }

        public GameObject Generate()
        {
            GameObject newOne = GameObject.Instantiate(_prefab);
            return newOne;
        }


        public void Push(GameObject item)
        {
            item.SetActive(false);
            _cache.Add(item);
        }

    }
}