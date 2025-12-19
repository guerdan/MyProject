using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Components
{
    public class MoveRect : MonoBehaviour
    {

        public Button setStartBtn;
        private float myValue;
        private Sequence sequence;

        void Start()
        {
            setStartBtn?.onClick.AddListener(onSetStartBtnClick);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                onSetStartBtnClick();
            }
        }

        void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }


        void onSetStartBtnClick()
        {
            sequence?.Kill();
            sequence = null;

            sequence = DOTween.Sequence();
            var ori = transform.localPosition;
            myValue = ori.y;

            var t1 = DOTween
            .To(() => myValue, x => myValue = x, 20f, 200f)
            .SetEase(Ease.InOutQuad) // 指定缓动类型
            .SetDelay(1f) // 添加 1 秒的延迟
                .OnUpdate(() =>
                {
                    transform.localPosition = new Vector3(ori.x, myValue, ori.z);
                    // Debug.Log("Tween update! ");
                })
                .OnComplete(() =>
                {
                    // 缓动完成时的回调
                    // Debug.Log("Tween complete! Final myValue: " + myValue);
                });


            sequence.Append(t1);//物体3秒移动到（1，1，1）
            sequence.Play();

        }


    }
}