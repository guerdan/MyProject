
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.UI.Component
{
    public class ScaleArtComp : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float BiggerScale = 1.07f;
        [SerializeField] private float ScalingDuration = 0.1f;
        private Tween touchStartTween;
        private Tween touchEndTween;

        private Action<PointerEventData> _onPointerEnter;
        private Action<PointerEventData> _onPointerExit;

        public void SetData(Action<PointerEventData> onPointerEnter = null, Action<PointerEventData> onPointerExit = null)
        {
            _onPointerEnter = onPointerEnter;
            _onPointerExit = onPointerExit;
        }

        //鼠标悬浮进入事件
        public void OnPointerEnter(PointerEventData eventData)
        {
            _onPointerEnter?.Invoke(eventData);

            Reset();
            float scale = 1;
            transform.localScale = new Vector3(scale, scale, scale);
            touchStartTween = DOTween.To(() => scale, x => { scale = x; }, BiggerScale, ScalingDuration).OnUpdate(() =>
            {
                transform.localScale = new Vector3(scale, scale, scale);
            });
        }

        //鼠标悬浮离开事件
        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointerExit?.Invoke(eventData);

            Reset();
            float scale = BiggerScale;
            transform.localScale = new Vector3(scale, scale, scale);
            touchEndTween = DOTween.To(() => scale, x => { scale = x; }, 1, ScalingDuration).OnUpdate(() =>
            {
                transform.localScale = new Vector3(scale, scale, scale);
            });
        }

        private void Reset()
        {
            touchStartTween?.Kill();
            touchStartTween = null;
            touchEndTween?.Kill();
            touchEndTween = null;

            transform.localScale = Vector3.one;
        }

    }
}