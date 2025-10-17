using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.Util
{
    public static class Utils
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
        /// UI下，非相同父节点的位置复制。
        /// 解耦target.pivot
        /// use_pivot = false时，表示用target的(0.5,0.5)作为参考点
        /// </summary>
        public static Vector2 GetPos(RectTransform actor, RectTransform target, Vector2 offset, bool use_pivot = false)
        {
            Vector2 pivot_offset = new Vector2(
                (0.5f - target.pivot.x) * target.rect.width,
                (0.5f - target.pivot.y) * target.rect.height
            );
            // 1. 获取 nodeA 在世界空间的位置
            Vector3 worldPos = target.position;

            // 2. 将 worldPos 转为 parentB 下的本地坐标
            Vector2 localPoint;
            RectTransform parentRect = actor.parent.GetComponent<RectTransform>();
            Canvas canvas = actor.GetComponentInParent<Canvas>();
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out localPoint);

            if (use_pivot)
                return localPoint + offset;
            else
                return localPoint + pivot_offset + offset;
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



        /// <summary>
        /// 判断 target 的 RectTransform 区域与 eventData.position 是否相交
        /// </summary>
        public static bool IsPointerOverUIObject(GameObject target, Canvas canvas)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            if (target == null || canvas == null || eventData == null) return false;
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform == null) return false;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, cam);
        }


        public static void CommonSort(List<string> list)
        {
            list.Sort((a, b) =>
            {
                if (a.Length != b.Length)
                    return a.Length.CompareTo(b.Length);
                else
                    return string.Compare(a, b, StringComparison.Ordinal);
            });
        }


        /// <summary>
        /// 能够控制点击穿透的接口
        /// </summary>
        public static void SetCanClick(GameObject target, bool canClick)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (!(canvasGroup))
                canvasGroup = target.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = canClick;
        }


    }


}