using System;
using System.Collections.Generic;
using OpenCvSharp;
using Script.Framework.Else;
using Script.Util;
using UnityEngine;
namespace Script.Model.Auto
{
    public partial class AutoScriptData
    {
        // 只要节点不是无穷多就行。 str_info.Param.Int会带上
        List<CompareParse> _compareParseCache = new List<CompareParse>();


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
        public float Invoke(Func<Vector2, Vector2, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV2(param_list[0].RawStr);
            var p1 = FormulaGetResultV2(param_list[1].RawStr);
            return method(p0, p1);
        }

        public float Invoke(Func<float[], float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultFL(param_list[0].RawStr);
            return method(p0);
        }
        public float Invoke(Func<Vector2[], RPN_TokenInfo, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV2L(param_list[0].RawStr);
            var p1 = param_list[1];
            return method(p0, p1);
        }
        public float Invoke(Func<Vector4, Vector4, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV4(param_list[0].RawStr);
            var p1 = FormulaGetResultV4(param_list[1].RawStr);
            return method(p0, p1);
        }
        public float Invoke(Func<Vector4, Vector4, RPN_TokenInfo, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV4(param_list[0].RawStr);
            var p1 = FormulaGetResultV4(param_list[1].RawStr);
            var p2 = param_list[2];
            return method(p0, p1, p2);
        }
        public float Invoke(Func<string[], HashSet<string>, float> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultSL(param_list[0].RawStr);
            var p1 = FormulaGetResultSHashSet(param_list[1].RawStr);
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
            var p0 = FormulaGetResultV4(param_list[0].RawStr);
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
        public float[] Invoke(Func<float[], RPN_TokenInfo, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultFL(param_list[0].RawStr);
            var p1 = param_list[1];
            return method(p0, p1);
        }
        public float[] Invoke(Func<Vector2[], RPN_TokenInfo, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV2L(param_list[0].RawStr);
            var p1 = param_list[1];
            return method(p0, p1);
        }
        public float[] Invoke(Func<Vector4[], RPN_TokenInfo, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV4L(param_list[0].RawStr);
            var p1 = param_list[1];
            return method(p0, p1);
        }

        public float[] Invoke(Func<float[], float, float[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultFL(param_list[0].RawStr);
            var p1 = ParseFloat(param_list[1]);
            return method(p0, p1);
        }
        public Vector2[] Invoke(Func<float, Vector2[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            return method(p0);
        }

        public Vector2[] Invoke(Func<Vector2[], Vector2, Vector2[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV2L(param_list[0].RawStr);
            var p1 = FormulaGetResultV2(param_list[1].RawStr);
            return method(p0, p1);
        }


        public Vector4[] Invoke(Func<float, Vector4[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = ParseFloat(param_list[0]);
            return method(p0);
        }
        public Vector4[] Invoke(Func<Vector2[], Vector4[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV2L(param_list[0].RawStr);
            return method(p0);
        }
        public Vector4[] Invoke(Func<Vector4[], Vector4[], Vector4[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV4L(param_list[0].RawStr);
            var p1 = FormulaGetResultV4L(param_list[1].RawStr);
            return method(p0, p1);
        }
        public Vector4[] Invoke(Func<Vector4[], Vector4, Vector4[]> method, RPN_TokenInfo[] param_list)
        {
            var p0 = FormulaGetResultV4L(param_list[0].RawStr);
            var p1 = FormulaGetResultV4(param_list[1].RawStr);
            return method(p0, p1);
        }


        public string[] Invoke(Func<float, string[]> method, RPN_TokenInfo[] param_list)
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
        public float GetDistance(Vector2 a, Vector2 b)
        {
            return (a - b).magnitude;
        }

        public float EqualV2(Vector2 a, Vector2 b)
        {
            bool compare = a.x == b.x && a.y == b.y;
            int result = compare ? 1 : 0;
            return result;
        }
        public float EqualV4(Vector4 a, Vector4 b)
        {
            bool compare = a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
            int result = compare ? 1 : 0;
            return result;
        }
        public float CompareCol(Vector4 a, Vector4 b, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);

            bool compare = false;
            switch (parse.CompareType)
            {
                case CompareType.Equal: compare = a.x == b.x && a.y == b.y && a.z == b.z; break;
                case CompareType.NotEqual: compare = a.x != b.x || a.y != b.y || a.z != b.z; break;
                case CompareType.Greater: compare = a.x > b.x && a.y > b.y && a.z > b.z; break;
                case CompareType.GreaterOrEqual: compare = a.x >= b.x && a.y >= b.y && a.z >= b.z; break;
                case CompareType.Smaller: compare = a.x < b.x && a.y < b.y && a.z < b.z; break;
                case CompareType.SmallerOrEqual: compare = a.x <= b.x && a.y <= b.y && a.z <= b.z; break;
            }

            int result = compare ? 1 : 0;
            return result;
        }

        float FindMax(float[] l)
        {
            float max_value = 0;
            float max_index = -1;
            for (int i = 0; i < l.Length; i++)
            {
                var value = l[i];
                if (max_index == -1 || value > max_value)
                {
                    max_index = i;
                    max_value = value;
                }
            }

            return max_index;
        }

        float FindMin(float[] l)
        {
            float min_value = 0;
            float min_index = -1;
            for (int i = 0; i < l.Length; i++)
            {
                var value = l[i];
                if (min_index == -1 || value < min_value)
                {
                    min_index = i;
                    min_value = value;
                }
            }

            return min_index;
        }
        /// <summary>
        /// str: "x"|"y"|"x^2+y^2"
        /// </summary>
        float FindMinV2L(Vector2[] l, RPN_TokenInfo str_info)
        {
            var convert = ConvertV2L(l, str_info);
            float min_index = FindMin(convert);
            return min_index;
        }

        float FindMaxV2L(Vector2[] l, RPN_TokenInfo str_info)
        {
            var convert = ConvertV2L(l, str_info);
            float max_index = FindMax(convert);
            return max_index;
        }

        float ContainCountSL(string[] source, HashSet<string> set)
        {
            var count = 0;
            foreach (var str in source)
            {
                if (set.Contains(str))
                    count++;
            }
            return count;
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
            return new Vector4(0, Utils.ScreenHeight, Utils.ScreenWidth, Utils.ScreenHeight);
        }

        #endregion

        #region Float[]

        public static float[] FL_Add(float[] a, float[] b)
        {
            float[] result = new float[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
        public float[] FL_Constructor(float a)
        {
            return new float[(int)a];
        }


        /// <summary>
        /// str: "self>10"
        /// </summary>
        public float[] FilterFL(float[] l, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);
            float[] result = FilterFL(l, parse);
            return result;
        }

        /// <summary>
        /// str支持一次比较操作："x>10"/"y>10"
        /// </summary>
        public float[] FilterV2L(Vector2[] l, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);
            float[] convert = ConvertV2L(l, parse.leftType);
            float[] result = FilterFL(convert, parse);
            return result;
        }


        /// <summary>
        /// str支持一次比较操作："x>10"/"y>10"/"z>10"/"w>10"
        /// </summary>
        public float[] FilterV4L(Vector4[] l, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);
            float[] convert = ConvertV4L(l, parse.leftType);
            float[] result = FilterFL(convert, parse);
            return result;
        }

        /// <summary>
        /// str支持："x"/"y"/"x*x+y*y"
        /// </summary>
        float[] ConvertV2L(Vector2[] l, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);
            return ConvertV2L(l, parse.leftType);
        }
        /// <summary>
        /// str支持："x"/"y"/"z"/"w"
        /// </summary>
        float[] ConvertV4L(Vector4[] l, RPN_TokenInfo str_info)
        {
            CompareParse parse = GetCompareParse(str_info);
            return ConvertV4L(l, parse.leftType);
        }

        float[] EachAddFL(float[] l, float n)
        {
            var result = new float[l.Length];
            for (int i = 0; i < l.Length; i++)
                result[i] = l[i] + n;
            return result;
        }

        public float[] FilterFL(float[] l, CompareParse parse)
        {
            List<float> result = new List<float>();
            var value = FormulaGetResult(parse.ValueInfo);

            switch (parse.CompareType)
            {
                case CompareType.Equal:
                    for (int i = 0; i < l.Length; i++) if (l[i] == value) result.Add(i); break;
                case CompareType.NotEqual:
                    for (int i = 0; i < l.Length; i++) if (l[i] != value) result.Add(i); break;
                case CompareType.Greater:
                    for (int i = 0; i < l.Length; i++) if (l[i] > value) result.Add(i); break;
                case CompareType.GreaterOrEqual:
                    for (int i = 0; i < l.Length; i++) if (l[i] >= value) result.Add(i); break;
                case CompareType.Smaller:
                    for (int i = 0; i < l.Length; i++) if (l[i] < value) result.Add(i); break;
                case CompareType.SmallerOrEqual:
                    for (int i = 0; i < l.Length; i++) if (l[i] <= value) result.Add(i); break;
            }

            return result.ToArray();
        }


        float[] ConvertV2L(Vector2[] l, ConvertType type)
        {
            var result = new float[l.Length];

            switch (type)
            {
                case ConvertType.X:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].x; break;
                case ConvertType.Y:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].y; break;
                case ConvertType.X2AndY2:
                    for (int i = 0; i < l.Length; i++)
                    {
                        var v2 = l[i];
                        result[i] = v2.x * v2.x + v2.y * v2.y;
                    }
                    break;
            }

            return result;
        }

        float[] ConvertV4L(Vector4[] l, ConvertType type)
        {
            var result = new float[l.Length];

            switch (type)
            {
                case ConvertType.X:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].x; break;
                case ConvertType.Y:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].y; break;
                case ConvertType.Z:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].z; break;
                case ConvertType.W:
                    for (int i = 0; i < l.Length; i++) result[i] = l[i].w; break;
            }

            return result;
        }





        #endregion

        #region Vector2[]
        public static Vector2[] V2L_Add(Vector2[] a, Vector2[] b)
        {
            Vector2[] result = new Vector2[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
        public Vector2[] V2L_Constructor(float a)
        {
            return new Vector2[(int)a];
        }

        Vector2[] EachAddV2L(Vector2[] l, Vector2 n)
        {
            var result = new Vector2[l.Length];
            for (int i = 0; i < l.Length; i++)
                result[i] = l[i] + n;
            return result;
        }

        #endregion


        #region Vector4[]
        public static Vector4[] V4L_Add(Vector4[] a, Vector4[] b)
        {
            Vector4[] result = new Vector4[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        public Vector4[] V4L_Constructor(float a)
        {
            return new Vector4[(int)a];
        }

        public Vector4[] GetScreenCols(Vector2[] poss)
        {
            Vector4[] result = new Vector4[poss.Length];

            CVRect rect = Manager.FrameCaptureRegion;
            Vec4b[] colors = Manager.FrameCaptureColor;
            int xs = rect.x, ys = rect.y, w = rect.w, h = rect.h;

            for (int i = 0; i < poss.Length; i++)
            {
                var spos = poss[i];
                var y = (int)spos.y - ys;
                var x = (int)spos.x - xs;
                int index = (h - 1 - y) * w + x;
                if (index < 0 || index >= colors.Length)
                {
                    throw new Exception("GetScreenCols() Error");
                }
                Vec4b color = colors[index];
                result[i] = new Vector4(color.Item2, color.Item1, color.Item0, color.Item3);
            }

            return result;
        }
        Vector4[] EachAddV4L(Vector4[] l, Vector4 n)
        {
            var result = new Vector4[l.Length];
            for (int i = 0; i < l.Length; i++)
                result[i] = l[i] + n;
            return result;
        }

        #endregion

        #region String[]
        public string[] SL_Constructor(float a)
        {
            return new string[(int)a];
        }

        #endregion

        #region 解析 


        /// <summary>
        /// 只支持一次比较操作。 若存在"&&"/"||"，就用for编辑吧
        /// </summary>
        public struct CompareParse
        {
            public ConvertType leftType;
            public CompareType CompareType;
            public RPN_TokenInfo[] ValueInfo;
        }

        public enum CompareType
        {
            Equal,                  // 等于
            NotEqual,               // 不等于
            Greater,                // 大于
            GreaterOrEqual,         // 大于等于
            Smaller,                // 小于
            SmallerOrEqual,         // 小于等于
        }

        public enum ConvertType
        {
            Self,
            X,
            Y,
            Z,
            W,
            X2AndY2,
        }


        CompareParse GetCompareParse(RPN_TokenInfo str_info)
        {
            var parse_index = str_info.Param.Int;
            if (parse_index < 0)
            {
                var parse = ParseCompareInfo(str_info.Param.Str);

                str_info.Param.Int = _compareParseCache.Count;
                _compareParseCache.Add(parse);
                return parse;
            }
            else
            {
                var parse = _compareParseCache[parse_index];
                return parse;
            }

        }


        CompareParse ParseCompareInfo(string formula)
        {
            CompareParse parse = default;

            // formula = formula.Replace(" ", "");      // UI层已经做了工作
            var matches = RPNCalculator.conditionRegex.Matches(formula);
            if (matches.Count != 1)
            {
                parse.leftType = ParseFieldIndex(formula);
                return parse;
            }

            var oper = matches[0].Value;
            var index = formula.IndexOf(oper);
            var left = formula.Substring(0, index);
            var right = formula.Substring(index + oper.Length);

            CompareType operType = CompareType.GreaterOrEqual;
            switch (oper)
            {
                case "==": operType = CompareType.Equal; break;
                case "!=": operType = CompareType.NotEqual; break;
                case ">": operType = CompareType.Greater; break;
                case ">=": operType = CompareType.GreaterOrEqual; break;
                case "<": operType = CompareType.Smaller; break;
                case "<=": operType = CompareType.SmallerOrEqual; break;
            }

            if (left != "")
                parse.leftType = ParseFieldIndex(left);
            parse.CompareType = operType;
            parse.ValueInfo = RPNCalculator.InfixToRPN(right);

            return parse;
        }

        ConvertType ParseFieldIndex(string str)
        {
            ConvertType field_index = 0;
            switch (str)
            {
                case "self": field_index = ConvertType.Self; break;
                case "x": field_index = ConvertType.X; break;
                case "y": field_index = ConvertType.Y; break;
                case "z": field_index = ConvertType.Z; break;
                case "w": field_index = ConvertType.W; break;
                case "x^2+y^2": field_index = ConvertType.X2AndY2; break;
                default: throw new Exception("ParseFieldIndex() Error");
            }
            return field_index;
        }

        #endregion

    }

    #region 解析 Method
    public static class MethodParseUtil
    {
        static Dictionary<string, MethodID> Dic;

        // item1-原字符串 | item2-小写
        public static List<TipMatchItem> MethodNameList;

        // 这些方法可以做个提示，当光标移动到提示词时，自动出现文本解释说明。
        public static void InitDic()
        {
            Dic = new Dictionary<string, MethodID>()
            {
                {"Add", MethodID.Add},
                {"GetDistance", MethodID.GetDistance},
                {"EqualV2", MethodID.EqualV2},
                {"EqualV4", MethodID.EqualV4},
                {"CompareCol", MethodID.CompareCol},        // 将A的x,y,z与B比较
                {"FindMax", MethodID.FindMax},              // 找"某条件"最大的一个元素，返回索引
                {"FindMin", MethodID.FindMin},              // 找"某条件"最小的一个元素，返回索引。
                {"FindMaxV2L", MethodID.FindMaxV2L},        // 
                {"FindMinV2L", MethodID.FindMinV2L},        // 可支持"x*x+y*y"
                {"ContainNumSL", MethodID.ContainNumSL},    // 集合A包含多少个集合B的元素
                {"V2", MethodID.V2_New},
                {"GetCenter", MethodID.V2_GetCenter},
                {"V4", MethodID.V4_New},
                {"Screen", MethodID.V4_Screen},
                {"FL_New", MethodID.FL_New},
                {"FilterFL", MethodID.FL_FilterFL},             // 筛选全部符合条件的元素，返回索引数组
                {"FilterV2L", MethodID.FL_FilterV2L},           // 例 "x>=2"
                {"FilterV4L", MethodID.FL_FilterV4L},           // 例 "x>=2"
                {"ConvertV2L", MethodID.FL_ConvertV2L},         // 降维转换成float[]
                {"ConvertV4L", MethodID.FL_ConvertV4L},         // 降维转换成float[]
                {"EachAddFL", MethodID.FL_EachAddFL},           // 每个元素+上

                {"V2L_New", MethodID.V2L_New},
                {"EachAddV2L", MethodID.V2L_EachAddV2L},        // 每个元素+上

                {"V4L_New", MethodID.V4L_New},
                {"V4L_Add", MethodID.V4L_Add},                  // 拼接两个列表, 可以直接用"+"
                {"GetScreenCols", MethodID.V4L_GetScreenCols},
                {"EachAddV4L", MethodID.V4L_EachAddV4L},        // 每个元素+上
                
                {"SL_New", MethodID.SL_New},
            };

            MethodNameList = new List<TipMatchItem>();
            foreach (var key in Dic.Keys)
            {
                string lower = key;
                if (key.Length > 1)
                    lower = key.Substring(0, 1) + key.Substring(1, key.Length - 1).ToLower();
                MethodNameList.Add(new TipMatchItem(key, lower, 1));
            }
        }

        public static MethodID ParseMethod(string method_name)
        {
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

        Add,
        GetDistance,
        EqualV2,
        EqualV4,
        CompareCol,
        FindMax,
        FindMin,
        FindMaxV2L,
        FindMinV2L,
        ContainNumSL,


        V2_New,
        V2_GetCenter,


        V4_New,
        V4_Screen,

        FL_New,
        FL_FilterFL,
        FL_FilterV2L,
        FL_FilterV4L,
        FL_ConvertV2L,
        FL_ConvertV4L,
        FL_EachAddFL,



        V2L_New,
        V2L_EachAddV2L,


        V4L_New,
        V4L_GetScreenCols,
        V4L_EachAddV4L,
        V4L_Add,
        // 用"SL"来简称string[]
        SL_New
    }

    #endregion

}
