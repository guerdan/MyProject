using System;
using System.Collections;
using System.Collections.Generic;
using Script.Framework.AssetLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using XLua;

namespace Script.XLua
{
    public class XLuaManager : MonoBehaviour
    {

        public static XLuaManager Inst;
        private readonly string LuaSummaryAddress = "Other/LuaSummary";
        /// <summary>
        /// 脚本缓存字典
        /// </summary>
        public Dictionary<string, byte[]> LuaTextByteDic = new Dictionary<string, byte[]>();

        private LuaEnv luaEnv;  //lua 环境
        private List<string> luaPaths = new List<string>();  //lua 的addressable

        void Awake()
        {
            Inst = this;
            //创建lua运行环境
            luaEnv = new LuaEnv();
            Application.targetFrameRate= 120;

            StartCoroutine(LoadLuaAddressable());
        }

        void OnDestroy()
        {
            //释放lua环境
            if (luaEnv != null) luaEnv.Dispose();
        }
        // 异步加载脚本
        private IEnumerator LoadLuaAddressable()
        {
            // todo 写个工具自动刷Lua文件下脚本的Address，并统计存到文本中。
            // 必须先加载lua统计文本，再赋值给luaPaths，再去加载lua脚本。
            bool isComplete = false;
            AssetManager.Inst.LoadAssetAsync<TextAsset>(LuaSummaryAddress, (textAsset) =>
            {
                string[] texts = textAsset.text.Split("\r\n");
                for (int i = 0; i < texts.Length; i++)
                {
                    luaPaths.Add(texts[i]);
                }
                isComplete = true;
            }, null, UnloadMode.WhenComplete);
            yield return new WaitUntil(() => isComplete);

            Debug.Log($"开始加载Lua脚本：{Time.frameCount}");
            var start = DateTime.UtcNow;
            var LuaHandle = AssetManager.Inst.LoadAssetsAsync<TextAsset>(luaPaths, (asset) =>
            {
                Debug.Log($"加载Lua脚本：" + asset.name + Time.frameCount);
            });

            yield return LuaHandle;

            if (LuaHandle.Status == AsyncOperationStatus.Succeeded)
            {
                for (int i = 0; i < LuaHandle.Result.Count; i++)
                {
                    TextAsset textAsset = LuaHandle.Result[i];
                    LuaTextByteDic.Add(luaPaths[i], textAsset.bytes);
                }
                luaEnv.AddLoader(LuaScriptLoader);
                TimeSpan st1 = DateTime.UtcNow - start;
                Debug.Log($"Lua脚本加载成功{Time.frameCount} 耗时{Convert.ToInt64(st1.TotalMilliseconds)}");
            }
            else
            {
                Debug.LogError("Lua脚本加载失败");
            }
            Addressables.Release(LuaHandle);


        }

        public byte[] LuaScriptLoader(ref string filepath)
        {
            //传入 game.init 转换成 game/init.lua.txt
            filepath = filepath + ".lua.txt";
            //通过字典获取资源
            var bytes = LuaTextByteDic[filepath];
            LuaTextByteDic.Remove(filepath);
            return bytes;
        }


        void Update()
        {
            // 实现lua热更新
            if (Input.GetKeyDown(KeyCode.A))
            {
                luaEnv.DoString("require 'Lua/Folder1/testB'");
                luaEnv.DoString("require 'Lua/Folder1/testA'");
            }

            // 实现C#热更新，原理是HotFix特性
            if (Input.GetKeyDown(KeyCode.B))
            {
                var b = new TestB();
                // b.DirectCall();
            }


            if (Input.GetKeyDown(KeyCode.C))
            {
                // 没用LuaCallCSharp是260ms
                LuaFunction luaFunction = luaEnv.Global.Get<LuaFunction>("TestCallEmpty");
                // 用LuaCallCSharp是180ms
                TestCall testCall = luaEnv.Global.Get<TestCall>("TestCallEmpty");

                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                for (int i = 0; i < 1000000; i++)
                {
                    // luaFunction.Call();
                    testCall?.Invoke();
                    // Test();
                }
                stopwatch.Stop();

                Debug.Log("LuaFunction 调用时间：" + stopwatch.ElapsedMilliseconds);
            }

            //测试lua代码优化效率
            if (Input.GetKeyDown(KeyCode.D))
            {
                luaEnv.Global.Get("TestCall", out Action action);
                action.Invoke();
            }


        }


        void Test()
        {

        }
    }


    [LuaCallCSharp]
    public class TestB
    {
        public TestC testC;
        private void Awake()
        {
            Debug.LogWarning("Awake");
        }

        [Hotfix]
        public void DirectCall(TestC testC)
        {

        }
        public void DirectCallM()
        {

        }
    }
    public class TestC
    {

        private void Awake()
        {
            Debug.LogWarning("Awake");
        }

    }

    [CSharpCallLua]
    public delegate void TestCall();
}