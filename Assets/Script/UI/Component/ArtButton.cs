
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.UI.Component
{
    // 按钮节点可以透明图，子节点挂实际图片
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(Button))]
    public class ArtButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float TargetScale = 0.85f;
        public float ScalingDuration = 0.1f;

        private RectTransform rect;
        private Image image;
        private Vector2 originalSize;
        private Tween touchStartTween;
        private Tween touchEndTween;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            originalSize = rect.sizeDelta;
            var button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
        }

        //touchstart 事件
        public void OnPointerDown(PointerEventData eventData)
        {
            Reset();
            float scale = 1;
            rect.localScale = new Vector3(scale, scale, scale);
            touchStartTween = DOTween.To(() => scale, x => { scale = x; }, TargetScale, ScalingDuration).OnUpdate(() =>
            {
                FillRaycastRegion(scale);
                rect.localScale = new Vector3(scale, scale, scale);
            });
        }

        // touchend 事件
        public void OnPointerUp(PointerEventData eventData)
        {
            Reset();
            float scale = TargetScale;
            rect.localScale = new Vector3(scale, scale, scale);
            touchEndTween = DOTween.To(() => scale, x => { scale = x; }, 1, ScalingDuration).OnUpdate(() =>
            {
                FillRaycastRegion(scale);
                rect.localScale = new Vector3(scale, scale, scale);
            });
        }

        // 缩小时，填充点击区域，优化体验
        private void FillRaycastRegion(float scale)
        {
            if (scale >= 1) return;
            var delta = originalSize * (1 - scale);
            var pivot = rect.pivot;
            //左 下 右 上
            image.raycastPadding = new Vector4(
                -delta.x * (pivot.x - 0),
                -delta.y * (pivot.y - 0),
                -delta.x * (1 - pivot.x),
                -delta.y * (1 - pivot.y)
                );
        }

        private void Reset()
        {
            touchStartTween?.Kill();
            touchStartTween = null;
            touchEndTween?.Kill();
            touchEndTween = null;
        }

        private void OnDestroy()
        {
            Reset();
        }

    }
}