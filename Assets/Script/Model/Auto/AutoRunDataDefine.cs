using System;
using System.Collections.Generic;
using UnityEngine;
namespace Script.Model.Auto
{
    public partial class AutoScriptData
    {
        Dictionary<string, CompareParse> _compareParseCache = new Dictionary<string, CompareParse>();

        CompareParse ParseCompareInfo(string str)
        {
            CompareParse parse = default;

            str = str.Replace(" ", "");
            char comparison1 = str[4];
            char comparison2 = str[5];
            if (comparison1 == '>' && comparison2 == '=')
            {
                parse.CompareType = CompareType.GreaterOrEqual;
                parse.Value = float.Parse(str.Substring(6));
            }
            else if (comparison1 == '<' && comparison2 == '=')
            {
                parse.CompareType = CompareType.SmallerOrEqual;
                parse.Value = float.Parse(str.Substring(6));
            }
            else if (comparison1 == '=' && comparison2 == '=')
            {
                parse.CompareType = CompareType.Equal;
                parse.Value = float.Parse(str.Substring(6));
            }
            else if (comparison1 == '>')
            {
                parse.CompareType = CompareType.Greater;
                parse.Value = float.Parse(str.Substring(5));
            }
            else if (comparison1 == '<')
            {
                parse.CompareType = CompareType.Smaller;
                parse.Value = float.Parse(str.Substring(5));
            }
            return parse;
        }
        //Invoke可以合着写，但是要拆箱装箱，耗时加倍
        // 后续：1.检查方法的参数个数和类型是否匹配
        #region Invoke  

        public float Invoke(Func<float> method, RPN_TokenInfo[] param_list)
        {
            return method();
        }
        public float Invoke(Func<float, float, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            return method(p0, p1);
        }

        public Vector2 Invoke(Func<float, float, Vector2> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            return method(p0, p1);
        }
        public Vector2 Invoke(Func<Vector4, Vector2> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseVector4(param_list[0].RawStr);
            return method(p0);
        }

        public Vector4 Invoke(Func<float, float, float, float, Vector4> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            var p1 = ParseFloat(param_list[1]);
            var p2 = ParseFloat(param_list[2]);
            var p3 = ParseFloat(param_list[3]);
            return method(p0, p1, p2, p3);
        }

        public Vector4 Invoke(Func<Vector4> method, RPN_TokenInfo[] param_list)
        {
            return method();
        }

        public float[] Invoke(Func<float, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            return method(p0);
        }
        public float[] Invoke(Func<float[], string, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultFL(param_list[0].RawStr);
            var p1 = param_list[1].Param.Str;
            return method(p0, p1);
        }
        public Vector2[] Invoke(Func<float, Vector2[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            return method(p0);
        }
        public Vector4[] Invoke(Func<float, Vector4[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            return method(p0);
        }

        #endregion

        bool TryAccessField(FormulaVarInfo_Cal obj, string field_name, out FormulaVarInfo_Cal result)
        {
            result = default;
            result.Type = FormulaVarType.Float;

            switch (obj.Type)
            {
                case FormulaVarType.Vector2:
                    var v2 = obj.V2;
                    if (field_name == "x") result.F = v2.x;
                    if (field_name == "y") result.F = v2.y;
                    break;
                case FormulaVarType.Vector4:
                    var v4 = obj.V4;
                    if (field_name == "x") result.F = v4.x;
                    if (field_name == "y") result.F = v4.y;
                    if (field_name == "z") result.F = v4.z;
                    if (field_name == "w") result.F = v4.w;
                    break;
                case FormulaVarType.ListFloat:
                    var fl = obj.ListF;
                    if (field_name == "length") result.F = fl.Length;
                    break;
                case FormulaVarType.ListVector2:
                    var v2l = obj.ListV2;
                    if (field_name == "length") result.F = v2l.Length;
                    break;
                case FormulaVarType.ListVector4:
                    var v4l = obj.ListV4;
                    if (field_name == "length") result.F = v4l.Length;
                    break;
            }

            return result.Type != FormulaVarType.Undefined;
        }


        #region Float
        public float Add(float a, float b)
        {
            return a + b;
        }


        #endregion

        #region Vector2
        public Vector2 V2_Constructor(float x, float y)
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
        public Vector4 V4_Constructor(float x, float y, float z, float w)
        {
            return new Vector4(x, y, z, w);
        }
        public Vector4 Screen()
        {
            return new Vector4(0, 0, UnityEngine.Screen.width, UnityEngine.Screen.height);
        }

        #endregion

        #region FloatList
        public float[] FL_Constructor(float a)
        {
            return new float[(int)a];
        }

        /// <summary>
        /// str: "self>10"
        /// </summary>
        public float[] FL_FindIndex(float[] l, string str)
        {
            if (!_compareParseCache.TryGetValue(str, out CompareParse parse))
            {
                parse = ParseCompareInfo(str);
                _compareParseCache[str] = parse;
            }

            List<float> result = new List<float>();
            switch (parse.CompareType)
            {
                case CompareType.Equal:
                    for (int i = 0; i < l.Length; i++)
                        if (l[i] == parse.Value)
                            result.Add(i);
                    break;
                case CompareType.Greater:
                    for (int i = 0; i < l.Length; i++)
                        if (l[i] > parse.Value)
                            result.Add(i);
                    break;
                case CompareType.GreaterOrEqual:
                    for (int i = 0; i < l.Length; i++)
                        if (l[i] >= parse.Value)
                            result.Add(i);
                    break;
                case CompareType.Smaller:
                    for (int i = 0; i < l.Length; i++)
                        if (l[i] < parse.Value)
                            result.Add(i);
                    break;
                case CompareType.SmallerOrEqual:
                    for (int i = 0; i < l.Length; i++)
                        if (l[i] <= parse.Value)
                            result.Add(i);
                    break;
            }


            return result.ToArray();
        }

        #endregion

        #region Vector2List
        public Vector2[] V2L_Constructor(float a)
        {
            return new Vector2[(int)a];
        }
        public Vector2[] V2L_Find(Vector2[] l)
        {
            return new Vector2[10];
        }

        #endregion


        #region Vector4List
        public Vector4[] V4L_Constructor(float a)
        {
            return new Vector4[(int)a];
        }
        public Vector4[] V4L_Find(Vector4[] l)
        {
            return new Vector4[10];
        }

        #endregion

        struct CompareParse
        {
            public int FieldIndex;
            public CompareType CompareType;
            public float Value;
        }

        public enum CompareType
        {
            Equal,                  // 等于
            Greater,                // 大于
            GreaterOrEqual,         // 大于等于
            Smaller,                // 小于
            SmallerOrEqual,         // 小于等于
        }

    }

    #region 解析
    public static class MethodParseUtil
    {
        static Dictionary<string, MethodID> Dic;

        static void InitDic()
        {
            Dic = new Dictionary<string, MethodID>();
            Dic.Add("Add", MethodID.F_Add);
            Dic.Add("V2", MethodID.V2_Constructor);
            Dic.Add("GetCenter", MethodID.V2_GetCenter);
            Dic.Add("V4", MethodID.V4_Constructor);
            Dic.Add("Screen", MethodID.V4_Screen);
            Dic.Add("FL_New", MethodID.FL_Constructor);
            Dic.Add("FL_Find", MethodID.FL_Find);
            Dic.Add("V2L_New", MethodID.V2L_Constructor);
            Dic.Add("V4L_New", MethodID.V4L_Constructor);
        }

        public static MethodID ParseMethod(string method_name)
        {
            if (Dic == null)
                InitDic();

            Dic.TryGetValue(method_name, out var result);
            return result;
        }


    }


    #endregion

    #region 枚举

    /// <summary>
    /// 定义Method的ID, "整型比较"要快于"字符串比较"
    /// </summary>
    public enum MethodID
    {
        Undefined,

        #region F
        F_Add,

        #endregion

        #region V2
        V2_Constructor,
        V2_GetCenter,
        #endregion


        #region V4
        V4_Constructor,
        V4_Screen,
        #endregion

        #region FL
        FL_Constructor,
        FL_Find,
        #endregion


        #region V2L
        V2L_Constructor,
        V2L_Find,
        #endregion


        #region V4L
        V4L_Constructor,
        V4L_Find,
        #endregion
    }

    #endregion

}
