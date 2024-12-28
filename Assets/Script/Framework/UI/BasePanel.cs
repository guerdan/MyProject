using System;
using System.Threading;
using DG.Tweening;
using Script.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Framework.UI
{
    public interface IPanel
    {
        PanelDefine PanelDefine { get; set; }
        Transform Transform { get; }
        void SetData(object data);
        void BeforeShow();
        void OnShow(Action cb);
        void AfterShow();
        void BeforeHide();
        void OnHide(Action cb);
        void AfterHide();
        //Panel找UIManager回收
        void Recycle();
    }
    public class BasePanel : MonoBehaviour, IPanel
    {

        [SerializeField] public Button[] CloseButtons;
        protected const float OpenAnimDuration = 0.4f;
        protected const float CloseAnimDuration = 0.12f;
        // protected const float MaskOpacity = 85;
        protected const float MaskOpacity = 204;

        protected bool display = false;      //打开状态
        protected Sequence openSeq;          //弹窗打开动画
        protected Sequence closeSeq;         //弹窗关闭动画


        protected GameObject _maskBgNode;
        private PanelDefine _panelDefine;
        private bool _animEnable = true;


        public PanelDefine PanelDefine { get { return _panelDefine; } set { _panelDefine = value; } }
        public Transform Transform { get { return transform; } }
        public Transform Content { get { return transform.Find("Content"); } }
        public bool AnimEnable { get { return _animEnable && PanelDefine.Type != UITypeEnum.Full; } }  //全屏式界面不提供默认动画，如需要自行实现

        public virtual void SetData(object data) { }

        public virtual void BeforeShow()
        {
            if (_maskBgNode == null)
            {
                CreateMaskBg();
                CloseButtonAddListener();
            }
            // Debug.Log($"BeforeShow Panel {PanelDefine.Name} {DateTime.UtcNow} {DateTime.UtcNow.Millisecond}");
        }


        public virtual void AfterShow()
        {
            display = true;

        }

        public virtual void BeforeHide()
        {
            display = false;

        }

        public virtual void AfterHide()
        {
        }

        //如需要重写动画时，赋值openSeq即可
        public virtual void OnShow(Action cb)
        {
            if (!AnimEnable || Content == null)
            {
                cb?.Invoke();
                return;
            }

            var tween0 = TweenUtil.GetNodeScaleTween(Content.gameObject, 0.7f, 1f, OpenAnimDuration, Ease.OutBack);
            var tween1 = TweenUtil.GetNodeFadeTween(Content.gameObject, 0, 255, OpenAnimDuration, Ease.OutBack);
            var tween2 = TweenUtil.GetImageFadeTween(_maskBgNode.GetComponent<Image>(),
                0, MaskOpacity, OpenAnimDuration, Ease.OutBack);

            openSeq = DOTween.Sequence().Join(tween0).Join(tween1).Join(tween2)
                .OnComplete(() => { cb?.Invoke(); });
            openSeq.Play();
        }


        //如需要重写动画时，赋值closeSeq即可
        public virtual void OnHide(Action cb)
        {
            if (!AnimEnable || Content == null)
            {
                cb?.Invoke();
                return;
            }

            var tween0 = TweenUtil.GetNodeScaleTween(Content.gameObject, 1f, 0.7f, CloseAnimDuration, Ease.InBack);
            var tween1 = TweenUtil.GetNodeFadeTween(Content.gameObject, 255, 0, CloseAnimDuration, Ease.InBack);
            var tween2 = TweenUtil.GetImageFadeTween(_maskBgNode.GetComponent<Image>(),
                MaskOpacity, 0, CloseAnimDuration, Ease.InBack);

            closeSeq = DOTween.Sequence().Join(tween0).Join(tween1).Join(tween2)
                .OnComplete(() => { cb?.Invoke(); });
            closeSeq.Play();
        }


        public void Recycle()
        {
            ClearAnim();
            UIManager.Inst.Recycle(this);
        }

        public void Close()
        {
            if (!display) return;
            UIManager.Inst.PopPanel(PanelDefine.Layer);
        }
        public void CloseSelf()
        {
            if (!display) return;
            UIManager.Inst.PopPanel(PanelDefine.Key);
        }


        protected void ClearAnim()
        {
            openSeq?.Kill();
            openSeq = null;
            closeSeq?.Kill();
            closeSeq = null;
        }

        //创建一个背景遮罩，阻挡点击，（需要的话）呈黑色
        private void CreateMaskBg()
        {
            _maskBgNode = new GameObject("MaskBg");
            var rect = _maskBgNode.AddComponent<RectTransform>();
            var img = _maskBgNode.AddComponent<Image>();
            _maskBgNode.transform.SetParent(transform, false);
            _maskBgNode.transform.SetAsFirstSibling();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 0);
            img.raycastTarget = true;

            if (PanelDefine.Type == UITypeEnum.Full)
            {
                img.color = new Color(0, 0, 0, 0);
            }
            else
            {
                var button = _maskBgNode.AddComponent<Button>();
                img.color = new Color(0, 0, 0, 0.86f);
                button.onClick.AddListener(Close);
            }

        }

        private void CloseButtonAddListener()
        {
            foreach (var button in CloseButtons)
            {
                if (button == null) continue;
                button.onClick.AddListener(Close);
            }
        }
    }
}