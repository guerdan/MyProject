
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Util
{
    public static class TweenUtil
    {
        public static Tween GetNodeScaleTween(GameObject node, float start, float end, float duration, Ease ease)
        {
            var trans = node.transform;

            float scale = start;
            trans.localScale = Vector3.one * scale;
            return DOTween
                .To(() => scale, x => scale = x, end, duration)
                .SetEase(ease)
                .OnUpdate(() =>
                    {
                        trans.localScale = Vector3.one * scale;
                    });
        }
        public static Tween GetNodeFadeTween(GameObject node, float start, float end, float duration, Ease ease)
        {
            CanvasGroup group = node.GetComponent<CanvasGroup>();
            if (group == null) group = node.AddComponent<CanvasGroup>();

            float opa = start;
            group.alpha = opa / 255;
            return DOTween
                .To(() => opa, x => opa = x, end, duration)
                .SetEase(ease)
                .OnUpdate(() =>
                    {
                        group.alpha = opa / 255;
                    });
        }
        public static Tween GetImageFadeTween(Image img, float start, float end, float duration, Ease ease)
        {
            float opa = start;
            img.color = new Color(0, 0, 0, opa / 255);
            return DOTween
                .To(() => opa, x => opa = x, end, duration)
                .SetEase(ease)
                .OnUpdate(() =>
                    {
                        img.color = new Color(0, 0, 0, opa / 255);
                    });
        }

        public static float OutBackEase(float time, float duration, float overshootOrAmplitude, float period)
        {
            float s = 1.70158f;
            float t = time / duration - 1;
            return t * t * ((s + 1) * t + s) + 1;
        }


        public static float OutBackEaseClip(float time, float duration, float overshootOrAmplitude, float period)
        {
            float s = 1.70158f;
            float t = time / duration - 1;
            var result = t * t * ((s + 1) * t + s) + 1;
            return Mathf.Clamp(result, 0, 1);
        }

        public static float InBackEase(float time, float duration, float overshootOrAmplitude, float period)
        {
            float s = 1.70158f;
            float t = time / duration;
            return t * t * ((s + 1) * t - s);
        }
        public static float InBackEaseClip(float time, float duration, float overshootOrAmplitude, float period)
        {
            float s = 1.70158f;
            float t = time / duration;
            var result = t * t * ((s + 1) * t - s);
            return Mathf.Clamp(result, 0, 1);
        }

    }
}