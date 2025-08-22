using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.Util
{
    public class Utils
    {

        public const string CustomToolsPath = "CustomTools/";
        public const string StartOperationStringFormat = ">>>----------Start {0}---------->>>";
        public const string EndOperationStringFormat = "<<<----------End {0}----------<<<";

        /// <summary>
        /// 如果没有什么特别组件而想存成List[GameObject]，就改用存List[Transform]或者List[RectTransform]
        /// 以达到相同的效果
        /// 1.如何高效率地添加item,需要知道当前已经用了几个，想再用几个
        /// </summary>
        public static void RefreshItemListByCount<T>(List<T> list, int count
        , GameObject prefab, Transform parent, Action<T, int> process) where T : Component
        {
            //如果没有什么特别组件而想存成List<GameObject>，就改用存List<Transform>或者List<RectTransform>以达到相同的效果
            if (list == null || prefab == null) return;

            int need = count - list.Count;
            for (int i = 0; i < need; i++)
            {
                GameObject go = GameObject.Instantiate(prefab, parent, false);
                go.transform.localScale = Vector3.one;
                list.Add(go.GetComponent<T>());
            }

            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                bool show = i < count;
                item.gameObject.SetActive(show);

                if (show && process != null)
                    process(item, i);
            }
        }



        public static Rect ConvertRect(OpenCvSharp.Rect rect)
        {
            return new Rect(rect.X, -rect.Y, rect.Width, rect.Height);
        }
        public static void SetActive(GameObject go, bool show)
        {
            if (go != null)
                go.SetActive(show);
        }
        public static void SetActive(Component comp, bool show)
        {
            if (comp != null)
                comp.gameObject.SetActive(show);
        }


        public static Color ParseHtmlString(string htmlColor)
        {
            if ((htmlColor.Length != 7 && htmlColor.Length != 9) || !htmlColor.StartsWith("#"))
            {
                DU.LogError($"非法 HTML color string: {htmlColor}");
                return Color.white;
            }

            string r_s = htmlColor.Substring(1, 2);
            string g_s = htmlColor.Substring(3, 2);
            string b_s = htmlColor.Substring(5, 2);
            float r = Convert.ToInt32(r_s, 16) / 255f;
            float g = Convert.ToInt32(g_s, 16) / 255f;
            float b = Convert.ToInt32(b_s, 16) / 255f;
            Color color = new Color(r, g, b, 1f);


            return color;
        }

        /// <summary>
        /// UI下，非相同父节点的位置复制
        /// </summary>
        public static Vector2 GetPos(RectTransform actor, RectTransform target, Vector2 offset)
        {
            // 1. 获取 nodeA 在世界空间的位置
            Vector3 worldPos = target.position;

            // 2. 将 worldPos 转为 parentB 下的本地坐标
            Vector2 localPoint;
            RectTransform parentRect = actor.parent.GetComponent<RectTransform>();
            Canvas canvas = actor.GetComponentInParent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out localPoint);

            return localPoint + offset;
        }

        /// <summary>
        /// UI下，获取child相对于父节点的本地位置
        /// 先只实现锚点重合的情况
        /// </summary>
        public static Vector2 GetRelativePosToParent(RectTransform child)
        {
            var pos = child.anchoredPosition;
            if (child.anchorMin != child.anchorMax)
                return pos;

            var parent = child.parent as RectTransform;
            var size = parent.rect;
            var anchor = child.anchorMin;

            pos = pos + new Vector2(
                (anchor.x - 0.5f) * size.width,
                (anchor.y - 0.5f) * size.height
            );   

            return pos;
        }
    }

    /// <summary>
    /// 只适用于总控显隐 => 一句代码控制所有个体显隐
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class UICacheList<T> : List<T> where T : Component
    {

        private int _activeCount = 0;
        private GameObject _prefab;
        private Transform _parent;

        /// <summary>
        /// 更新全部
        /// </summary>
        public void RefreshItemListByCount(int activeCount, GameObject prefab,
        Transform parent, Action<T, int> process)
        {
            //如果没有什么特别组件而想存成List<GameObject>，就改用存List<Transform>或者List<RectTransform>以达到相同的效果
            if (prefab == null) return;

            _activeCount = activeCount;
            _prefab = prefab;
            _parent = parent;

            int need = _activeCount - Count;
            for (int i = 0; i < need; i++)
            {
                GameObject go = GameObject.Instantiate(prefab, parent, false);
                go.transform.localScale = Vector3.one;
                Add(go.GetComponent<T>());
            }

            for (int i = 0; i < Count; i++)
            {
                T item = this[i];
                bool show = i < _activeCount;
                item.gameObject.SetActive(show);

                if (show && process != null)
                    process(item, i);
            }

        }
        /// <summary>
        /// 只更新新加入的
        /// </summary>
        public void AddItem(int addCount, Action<T, int> process)
        {
            int startIndex = _activeCount;
            _activeCount += addCount;

            int need = _activeCount - Count;
            for (int i = 0; i < need; i++)
            {
                GameObject go = GameObject.Instantiate(_prefab, _parent, false);
                go.transform.localScale = Vector3.one;
                Add(go.GetComponent<T>());
            }

            for (int i = startIndex; i < Count; i++)
            {
                T item = this[i];
                bool show = i < _activeCount;
                item.gameObject.SetActive(show);

                if (show && process != null)
                    process(item, i);
            }
        }

        /// <summary>
        /// 删除指定的对象
        /// </summary>
        /// <param name="item"></param>
        public void RemoveItem(T item)
        {
            var index = IndexOf(item);
            if (index < 0) return;
            item.gameObject.SetActive(false);
            _activeCount--;
            Remove(item);
            Add(item);
        }
    }
}