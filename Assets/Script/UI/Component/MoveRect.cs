using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Component
{
    public class MoveRect : MonoBehaviour
    {

        public Button setStartBtn;
        private float myValue;
        private Sequence sequence;

        void Start()
        {
            setStartBtn.onClick.AddListener(onSetStartBtnClick);
        }

        void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
        }


        void onSetStartBtnClick()
        {
            if (sequence == null)
            {
                sequence = DOTween.Sequence();

                myValue = 0;

                var t1 = DOTween
                .To(() => myValue, x => myValue = x, 200f, 2f)
                .SetEase(Ease.InOutQuad) // 指定缓动类型
                .SetDelay(1f) // 添加 1 秒的延迟
                    .OnUpdate(() =>
                    {
                        transform.localPosition = new Vector3(myValue, myValue, myValue);
                        Debug.Log("Tween update! ");
                    })
                    .OnComplete(() =>
                    {
                        // 缓动完成时的回调
                        Debug.Log("Tween complete! Final myValue: " + myValue);
                    });


                sequence.Append(t1);//物体3秒移动到（1，1，1）

            }

        }


    }
}