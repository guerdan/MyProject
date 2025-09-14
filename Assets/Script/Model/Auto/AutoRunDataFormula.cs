using System;
using UnityEngine;
namespace Script.Model.Auto
{
    public partial class AutoScriptData
    {
        //Invoke可以合着写，但是要拆箱装箱，耗时加倍
        // 后续：1.检查方法的参数个数和类型是否匹配
        #region Invoke  

        public float Invoke(Func<float> method, string[] param_list)
        {
            return method();
        }
        public float Invoke(Func<float, float, float> method, string[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            return method(p0, p1);
        }

        public Vector2 Invoke(Func<float, float, Vector2> method, string[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            return method(p0, p1);
        }
        public Vector2 Invoke(Func<Vector4, Vector2> method, string[] param_list)
        {
            var p0 = ParseVector4(param_list[0]);
            return method(p0);
        }

        public Vector4 Invoke(Func<float, float, float, float, Vector4> method, string[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            var p2 = ParseFloat(param_list[2]);
            var p3 = ParseFloat(param_list[3]);
            return method(p0, p1, p2, p3);
        }

        public Vector4 Invoke(Func<Vector4> method, string[] param_list)
        {
            return method();
        }

        #endregion

        bool TryAccessField(object obj, string field_name, out object value)
        {
            var type = obj.GetType().Name;
            value = null;

            switch (type)
            {
                case "Vector2":
                    var v2 = (Vector2)obj;
                    if (field_name == "x") value = v2.x;
                    if (field_name == "y") value = v2.y;
                    break;
                case "Vector3":
                    var v3 = (Vector3)obj;
                    if (field_name == "x") value = v3.x;
                    if (field_name == "y") value = v3.y;
                    if (field_name == "z") value = v3.z;
                    break;
                case "Vector4":
                    var v4 = (Vector4)obj;
                    if (field_name == "x") value = v4.x;
                    if (field_name == "y") value = v4.y;
                    if (field_name == "z") value = v4.z;
                    if (field_name == "w") value = v4.w;
                    break;
            }

            return value != null;
        }



        #region 自定义方法


        #endregion

        #region float
        public float Add(float a, float b)
        {
            return a + b;
        }



        #endregion

        #region Vector2
        public Vector2 V2Constructor(float x, float y)
        {
            return new Vector2(x, y);
        }
        // 获取CVRect的中心点坐标。1.鼠标点击匹配结果中心
        public Vector2 GetCenter(Vector4 v4)
        {
            return new Vector2(v4.x + v4.z / 2, v4.y + v4.w / 2);
        }


        #endregion

        #region Vector4
        public Vector4 V4Constructor(float x, float y, float z, float w)
        {
            return new Vector4(x, y, z, w);
        }
        public Vector4 Screen()
        {
            return new Vector4(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height);
        }

        #endregion
    }
}
