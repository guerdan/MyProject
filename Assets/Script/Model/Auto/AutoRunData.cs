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
    }

    public struct FormulaVarInfo
    {
        public HashSet<AssignOperNode> Nodes;           //变量名
        public FormulaVarType Type;                     //变量类型

        public FormulaVarInfo(HashSet<AssignOperNode> nodes, FormulaVarType type)
        {
            Nodes = nodes;
            Type = type;
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
        #region 运行变量

        // 存储运行中的float变量
        private Dictionary<string, float> _floatVars = new Dictionary<string, float>();
        // 存储运行中的Vector2变量
        private Dictionary<string, Vector2> _v2Vars = new Dictionary<string, Vector2>();
        // 存储运行中的Vector4变量
        private Dictionary<string, Vector4> _v4Vars = new Dictionary<string, Vector4>();
        // 存储运行中的所有变量
        private Dictionary<string, object> _allVars = new Dictionary<string, object>();


        // 编辑下：变量的赋值次数。
        // 变化时机：1.增赋值节点 2.删赋值节点 3.修改赋值节点
        private Dictionary<string, FormulaVarInfo> _inEditVarRef = new Dictionary<string, FormulaVarInfo>();


        /// <summary>
        /// 赋值语句。必须指定类型。在UI侧第一次赋值变量需指定类型，后面就可自动
        /// 影响运行变量的
        /// </summary>
        public void RunAssignFormula(string var_name, string expression, FormulaVarType type, object inData)
        {
            _allVars[IN_VarName] = inData;
            Action delete = null;

            string in_type = inData?.GetType().Name;
            switch (in_type)
            {
                case "Single":
                    _floatVars[IN_VarName] = (float)inData;
                    delete = () => _floatVars.Remove(IN_VarName);
                    break;
                case "Vector2":
                    _v2Vars[IN_VarName] = (Vector2)inData;
                    delete = () => _v2Vars.Remove(IN_VarName);
                    break;
                case "Vector4":
                    _v4Vars[IN_VarName] = (Vector4)inData;
                    delete = () => _v4Vars.Remove(IN_VarName);
                    break;
            }


            switch (type)
            {
                case FormulaVarType.Float:
                    {
                        var v = FormulaGetResult(expression);
                        _floatVars[var_name] = v;
                        _allVars[var_name] = v;
                        DU.LogWarning($"变量 {var_name} : {v}");
                    }
                    break;
                case FormulaVarType.Vector2:
                    {
                        var v = FormulaGetResultV2(expression);
                        _v2Vars[var_name] = v;
                        _allVars[var_name] = v;
                        DU.LogWarning($"变量 {var_name} : {v}");
                    }
                    break;
                case FormulaVarType.Vector4:
                    {
                        var v = FormulaGetResultV4(expression);
                        _v4Vars[var_name] = v;
                        _allVars[var_name] = v;
                        DU.LogWarning($"变量 {var_name} : {v}");
                    }
                    break;
            }

            _allVars.Remove(IN_VarName);
            delete?.Invoke();
        }


        /// <summary>
        /// 编辑器环境下：统计所有的变量名和方法名
        /// </summary>
        public HashSet<string> GetInEditVarRef()
        {
            var result = new HashSet<string>();
            foreach (var item in _inEditVarRef.Keys)
            {
                result.Add(item);
            }

            return result;
        }

        public void AddVarRef(AssignOperNode node)
        {
            var name = node.VarNameLower;
            if (node.VarType == FormulaVarType.Undefined || name == "") return;

            if (_inEditVarRef.TryGetValue(name, out FormulaVarInfo info))
            {
                info.Nodes.Add(node);
            }
            else
            {
                var l = new HashSet<AssignOperNode>() { node };
                info = new FormulaVarInfo(l, node.VarType);
                _inEditVarRef[name] = info;
            }
        }

        public void DeleteVarRef(AssignOperNode node)
        {
            if (node.VarType == FormulaVarType.Undefined) return;

            var name = node.VarNameLower;
            if (_inEditVarRef.TryGetValue(name, out FormulaVarInfo info))
            {
                info.Nodes.Remove(node);
                if (info.Nodes.Count <= 0)
                {
                    _inEditVarRef.Remove(name);
                }
            }
        }

        public bool CheckFormula(string name, string expression, FormulaVarType type)
        {
            if (type == FormulaVarType.Undefined || name == "")
                return false;

            if (GetVarInfo(name, out var info) && info.Type != type)
                return false;

            return AutoDataUIConfig.ExpressionIsLegal(expression);

        }



        public bool GetVarInfo(string name, out FormulaVarInfo info)
        {
            return _inEditVarRef.TryGetValue(name, out info);
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


        /// <summary>
        /// 不存在的变量。我们考虑赋值和使用时机对不上的情况。不存在的变量就返回默认值。
        /// 方法变量。设计为：由"{"，"}"包裹。方法变量与方法变量不嵌套。目前不能包含运算符,定义方法不能重载
        /// 成员变量。设计为：可嵌套。支持：Vector2/Vector4
        /// 值变量。
        /// </summary>
        public float OperandResolver(string operand)
        {
            var is_method = operand.IndexOf("{") >= 0;
            if (is_method)
            {
                // 方法变量
                RPNCalculator.GetMethodParseResult(operand, out string method_name, out string[] param_list);

                // 调用方法
                switch (method_name)
                {
                    case "Add":
                        return Invoke(Add, param_list);
                    default:
                        DU.LogError($"方法 {method_name} 不存在，默认值0");
                        return 0;
                }
            }
            else
            {
                // a.b.c => Method(Method(a,"b"),"c")

                if (operand.IndexOf(".") > 0)
                {
                    //访问字段变量
                    var str_list = RPNCalculator.GetAccessParse(operand);

                    var var_name = str_list[0];
                    if (!_allVars.TryGetValue(var_name, out object obj))
                    {
                        DU.LogWarning($"变量 {var_name} 不存在于all集合中，默认值0");
                        return 0;
                    }

                    for (int i = 1; i < str_list.Length; i++)
                    {
                        var field_name = str_list[i];
                        bool r = TryAccessField(obj, field_name, out obj);
                        if (!r)
                        {
                            DU.LogWarning($"变量 {operand} 不存在字段 {field_name}，默认值0");
                            return 0;
                        }
                    }

                    return (float)obj;
                }
                else
                {
                    if (_floatVars.TryGetValue(operand, out var v))
                        return v;

                    DU.LogWarning($"变量 {operand} 不存在于_floatVars，默认值0");
                    return 0;
                }

            }

        }

        public float ParseFloat(string str)
        {
            if (RPNCalculator.IsFloat(str, out var number))
            {
                return number;
            }

            return FormulaGetResult(str);
        }




        #endregion

        #region Vector2
        public Vector2 FormulaGetResultV2(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
            var result = RPNCalculator.EvaluateRPNForVector2(list, OperandResolverV2);
            return result;
        }

        public Vector2 OperandResolverV2(string operand)
        {

            var is_method = operand.IndexOf("{") >= 0;
            if (is_method)
            {
                // 方法变量
                RPNCalculator.GetMethodParseResult(operand, out string method_name, out string[] param_list);

                // 调用方法
                switch (method_name)
                {
                    case "V2":
                        return Invoke(V2Constructor, param_list);
                    case "GetCenter":
                        return Invoke(GetCenter, param_list);
                    default:
                        DU.LogError($"方法 {method_name} 不存在，默认值0");
                        return Vector2.zero;
                }
            }
            else
            {
                //成员变量
                if (operand.IndexOf(".") > 0)
                {
                    //访问字段变量
                    return Vector2.zero;
                }
                else
                {
                    if (_v2Vars.TryGetValue(operand, out var v))
                        return v;

                    DU.LogWarning($"变量 {operand} 不存在于_v2Vars，默认值0");
                    return Vector2.zero;
                }
            }
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

        public Vector4 OperandResolverV4(string operand)
        {

            var is_method = operand.IndexOf("{") >= 0;
            if (is_method)
            {
                // 方法变量
                RPNCalculator.GetMethodParseResult(operand, out string method_name, out string[] param_list);

                // 调用方法
                switch (method_name)
                {
                    case "V4":
                        return Invoke(V4Constructor, param_list);
                    case "Screen":
                        return Invoke(Screen, param_list);
                    default:
                        DU.LogError($"方法 {method_name} 不存在，默认值0");
                        return Vector4.zero;
                }
            }
            else
            {
                if (operand.IndexOf(".") > 0)
                {
                    //访问字段变量
                    return Vector4.zero;
                }
                else
                {
                    if (_v4Vars.TryGetValue(operand, out var v))
                        return v;

                    DU.LogWarning($"变量 {operand} 不存在于_v2Vars，默认值0");
                    return Vector4.zero;
                }
            }
        }
        public Vector4 ParseVector4(string str)
        {
            return FormulaGetResultV4(str);
        }

        #endregion

        #region Condition (float)
        public bool FormulaGetResultCondition(string formula)
        {
            var list = RPNCalculator.GetRPN(formula);
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
            var left_v = ParseFloat(left);
            var right_v = ParseFloat(right);

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
        #endregion
    }
}
