using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Debug = UnityEngine.Debug;


namespace Script.Test
{
    public class TestClass : MonoBehaviour
    {

        public int a;
        private void Awake()
        {
            Debug.LogWarning("Awake");
        }

        public void DirectCall()
        {


            
        }

        void Start()
        {
            // AClass B = new BClass();
            // BClass B1 = B as BClass;
            // B.Print();
            // B1.Print();

            // Program.Main();

            // string typeName = "Script.Test.AClass";
            // Type type = Type.GetType(typeName);
            // MethodInfo method = type.GetMethod("Print");
            // object instance = Activator.CreateInstance(type);
            // method.Invoke(instance, null);


            // ArrayList arrayList = new ArrayList();
            // arrayList.Add(1);


            // Func<string, string, int> func = (s, i) => 1;
            // Delegate func1 = func;
            // var k = func1.DynamicInvoke(new object[] { "a", "s" });
            // Debug.LogWarning(k);


            int iterations = 1000000;

            // 直接调用Stopwatch
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                // DirectCall();

            }
            stopwatch.Stop();
            Debug.Log("Direct call time: " + stopwatch.ElapsedMilliseconds + " ms");

            // 反射调用
            MethodInfo methodInfo = typeof(TestClass).GetMethod("DirectCall");
            stopwatch.Reset();
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
            {
                methodInfo.Invoke(this,null);
            }
            stopwatch.Stop();
            Debug.Log("Reflection call time: " + stopwatch.ElapsedMilliseconds + " ms");

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
        public virtual void Print()
        {
            Debug.LogWarning("AClass");
        }
    }


    public class BClass : AClass
    {
        public new void Print()
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