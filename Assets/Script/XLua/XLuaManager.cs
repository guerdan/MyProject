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

        static XLuaManager Instance;
        /// <summary>
        /// 脚本缓存字典
        /// </summary>
        public Dictionary<string, TextAsset> LuaTextAssetsDic = new Dictionary<string, TextAsset>();



        private LuaEnv luaEnv;  //lua 环境
        private List<string> luaPaths;  //lua 环境

        void Awake()
        {
            Instance = this;
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
            luaEnv.Dispose();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                luaEnv.DoString("require 'Lua/Folder1/testA'");
            }
        }



        private IEnumerator LoadLuaAddressable()
        {
            var LuaHandle = Addressables.LoadAssetsAsync<TextAsset>(luaPaths, null, Addressables.MergeMode.Union);
LuaHandle.WaitForCompletion();
            LuaHandle.Completed += (handle) =>
            {
                for (int i = 0; i < handle.Result.Count; i++)
                {
                    TextAsset textAsset = handle.Result[i];
                    XLuaManager.Instance.LuaTextAssetsDic.Add(luaPaths[i], textAsset);
                }
            };

            yield return LuaHandle;

            if (LuaHandle.Status == AsyncOperationStatus.Succeeded)
            {
                luaEnv.AddLoader(XLuaManager.Instance.LuaScriptLoader);
                Debug.Log("Lua脚本加载成功");
            }
            else
            {
                Debug.LogError("Lua脚本加载失败");
            }

        }


        public byte[] LuaScriptLoader(ref string filepath)
        {
            //传入 game.init 转换成 game/inin.lua.txt
            filepath = filepath + ".lua.txt";
            //通过字典获取资源
            TextAsset result = LuaTextAssetsDic[filepath];
            return result.bytes;

        }

    }


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
}