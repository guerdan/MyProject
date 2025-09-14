

using System.Collections;
using DG.Tweening;
using Script.Util;
using UnityEngine;


namespace Script.UI.Component
{
    public class LoadingProgressUI : MonoBehaviour
    {

        [SerializeField] public GameObject BlockNode;
        [SerializeField] public GameObject ImageNode;

        private Sequence sequence;

        void Awake()
        {
            Hide();
        }

        public void Show()
        {
            BlockNode.SetActive(true);

            var tween0 = DOTween.To(() => 0, v => {}, 1, 0.1f).OnComplete(() =>
            {
                ImageNode.SetActive(true);
            });

            var tween1 = TweenUtil.GetNodeFadeTween(BlockNode, 0, 255, 0.2f, Ease.Linear);
            sequence = DOTween.Sequence().Append(tween0).Append(tween1);
            sequence.Play();
        }
        public void Hide()
        {
            BlockNode.SetActive(false);
            ImageNode.SetActive(false);
            sequence?.Kill();
            sequence = null;
        }
        
    }
}