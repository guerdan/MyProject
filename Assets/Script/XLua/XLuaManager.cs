using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using XLua;

namespace Script.XLua
{
    public class XLuaManager : MonoBehaviour
    {

        public static XLuaManager Inst;
        /// <summary>
        /// 脚本缓存字典
        /// </summary>
        public Dictionary<string, TextAsset> LuaTextAssetsDic = new Dictionary<string, TextAsset>();

        private LuaEnv luaEnv;  //lua 环境
        private List<string> luaPaths;  //lua 的addressable

        void Awake()
        {
            Inst = this;
            //创建lua运行环境
            luaEnv = new LuaEnv();

            luaEnv.DoString("", "chunk", null);

            luaPaths = new List<string>(){
                "Lua/Folder1/testA.lua.txt",
                "Lua/Folder1/testB.lua.txt",
            };
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
            var LuaHandle = Addressables.LoadAssetsAsync<TextAsset>(luaPaths, null, Addressables.MergeMode.Union);
            LuaHandle.Completed += (handle) =>
            {
                for (int i = 0; i < handle.Result.Count; i++)
                {
                    TextAsset textAsset = handle.Result[i];
                    LuaTextAssetsDic.Add(luaPaths[i], textAsset);
                }
            };

            yield return LuaHandle;

            if (LuaHandle.Status == AsyncOperationStatus.Succeeded)
            {
                luaEnv.AddLoader(LuaScriptLoader);
                Debug.Log("Lua脚本加载成功");
            }
            else
            {
                Debug.LogError("Lua脚本加载失败");
            }

            Addressables.Release(LuaHandle);
        }

        public byte[] LuaScriptLoader(ref string filepath)
        {
            //传入 game.init 转换成 game/inin.lua.txt
            filepath = filepath + ".lua.txt";
            //通过字典获取资源
            TextAsset result = LuaTextAssetsDic[filepath];
            return result.bytes;
        }


        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                luaEnv.DoString("require 'Lua/Folder1/testB'");
                luaEnv.DoString("require 'Lua/Folder1/testA'");

                // 获取 Lua 函数
                // LuaFunction luaFunction = luaEnv.Global.Get<LuaFunction>("LuaFunctionExample");
                // luaFunction.Call(10);


                luaEnv.Global.Get("TestCall", out Action action);
                action.Invoke();
            }
        }






    }


    [LuaCallCSharp]
    public class TestA
    {
        public int a = 42;

        private void Awake()
        {
            Debug.LogWarning("Awake");


        }

        public void DirectCall()
        {
        }
    }


    [LuaCallCSharp]
    public class TestB
    {
        public TestA a;

        private void Awake()
        {
            Debug.LogWarning("Awake");
        }

        [Hotfix]
        public void DirectCall()
        {
            Debug.LogWarning("DirectCall");
        }


    }
}