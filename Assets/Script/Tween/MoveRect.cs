using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Script.Tween
{
    public class MoveRect:MonoBehaviour
    {
        
        public Button setStartBtn;
        
        public GameObject child;

        public GameObject myObject;

        void Start()
        {
            setStartBtn.onClick.AddListener(onSetStartBtnClick);
            
            
            // // 创建一个新的 GameObject
            // myObject = new GameObject("MyObject");
            //
            // // 销毁 GameObject
            // Destroy(myObject);
            //
            // // 在下一帧之前，myObject 引用仍然存在
            // Helper.Log((myObject != null).ToString()); // 输出: True
            //
            // // 尝试访问已销毁对象的属性
            // Helper.Log(myObject.name); // 输出: MyObject
            
            var go = child.GetComponent<MoveParent>().myObject;
            Helper.Log(go != null ? "has" :"no"); // 输出: True
            
            
        }
        
        // void Update()
        // {
        //
        //     // 检查对象是否已销毁
        //     if (myObject == null)
        //     {
        //         Helper.Log("myObject is null");
        //     }
        //     else
        //     {
        //         Helper.Log(myObject.name); // 安全访问属性
        //     }
        // }

        void onSetStartBtnClick()
        {
            Sequence sequence = DOTween.Sequence();
            // var rectTrans = GetComponent<RectTransform>();


            Helper.Log("调用前");
            var t1 = transform.DOMove(Vector3.one, 3);
            sequence.Append(t1);//物体3秒移动到（1，1，1）

            StartCoroutine(Delay(() => { sequence.Pause(); }, 0.033f));
            
            // sequence.Insert(0, transform.DOMove(-Vector3.one, 2));//覆盖掉0到2秒的时间执行物体移动到（-1，-1，-1）的动画
            // sequence.Insert(0, transform.DOScale(Vector3.one*2, 2));//0到2秒的时间执行物体缩放到（2，2，2）的动画
            // sequence.Insert(4, transform.DOMove(Vector3.one * (3), 2));
            
            
            t1.OnComplete(() => { Helper.Log("动画播放完成"); });
            t1.OnStart(() => { Helper.Log("动画开始播放"); });//只在第一次播放动画时调用，在play之前调用
            t1.OnPlay(() => { Helper.Log("动画播放时回调,暂停后重新播放也会调用"); });
            t1.OnPause(() => { Helper.Log("动画暂停播放"); });
            t1.OnRewind(() => { Helper.Log("动画重新播放"); });//——使用DORestart重新播放时、使用Rewind倒播动画完成时、使用Rewind倒播动画完成时、 使用DOPlayBackwards反向播放动画完成时       
            t1.OnKill(() => { Helper.Log("动画被销毁时回调"); });	
            t1.OnStepComplete(() => { Helper.Log("完成单个循环周期时触发"); });	
            // t1.OnUpdate(() => { Helper.Log("帧回调"); });	
        }

        IEnumerator Delay(Action action, float time)
        {
            yield return new WaitForSeconds(time);
            action.Invoke();
            
        }
    }
}