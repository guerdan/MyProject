using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script
{
    public class Timer : MonoBehaviour
    {
        public static Timer inst;

        public Dictionary<MonoBehaviour, List<Coroutine>> ComponentToCoroutines;


        void Awake()
        {
            Timer.inst = this;
        }

        public void SetTimeOnce(Action action, float time, MonoBehaviour component = null)
        {
           var cor = StartCoroutine(SetTimeOnceEnumerator(action, time));
            if (component != null)
            {
                if (!ComponentToCoroutines.ContainsKey(component))
                {
                    ComponentToCoroutines.Add(component, new List<Coroutine>());
                }

                var list = ComponentToCoroutines[component];
                list.Add(cor);
            }
        } 
        public IEnumerator SetTimeOnceEnumerator(Action action, float time)
        {
            yield return new WaitForSeconds(time);
            action.Invoke();
        }

        public void Update()
        {
            var keys = ComponentToCoroutines.Keys.ToList();
            foreach (var component in keys)
            {
                // component.isActiveAndEnabled
                // ComponentToCoroutines.Remove(component);
            }
        }
    }
}