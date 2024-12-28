using System;
using System.Collections.Generic;
using XLua;

namespace Script.Editor
{
    public static class HotfixCfg
    {

        //静态列表，手动添加
        [Hotfix]
        public static List<Type> mymodule_lua_call_cs_list = new List<Type>()
        {
            typeof(Script.XLua.TestB),
        };

        //动态列表，自动将某程序集下某命名空间下的类都添加进来。
        // [Hotfix]
        // public static List<Type> by_property
        // {
        //     get
        //     {
        //         return (from type in Assembly.Load("Assembly-CSharp").GetTypes()
        //                 where type.Namespace == "Script.XLua"
        //                 select type).ToList();
        //     }
        // }


    }
}