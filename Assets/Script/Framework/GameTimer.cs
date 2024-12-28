using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script.Framework
{
    //定时器
    public interface IGameTimer
    {
        /// <summary>
        /// 同个对象的同个方法只能有一个定时器，重复添加则覆盖
        /// </summary>
        void SetTimeOnce(object obj, Action action, float delay);
        /// <summary>
        /// 取消定时器
        /// </summary>
        void CancelTimer(object obj, Action action);
        /// <summary>
        /// 取消与对象关联的所有定时器
        /// </summary>
        void CancelAllTimer(object obj);
    }

    public class GameTimer : MonoBehaviour, IGameTimer
    {
        public static IGameTimer Inst;

        private Dictionary<object, Dictionary<Action, Coroutine>> objDic;

        void Awake()
        {
            GameTimer.Inst = this;
            objDic = new Dictionary<object, Dictionary<Action, Coroutine>>();
        }

        public void SetTimeOnce(object obj, Action action, float delay)
        {
            if (obj == null || action == null) return;

            var cor = StartCoroutine(SetTimeOnceCor(obj, action, delay));
            if (!objDic.TryGetValue(obj, out var actionDic))
            {
                actionDic = new Dictionary<Action, Coroutine>();
                objDic.Add(obj, actionDic);
            }

            //重复添加则覆盖
            if (actionDic.TryGetValue(action, out var old))
                StopCoroutine(old);

            actionDic[action] = cor;
        }

        public void CancelTimer(object obj, Action action)
        {
            if (obj == null || action == null) return;

            if (objDic.TryGetValue(obj, out var actionDic))
            {
                if (actionDic.TryGetValue(action, out var cor))
                {
                    StopCoroutine(cor);
                    DeleteCor(obj, action);
                }
            }
        }
        public void CancelAllTimer(object obj)
        {
            if (obj == null) return;

            if (objDic.TryGetValue(obj, out var actionDic))
            {
                objDic.Remove(obj);
                foreach (var cor in actionDic.Values)
                {
                    StopCoroutine(cor);
                }
            }
        }
        private IEnumerator SetTimeOnceCor(object obj, Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action.Invoke();
            DeleteCor(obj, action);
        }

        private void DeleteCor(object obj, Action action)
        {
            if (objDic.TryGetValue(obj, out var actionDic))
            {
                if (actionDic.TryGetValue(action, out var cor))
                {
                    actionDic.Remove(action);
                    if (actionDic.Count == 0)
                        objDic.Remove(obj);
                }
            }
        }

    }
}