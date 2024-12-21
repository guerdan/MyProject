using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;


namespace Script.Test
{
    public class PerformanceTestClass : MonoBehaviour
    {
        public InputField numberInput;

        private AClass aClass = new AClass();
        private int a;
        private int times = 0;
        private void Awake()
        {
            numberInput.onValueChanged.AddListener(OnNumberInputValueChanged);
        }

        private void OnNumberInputValueChanged(string value)
        {
            if (int.TryParse(value, out a))
            {
                times = a;
            }
        }

        public void DirectCall()
        {
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;
            a = b + aClass.a.a.a.a;


            // int a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
            // a = 1 + 1;
        }

        void Start()
        {

        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                DoRepeat(times);
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                DoRepeatReflect(times);
            }
        }

        private int b = 1;
        // private int c = 1;
        private void DoRepeat(int times)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < times; i++)
            {
                DirectCall();
                // int a = b + c;
            }
            stopwatch.Stop();
            Debug.Log("DoRepeat call time: " + stopwatch.ElapsedMilliseconds + " ms");
        }
        private void DoRepeatReflect(int times)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // 反射调用
            MethodInfo methodInfo = typeof(PerformanceTestClass).GetMethod("DirectCall");
            for (int i = 0; i < times; i++)
            {
                methodInfo.Invoke(this, null);
            }
            stopwatch.Stop();
            Debug.Log("DoRepeatReflect call time: " + stopwatch.ElapsedMilliseconds + " ms");
        }

        private void Reflect()
        {
            // string typeName = "Script.Test.AClass";
            // Type type = Type.GetType(typeName);
            // MethodInfo method = type.GetMethod("Print");
            // object instance = Activator.CreateInstance(type);
            // method.Invoke(instance, null);


            // Func<string, string, int> func = (s, i) => 1;
            // Delegate func1 = func;
            // var k = func1.DynamicInvoke(new object[] { "a", "s" });
            // Debug.LogWarning(k);
        }

        public static void SimpleFunction()
        {
            int a = 1;
            int b = 2;
            int c = a + b;
        }
    }


    public class AClass
    {
        public BClass a = new BClass();
        public virtual void Print()
        {
            Debug.LogWarning("AClass");

        }
    }


    public class BClass
    {

        public CClass a = new CClass();

        public void Print()
        {
            Debug.LogWarning("BClass");
        }
    }

    public class CClass 
    {

        public DClass a = new DClass();

        public void Print()
        {
            Debug.LogWarning("BClass");
        }
    }

    public class DClass 
    {

        public int a = 1;

        public void Print()
        {
            Debug.LogWarning("BClass");
        }
    }


    public delegate void NotifyEventHandler(string message); // 定义委托类型

    public class Publisher
    {
        // 定义事件
        public NotifyEventHandler Notify;

        // 触发事件
        public void TriggerEvent()
        {
            if (Notify != null)
            {
                Notify("事件已触发！");
            }
        }
    }

    public class Subscriber
    {
        public void OnNotify(string message)
        {
            Debug.Log("收到通知: " + message);
        }
    }

    class Program
    {
        public static void Main()
        {
            Publisher publisher = new Publisher();
            Subscriber subscriber = new Subscriber();

            // 订阅事件
            publisher.Notify += subscriber.OnNotify;

            // 触发事件
            publisher.TriggerEvent();

            // 取消订阅事件
            publisher.Notify -= subscriber.OnNotify;
        }
    }
}