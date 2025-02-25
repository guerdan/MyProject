
using System;
using UnityEngine;

namespace Script.Util
{
    // 禁用可序列化字段在Inspector面板的编辑
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class EditDisabledAttribute : PropertyAttribute { }
}