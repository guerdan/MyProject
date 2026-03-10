using System;
using System.Collections.Generic;
using Script.Util;
using UnityEngine;

namespace Script.Model.Auto
{
    public enum FormulaVarType
    {
        Undefined,          //未定义
        Float,              //单值
        Vector2,            //双值
        Vector4,            //四值
        ListFloat,          //单值列表
        ListVector2,        //双值列表
        ListVector4,        //四值列表

    }

    public struct FormulaVarInfo
    {
        public object Value;                            //变量值    
        public FormulaVarType Type;                     //变量类型

        public FormulaVarInfo(FormulaVarType type, object value)
        {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// Calculate Info
    /// </summary>
    public struct FormulaVarInfo_Cal
    {
        public FormulaVarType Type;                     //变量类型
        public float F;
        public Vector2 V2;
        public Vector4 V4;
        public float[] ListF;
        public Vector2[] ListV2;
        public Vector4[] ListV4;

        public FormulaVarInfo_Cal(FormulaVarInfo info)
        {
            Type = info.Type;
            F = 0;
            V2 = default;
            V4 = default;
            ListF = null;
            ListV2 = null;
            ListV4 = null;
            if (info.Type == FormulaVarType.Float)
                F = (float)info.Value;
            else if (info.Type == FormulaVarType.Vector2)
                V2 = (Vector2)info.Value;
            else if (info.Type == FormulaVarType.Vector4)
                V4 = (Vector4)info.Value;
            else if (info.Type == FormulaVarType.ListFloat)
                ListF = (float[])info.Value;
            else if (info.Type == FormulaVarType.ListVector2)
                ListV2 = (Vector2[])info.Value;
            else if (info.Type == FormulaVarType.ListVector4)
                ListV4 = (Vector4[])info.Value;

        }

    }

    public struct FormulaVarInfo_Edit
    {
        public HashSet<BaseNodeData> Nodes;             //变量名
        public FormulaVarType Type;                     //变量类型
        public string VarName;                          //变量名
        public string VarNameLower;                     //小写变量名，用于搜索

        public FormulaVarInfo_Edit(FormulaVarType type, string varName)
        {
            Type = type;
            VarName = varName;
            VarNameLower = VarName.ToLower();

            Nodes = new HashSet<BaseNodeData>();
        }
    }

    public struct AssignFormulaParse
    {
        public string VarName;
        public RPN_TokenInfo ArrayIndex;
        public RPN_TokenInfo[] RightExpression;

        public AssignFormulaParse(string formula)
        {
            VarName = "";
            ArrayIndex = default;
            RightExpression = null;

            var equal_index = formula.IndexOf("=");
            if (equal_index < 0)
                return;

            var left = formula.Substring(0, equal_index);
            var expression = formula.Substring(equal_index + 1);

            var start = left.IndexOf("[");
            if (start > -1)
            {
                VarName = left.Substring(0, start);     // aa[2]
                var indexStr = left.Substring(start + 1, left.Length - 2 - start);
                ArrayIndex = RPN_TokenInfo.ParseParam(indexStr);
            }
            else
            {
                VarName = left;
            }
            RightExpression = RPNCalculator.InfixToRPN(expression, false);
        }
    }



    /// <summary>
    /// "IN"为系统变量，不能作为变量名
    /// 增加方法时:1.定义方法内容; 2.要重载它的Invoke; 3.OperandResolver()里扩充switch
    /// 增加"."时:目前手写逻辑
    /// 变量名：大小写不能区分，不能多种类型
    /// 检查逻辑: 只看表达式
    /// 方法变量嵌套方法变量
    /// todo
    /// 
    /// </summary>
    public partial class AutoScriptData
    {
        public static readonly string IN_VarName = "IN";
        public event Action OnVarValueChange;

        private Dictionary<string, AssignFormulaParse> _assignFormulaCache = new Dictionary<string, AssignFormulaParse>();

        #region 运行变量
        // 存储运行中的所有变量。 变量不能重名
        private Dictionary<string, FormulaVarInfo> _allVars = new Dictionary<string, FormulaVarInfo>();


        // 编辑下：统计变量的"赋值"。
        // 变化时机：1.增赋值节点 2.删赋值节点 3.修改赋值节点
        private Dictionary<string, FormulaVarInfo_Edit> InEdit_VarRef = new Dictionary<string, FormulaVarInfo_Edit>();


        /// <summary>
        /// 赋值语句。必须指定类型。在UI侧第一次赋值变量需指定类型，后面就可自动提示
        /// 影响运行变量的
        /// 变量名、值公式、变量类型
        /// 
        /// 列表只支持 按长度初始化
        /// </summary>
        public void RunAssignFormula(string formula, FormulaVarType type, FormulaVarInfo inData)
        {
            if (!_assignFormulaCache.TryGetValue(formula, out var parseInfo))
            {
                parseInfo = new AssignFormulaParse(formula);
                _assignFormulaCache[formula] = parseInfo;
            }

            // 这里解决右边式子的值
            if (inData.Type != FormulaVarType.Undefined)
                _allVars[IN_VarName] = inData;

            object v = null;
            RPN_TokenInfo[] rightRPN = parseInfo.RightExpression;
            switch (type)
            {
                case FormulaVarType.Float:
                    v = FormulaGetResult(rightRPN);
                    break;
                case FormulaVarType.Vector2:
                    v = FormulaGetResultV2(rightRPN);
                    break;
                case FormulaVarType.Vector4:
                    v = FormulaGetResultV4(rightRPN);
                    break;
                case FormulaVarType.ListFloat:
                    v = FormulaGetResultFL(rightRPN);
                    break;
                case FormulaVarType.ListVector2:
                    v = FormulaGetResultV2L(rightRPN);
                    break;
                case FormulaVarType.ListVector4:
                    v = FormulaGetResultV4L(rightRPN);
                    break;
            }


            if (inData.Type != FormulaVarType.Undefined)
                _allVars.Remove(IN_VarName);

            RunAssign(parseInfo, type, v);
        }


        /// <summary>
        /// 这里解决左边式子的赋值。
        /// </summary>
        public void RunAssign(AssignFormulaParse parseInfo, FormulaVarType type, object value)
        {

            var left_index_type = parseInfo.ArrayIndex.Type;
            string var_name = parseInfo.VarName;
            RPN_TokenInfo arrayIndex_token = parseInfo.ArrayIndex;
            if (left_index_type == RPN_TokenType.Undefined)         // 直接变量赋值
            {
                _allVars[var_name] = new FormulaVarInfo(type, value);
            }
            else
            {
                int index = 0;
                if (left_index_type == RPN_TokenType.Formula)      //数组的动态索引赋值
                {
                    index = (int)FormulaGetResult(arrayIndex_token.RawStr);
                }
                else if (left_index_type == RPN_TokenType.Float)    //数组的静态索引赋值
                {
                    index = (int)arrayIndex_token.Param.F;
                }

                switch (type)
                {
                    case FormulaVarType.Float:
                        {
                            var list = (float[])_allVars[var_name].Value;
                            list[index] = (float)value;
                        }
                        break;
                    case FormulaVarType.Vector2:
                        {
                            var list = (Vector2[])_allVars[var_name].Value;
                            list[index] = (Vector2)value;
                        }
                        break;
                    case FormulaVarType.Vector4:
                        {
                            var list = (Vector4[])_allVars[var_name].Value;
                            list[index] = (Vector4)value;
                        }
                        break;
                }
            }

            OnVarValueChange?.Invoke();

            // bool is_debug = true;
            // if (is_debug) DU.LogWarning($"变量 {var_name} : {value}");
        }

        public void RunAssign(string var_name, FormulaVarType type, object value)
        {
            _allVars[var_name] = new FormulaVarInfo(type, value);
            OnVarValueChange?.Invoke();
        }

        /// <summary>
        /// 编辑器环境下：统计所有的变量名和方法名
        /// </summary>
        public Dictionary<string, FormulaVarInfo_Edit> GetInEditVarRef()
        {
            return InEdit_VarRef;
        }

        public void AddVarRef(string var_name, FormulaVarType var_type, BaseNodeData node)
        {
            if (var_type == FormulaVarType.Undefined || var_name == "")
                return;

            if (!InEdit_VarRef.TryGetValue(var_name, out FormulaVarInfo_Edit info))
            {
                info = new FormulaVarInfo_Edit(var_type, var_name);
                InEdit_VarRef[var_name] = info;
            }

            info.Nodes.Add(node);
        }

        public void DeleteVarRef(string var_name, FormulaVarType var_type, BaseNodeData node)
        {
            if (var_type == FormulaVarType.Undefined || var_name == "")
                return;

            if (InEdit_VarRef.TryGetValue(var_name, out FormulaVarInfo_Edit info))
            {
                info.Nodes.Remove(node);
                if (info.Nodes.Count <= 0)
                {
                    InEdit_VarRef.Remove(var_name);
                }
            }
        }

        public bool CheckConditionLegal(string formula)
        {
            string[] expressions = new string[3];
            var index0 = formula.IndexOf(";");
            if (index0 > 0)
            {
                // 以";"分隔。0/2是赋值表达式，1是条件表达式
                expressions = formula.Split(";");
            }
            if (expressions.Length != 3)
                return false;

            bool result = AutoDataUIConfig.ConditionIsLegal(expressions[1])
                            && AutoDataUIConfig.ExpressionIsLegal(expressions[2]);
            if (expressions[0] != "")
                result = result && AutoDataUIConfig.ExpressionIsLegal(expressions[0]);

            return result;
        }
        public bool CheckFormulaLegal(string formula, FormulaVarType type)
        {
            var equal_index = formula.IndexOf("=");
            var var_name = "";
            var left_str = "";
            if (equal_index > 0)
            {
                var_name = formula.Substring(0, equal_index);
                left_str = formula.Substring(equal_index + 1);
            }
            else
            {
                var_name = "";
            }

            var isLegal = CheckVar(var_name, type);
            var expressions = new string[3];

            var index1 = left_str.IndexOf("?");
            var index2 = left_str.IndexOf(":");
            if (index1 > -1 && index2 > -1)
            {
                expressions[0] = left_str.Substring(0, index1);
                expressions[1] = left_str.Substring(index1 + 1, index2 - index1 - 1);
                expressions[2] = left_str.Substring(index2 + 1);
                isLegal = isLegal
                            && AutoDataUIConfig.ConditionIsLegal(expressions[0])
                            && AutoDataUIConfig.ExpressionIsLegal(expressions[1])
                            && AutoDataUIConfig.ExpressionIsLegal(expressions[2]);

            }
            else
            {
                expressions[0] = left_str;
                expressions[1] = "";
                expressions[2] = "";
                isLegal = isLegal && AutoDataUIConfig.ExpressionIsLegal(expressions[0]);
            }
            return isLegal;
        }

        public bool CheckFormula(string name, string expression, FormulaVarType type)
        {
            return CheckVar(name, type) && AutoDataUIConfig.ExpressionIsLegal(expression);
        }
        public bool CheckVar(string name, FormulaVarType type)
        {
            if (type == FormulaVarType.Undefined || name == "")
                return false;
            // 新声明 或者 类型符合
            return !GetFormulaVarInfo_Edit(name, out var info) || info.Type == type;

        }

        public bool GetFormulaVarInfo_Edit(string name, out FormulaVarInfo_Edit info)
        {
            return InEdit_VarRef.TryGetValue(name, out info);
        }
        public FormulaVarInfo GetVarValue(string name)
        {
            _allVars.TryGetValue(name, out var result);
            return result;
        }

        #region float

        /// <summary>
        /// 计算表达式的值
        /// </summary>
        public float FormulaGetResult(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = RPNCalculator.EvaluateRPN(list, OperandResolver);
            return result;
        }
        public float FormulaGetResult(RPN_TokenInfo[] list)
        {
            var result = RPNCalculator.EvaluateRPN(list, OperandResolver);
            return result;
        }


        /// <summary>
        /// 不存在的变量。我们考虑赋值和使用时机对不上的情况。不存在的变量就返回默认值。
        /// 方法变量。设计为：由"{"，"}"包裹。支持方法参数内含运算符、方法变量。方法不可同名，也不支持方法重载
        /// 成员变量。设计为：可嵌套。支持：Vector2/Vector4
        /// 值变量。
        /// 
        /// 优化空间：对于方法参数不断嵌套运算符、方法变量的情况，可以暂存运算顺序与RPN结果，减少字典查找与解析字符操作
        /// </summary>
        public float OperandResolver(RPN_TokenInfo tInfo)
        {
            var p = tInfo.Param;

            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (float)v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return 0;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.F_Add:
                        return Invoke(Add, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }
            }
            else if (tInfo.Type == RPN_TokenType.FieldVar)
            {
                var fields = p.Strs;

                //先访问第一个变量
                var var_name = fields[0];

                FormulaVarInfo_Cal obj = default;
                if (_allVars.TryGetValue(var_name, out var var_info))
                {
                    obj = new FormulaVarInfo_Cal(var_info);
                }
                else
                {
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");
                    return 0;
                }

                for (int i = 1; i < fields.Length; i++)
                {
                    var field_name = fields[i];
                    bool r = TryAccessField(obj, field_name, out obj);
                    if (!r)
                    {
                        DU.LogWarning($"变量 {var_name} 不存在子字段 {field_name}");
                        return 0;
                    }
                }

                return obj.F;
            }
            else if (tInfo.Type == RPN_TokenType.ArrayVar)
            {
                var var_name = p.Str;
                int index = (int)p.F;
                if (_allVars.TryGetValue(var_name, out var v))
                    if (v.Type == FormulaVarType.ListFloat)
                    {
                        var list = (List<float>)v.Value;
                        return list[index];
                    }
                    else
                        DU.LogWarning($"变量 {var_name} 不是Float数组类型");
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return 0;
            }

            DU.LogWarning($"变量 {tInfo.RawStr} 计算异常");
            return 0;
        }

        // 稍微用缓存加速下
        public float ParseFloat(RPN_TokenInfo tInfo)
        {
            if (tInfo.Type == RPN_TokenType.Float)
                return tInfo.Param.F;
            return FormulaGetResult(tInfo.RawStr);
        }




        #endregion

        #region Vector2
        public Vector2 FormulaGetResultV2(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = RPNCalculator.EvaluateRPNForVector2(list, OperandResolverV2);
            return result;
        }
        public Vector2 FormulaGetResultV2(RPN_TokenInfo[] list)
        {
            var result = RPNCalculator.EvaluateRPNForVector2(list, OperandResolverV2);
            return result;
        }

        public Vector2 OperandResolverV2(RPN_TokenInfo tInfo)
        {
            var p = tInfo.Param;

            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (Vector2)v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.V2_Constructor:
                        return Invoke(V2_Constructor, param_list);
                    case MethodID.V2_GetCenter:
                        return Invoke(GetCenter, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }
            }
            else if (tInfo.Type == RPN_TokenType.ArrayVar)
            {
                var var_name = p.Str;
                int index = (int)p.F;
                if (_allVars.TryGetValue(var_name, out var v))
                    if (v.Type == FormulaVarType.ListVector2)
                    {
                        var list = (List<Vector2>)v.Value;
                        return list[index];
                    }
                    else
                        DU.LogWarning($"变量 {var_name} 不是Float数组类型");
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }

            DU.LogWarning($"变量 {tInfo.RawStr} 计算异常");
            return default;
        }

        public Vector2 ParseVector2(string str)
        {
            return FormulaGetResultV2(str);
        }


        #endregion


        #region Vector4
        public Vector4 FormulaGetResultV4(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = RPNCalculator.EvaluateRPNForVector4(list, OperandResolverV4);
            return result;
        }
        public Vector4 FormulaGetResultV4(RPN_TokenInfo[] list)
        {
            var result = RPNCalculator.EvaluateRPNForVector4(list, OperandResolverV4);
            return result;
        }

        public Vector4 OperandResolverV4(RPN_TokenInfo tInfo)
        {
            var p = tInfo.Param;

            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (Vector4)v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.V4_Constructor:
                        return Invoke(V4_Constructor, param_list);
                    case MethodID.V4_Screen:
                        return Invoke(Screen, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }

            }
            else if (tInfo.Type == RPN_TokenType.ArrayVar)
            {
                var var_name = p.Str;
                int index = (int)p.F;
                if (_allVars.TryGetValue(var_name, out var v))
                    if (v.Type == FormulaVarType.ListVector4)
                    {
                        var list = (List<Vector4>)v.Value;
                        return list[index];
                    }
                    else
                        DU.LogWarning($"变量 {var_name} 不是Vector4数组类型");
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }

            DU.LogWarning($"变量 {tInfo.RawStr} 计算异常");
            return default;
        }
        public Vector4 ParseVector4(string str)
        {
            return FormulaGetResultV4(str);
        }

        #endregion


        #region FloatList

        public float[] FormulaGetResultFL(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = FormulaGetResultFL(list);
            return result;
        }
        public float[] FormulaGetResultFL(RPN_TokenInfo[] list)
        {
            if (list.Length != 1)
                throw new Exception($"获取ListFloat 语法错误");

            var tInfo = list[0];
            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (float[])v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var p = tInfo.Param;
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.FL_Constructor:
                        return Invoke(FL_Constructor, param_list);
                    case MethodID.FL_Find:
                        return Invoke(FL_FindIndex, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }
            }
            else
            {
                throw new Exception($"获取ListFloat 语法错误");
            }

        }
        #endregion

        #region Vector2List

        public Vector2[] FormulaGetResultV2L(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = FormulaGetResultV2L(list);
            return result;
        }
        public Vector2[] FormulaGetResultV2L(RPN_TokenInfo[] list)
        {
            if (list.Length != 1)
                throw new Exception($"获取ListFloat 语法错误");

            var tInfo = list[0];
            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (Vector2[])v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var p = tInfo.Param;
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.V2L_Constructor:
                        return Invoke(V2L_Constructor, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }

            }
            else
            {
                throw new Exception($"获取ListFloat 语法错误");
            }

        }
        #endregion

        #region Vector4List
        public Vector4[] FormulaGetResultV4L(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = FormulaGetResultV4L(list);
            return result;
        }
        public Vector4[] FormulaGetResultV4L(RPN_TokenInfo[] list)
        {
            if (list.Length != 1)
                throw new Exception($"获取ListFloat 语法错误");

            var tInfo = list[0];
            if (tInfo.Type == RPN_TokenType.Variable)
            {
                var var_name = tInfo.RawStr;
                if (_allVars.TryGetValue(var_name, out var v))
                    return (Vector4[])v.Value;
                else
                    DU.LogWarning($"变量 {var_name} 不存在于变量字典中");

                return default;
            }
            else if (tInfo.Type == RPN_TokenType.MethodVar)
            {
                var p = list[0].Param;
                var method_id = p.MethodID;
                var param_list = p.Params;

                // 调用方法
                switch (method_id)
                {
                    case MethodID.V4L_Constructor:
                        return Invoke(V4L_Constructor, param_list);
                    default:
                        throw new Exception($"方法 {p.Str} 不存在，默认值0");
                }
            }
            else
            {
                throw new Exception($"获取ListFloat 语法错误");
            }
        }

        #endregion

        #region Condition (float)
        public bool FormulaGetResultCondition(string formula)
        {
            var list = RPNCalculator.GetRPN(formula, true);
            var result = RPNCalculator.EvaluateRPNForCondition(list, OperandResolverCondition);
            return result;
        }

        public bool OperandResolverCondition(string operand)
        {
            RPNCalculator.ConditionParseCompareOper(operand, out var oper, out var left, out var right);
            if (oper == "")
            {
                DU.LogWarning($"变量 {operand} 不含比较符");
                return false;
            }
            var left_v = FormulaGetResult(left);
            var right_v = FormulaGetResult(right);

            switch (oper)
            {
                case "==":
                    return left_v == right_v;
                case "!=":
                    return left_v != right_v;
                case ">":
                    return left_v > right_v;
                case "<":
                    return left_v < right_v;
                case ">=":
                    return left_v >= right_v;
                case "<=":
                    return left_v <= right_v;
                default:
                    DU.LogWarning($"变量 {operand} 比较符 {oper} 不存在");
                    return false;
            }

        }
        #endregion

        void ClearRunData()
        {
            _allVars.Clear();
        }

        #endregion
    }
}
