using System;
using DG.Tweening;
using Script.UI.Component;
using Script.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Script.Framework.UI
{
    public interface IPanel
    {
        PanelDefine PanelDefine { get; set; }
        int StackIndex { get; set; }
        Transform Transform { get; }
        bool Display { get; }
        void SetData(object data);
        void BeforeShow();
        void OnShow(Action cb);
        void AfterShow();
        void BeforeHide();
        void OnHide(Action cb);
        void AfterHide();
        //Panel找UIManager回收
        void Recycle();
        void OnShowContent();
        void OnHideContent();

    }
    public class BasePanel : MonoBehaviour, IPanel, IPointerDownHandler, IPointerUpHandler
    {

        [SerializeField] public Button[] CloseButtons;

        public static event Action<PanelEnum> BeforeShowEvent;
        public static event Action<PanelEnum> AfterShowEvent;
        public static event Action<PanelEnum> BeforeHideEvent;
        public static event Action<PanelEnum> AfterHideEvent;
        protected const float OpenAnimDuration = 0.4f;
        protected const float CloseAnimDuration = 0.1f;
        protected const float MaskOpacity = 200;        //手游220

        protected bool display = false;      //打开状态
        protected Sequence openSeq;          //弹窗打开动画
        protected Sequence closeSeq;         //弹窗关闭动画


        protected GameObject _maskBgNode;
        private PanelDefine _panelDefine;
        private int _stackIndex;

        protected bool _animEnable = true;
        protected bool _useScaleAnim = true;


        public bool Display { get { return display; } }
        public PanelDefine PanelDefine { get { return _panelDefine; } set { _panelDefine = value; } }
        public int StackIndex { get { return _stackIndex; } set { _stackIndex = value; } }
        public Transform Transform { get { return transform; } }
        public Transform Content { get { return transform.Find("Content"); } }
        public bool ShowMask { get { return _panelDefine.ClickOutWinClose; } }
        public bool AnimEnable { get { return _animEnable && PanelDefine.Type != UITypeEnum.Full; } }  //全屏式界面不提供默认动画，如需要自行实现

        public virtual void SetData(object data) { }

        public virtual void BeforeShow()
        {
            display = true;

            if (ShowMask && _maskBgNode == null)
            {
                CreateMaskBg();
            }

            CloseButtonAddListener();
            // AudioManager.Inst.PlaySound(AudioEnum.PanelOpen); //播放音效
            BeforeShowEvent?.Invoke(PanelDefine.Key);
            // Debug.Log($"BeforeShow Panel {PanelDefine.Name} {DateTime.UtcNow} {DateTime.UtcNow.Millisecond}");

            RefreshPos();
            GetComponentInChildren<DragPanel>()?.SetData(this);
        }


        public virtual void AfterShow()
        {
            AfterShowEvent?.Invoke(PanelDefine.Key);
        }

        public virtual void BeforeHide()
        {
            display = false;
            // AudioManager.Inst.PlaySound(AudioEnum.PanelClose);
            BeforeHideEvent?.Invoke(PanelDefine.Key);

        }

        public virtual void AfterHide()
        {
            AfterHideEvent?.Invoke(PanelDefine.Key);
        }

        //如需要重写动画时，赋值openSeq即可
        public virtual void OnShow(Action cb)
        {
            if (!AnimEnable || Content == null)
            {
                cb?.Invoke();
                return;
            }

            var tween0 = _useScaleAnim ? TweenUtil.GetNodeScaleTween(Content.gameObject, 0.8f, 1f, OpenAnimDuration, Ease.OutBack)
             : TweenUtil.GetEmptyTween();
            var tween1 = TweenUtil.GetNodeFadeTween(Content.gameObject, 0, 255, OpenAnimDuration, Ease.OutBack);
            openSeq = DOTween.Sequence().Join(tween0).Join(tween1)
                .OnComplete(() => { cb?.Invoke(); });

            var last = UISceneMixin.Inst.FindPanel(PanelDefine.Layer, 1);
            var uiManager = UIManager.Inst as UIManager;

            if (ShowMask)
            {
                if (uiManager.NeedHideLast(PanelDefine, last))
                {
                    _maskBgNode.GetComponent<Image>().color = new Color(0, 0, 0, MaskOpacity / 255);
                }
                else
                {
                    var tween2 = TweenUtil.GetImageFadeTween(_maskBgNode.GetComponent<Image>(),
                        0, MaskOpacity, OpenAnimDuration, Ease.OutBack);
                    tween2.SetEase(TweenUtil.OutBackEaseClip);
                    openSeq.Join(tween2);
                }
            }

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

            var tween0 = _useScaleAnim ? TweenUtil.GetNodeScaleTween(Content.gameObject, 1f, 0.8f, CloseAnimDuration, Ease.Linear)
            : TweenUtil.GetEmptyTween();
            var tween1 = TweenUtil.GetNodeFadeTween(Content.gameObject, 255, 0, CloseAnimDuration, Ease.InOutQuad);
            closeSeq = DOTween.Sequence().Join(tween0).Join(tween1)
                .OnComplete(() => { cb?.Invoke(); });

            var last = UISceneMixin.Inst.PeekPanel(PanelDefine.Layer);
            var uiManager = UIManager.Inst as UIManager;

            if (ShowMask)
            {
                if (uiManager.NeedHideLast(PanelDefine, last))
                {
                    _maskBgNode.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                }
                else
                {
                    var tween2 = TweenUtil.GetImageFadeTween(_maskBgNode.GetComponent<Image>(),
                        MaskOpacity, 0, CloseAnimDuration, Ease.InOutQuad);
                    closeSeq.Join(tween2);
                }
            }

            closeSeq.Play();
        }


        public void Recycle()
        {
            ClearAnim();
            var uiManager = UIManager.Inst as UIManager;
            uiManager.Recycle(this);
        }

        public void OnShowContent()
        {
            if (Content != null) Content.gameObject.SetActive(true);
            if (ShowMask) _maskBgNode.gameObject.SetActive(true);
        }
        public void OnHideContent()
        {
            if (Content != null) Content.gameObject.SetActive(false);
            if (ShowMask) _maskBgNode.gameObject.SetActive(false);
        }

        public virtual void Close()
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
            rect.sizeDelta = new Vector2(1920 * 2, 1080 * 2);

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

        public virtual void RefreshPos()
        {
            ((RectTransform)transform).anchoredPosition = PanelDefine.InitPos;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (PanelDefine.Type == UITypeEnum.WindowsPopUp)
            {
                UISceneMixin.Inst.ToFirst(this, null);
                // DU.LogWarning($"ToFirst {PanelDefine.Name}");
            }
        }
    }
}