
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Script.Model.Auto
{
    public enum RPN_TokenType
    {
        Undefined,                  // 未定义
        OperatorAdd,                // 加
        OperatorSub,                // 减
        OperatorMul,                // 乘
        OperatorDvi,                // 除
        OperatorOr,                 // 或
        OperatorAnd,                // 与
        OperatorEqual,              // 等于
        OperatorNotEqual,           // 不等于
        OperatorGreater,            // 大于
        OperatorGreaterOrEqual,     // 大于等于
        OperatorSmaller,            // 小于
        OperatorSmallerOrEqual,     // 小于等于
        Float,                      // 浮点数
        String,                     // 字符串
        Array,                      // 数组常量
        Formula,                    // 公式
        Condition,                  // 条件表达式
        Variable,                   // 变量
        MethodVar,                  // 变量—调用方法取得
        FieldVar,                   // 变量—访问字段取得
        ArrayIndexOper,             // 变量—数组索引访问
    }

    #region TokenInfo



    /// <summary>
    /// 四则运算与括号 > 常数 > 数组常量 > 访问方法 > 数组访问元素 > 字符串常量 > 对象访问字段 > 普通变量
    /// 会出现","的情况：1.访问方法;2.数组常量
    /// </summary>
    public struct RPN_TokenInfo
    {
        public string RawStr;
        public RPN_TokenType Type;
        public RPN_TokenParam Param;

        public RPN_TokenInfo(string token)
        {
            RawStr = token;
            Param = null;

            if (token == "+")
                Type = RPN_TokenType.OperatorAdd;
            else if (token == "-")
                Type = RPN_TokenType.OperatorSub;
            else if (token == "*")
                Type = RPN_TokenType.OperatorMul;
            else if (token == "/")
                Type = RPN_TokenType.OperatorDvi;

            else
            {
                bool isFloat = float.TryParse(token, out var f);
                if (isFloat)
                {
                    Type = RPN_TokenType.Float;
                    Param = new RPN_TokenParam();
                    Param.F = f;
                }
                else
                {
                    var array_symbol = token.IndexOf('[');
                    var method_symbol = token.IndexOf('{');

                    int some_type = -1;
                    int min_index = -1;
                    if (array_symbol > -1 && (min_index == -1 || array_symbol < min_index))
                    { min_index = array_symbol; some_type = 0; }
                    if (method_symbol > -1 && (min_index == -1 || method_symbol < min_index))
                    { min_index = method_symbol; some_type = 1; }


                    if (array_symbol == 0)      // 整个数组赋值
                    {
                        Type = RPN_TokenType.Array;
                        RPNCalculator.ParseArray(token, out string[] paras);
                        Param = new RPN_TokenParam();
                        Param.Strs = paras;
                    }
                    else if (some_type == 0)    // 数组索引取值
                    {
                        Type = RPN_TokenType.ArrayIndexOper;
                        Param = new RPN_TokenParam();
                        RPNCalculator.ParseArrayVar(token, out var var_name, out var index_token);
                        Param.Str = var_name;
                        Param.Params = new RPN_TokenInfo[] { index_token };
                    }
                    else if (some_type == 1)    // 调用方法
                    {
                        Type = RPN_TokenType.MethodVar;
                        RPNCalculator.ParseMethodVar(token, out var method_id, out var method_name, out string[] paras);
                        Param = new RPN_TokenParam();
                        Param.MethodID = method_id;
                        Param.Str = method_name;
                        Param.Params = new RPN_TokenInfo[paras.Length];      // 方法的多个参数
                        for (int i = 0; i < paras.Length; i++)
                        {
                            Param.Params[i] = ParseParam(paras[i]);   // 方法参数
                        }
                    }
                    else if (token[0] == '\"')
                    {
                        Type = RPN_TokenType.String;
                        Param = new RPN_TokenParam();
                        Param.Str = token.Substring(1, token.Length - 2);
                    }
                    else if (token.IndexOf('.') > -1)
                    {
                        Type = RPN_TokenType.FieldVar;
                        Param = new RPN_TokenParam();
                        Param.Strs = token.Split('.');      // 对象字段访问列表
                    }
                    else
                        Type = RPN_TokenType.Variable;
                }

            }
        }


        public static RPN_TokenInfo ParseConditionToken(string token)
        {
            RPN_TokenInfo result = default;
            result.RawStr = token;

            if (token == "||")
                result.Type = RPN_TokenType.OperatorOr;
            else if (token == "&&")
                result.Type = RPN_TokenType.OperatorAnd;
            else
            {
                result.Type = RPN_TokenType.Condition;
                var param = new RPN_TokenParam();
                result.Param = param;
                RPNCalculator.ConditionParseCompareOper(token, out var oper, out var left, out var right);

                RPN_TokenType operType;
                switch (oper)
                {
                    case "==": operType = RPN_TokenType.OperatorEqual; break;
                    case "!=": operType = RPN_TokenType.OperatorNotEqual; break;
                    case ">": operType = RPN_TokenType.OperatorGreater; break;
                    case ">=": operType = RPN_TokenType.OperatorGreaterOrEqual; break;
                    case "<": operType = RPN_TokenType.OperatorSmaller; break;
                    case "<=": operType = RPN_TokenType.OperatorSmallerOrEqual; break;
                    default: operType = RPN_TokenType.Undefined; break;
                }

                if (operType == RPN_TokenType.Undefined)
                {
                    // 这里默认以 left > 0 语句来解析
                    param.Params = RPNCalculator.InfixToRPN(left, false);
                    param.Int = (int)RPN_TokenType.OperatorGreater;
                }
                else
                {
                    // 条件表达式转成 left - (right) ? 0
                    //
                    var expression = right != "" ? $"{left}-({right})" : left;
                    param.Params = RPNCalculator.InfixToRPN(expression, false);
                    param.Int = (int)operType;
                }

            }
            return result;

        }
        public static RPN_TokenInfo ParseParam(string token)
        {
            RPN_TokenInfo result = default;
            result.RawStr = token;

            if (token[0] == '\"')
            {
                result.Type = RPN_TokenType.String;
                result.Param = new RPN_TokenParam();
                result.Param.Str = token.Substring(1, token.Length - 2);
            }
            else if (float.TryParse(token, out var f))
            {
                result.Type = RPN_TokenType.Float;
                result.Param = new RPN_TokenParam();
                result.Param.F = f;
            }
            else
                result.Type = RPN_TokenType.Formula;


            return result;
        }




    }


    public class RPN_TokenParam
    {
        public float F;                     // FloatValue
        public int Int = -1;                //  ConditionCompareOper / AnotherDataIndex(指向其他数据的索引)
        public string Str;                  // ArrayName  / MethodName(for debug)
        public string[] Strs;               // FieldAccessList / ArrayElements

        public MethodID MethodID;               // MethodName 
        public RPN_TokenInfo[] Params;          // ArrayIndex / MethodParams / ConditionFormula

    }

    public struct RPN_Condition
    {
        public RPN_TokenInfo[] Formula;         // MethodParams 
        public RPN_TokenInfo Operator;         // MethodParams 

    }
    #endregion

    #region RPNCalculator
    /// <summary>
    /// 逆波兰表达式接口
    /// todo
    /// 1.验证方法
    /// </summary>
    public static class RPNCalculator
    {
        // 词的分割方法
        public delegate List<string> TokenizeMethod(string expression);
        static Dictionary<string, (int precedence, string associativity)> operators = new Dictionary<string, (int, string)>
        {
            { "+", (2, "L") },
            { "-", (2, "L") },
            { "*", (3, "L") },
            { "/", (3, "L") },
            { "||", (4, "L") },
            { "&&", (5, "L") },
        };

        // 缓存解析结果
        static Dictionary<string, RPN_TokenInfo[]> _cache = new Dictionary<string, RPN_TokenInfo[]>();
        // 缓存条件表达式的解析结果
        static Dictionary<string, RPN_Condition> _conditionCache = new Dictionary<string, RPN_Condition>();
        // 缓存方法解析的结果
        static Dictionary<string, (MethodID, string[])> InEdit_MethodCache = new Dictionary<string, (MethodID, string[])>();

        static float[] _methodFList = new float[1000];              //方法内部复用
        static Vector2[] _methodV2List = new Vector2[1000];         //方法内部复用
        static Vector4[] _methodV4List = new Vector4[1000];         //方法内部复用
        static float[][] _methodFLList = new float[1000][];         //方法内部复用
        static Vector2[][] _methodV2LList = new Vector2[1000][];    //方法内部复用
        static Vector4[][] _methodV4LList = new Vector4[1000][];    //方法内部复用
        static bool[] _methodBoolList = new bool[1000];             //方法内部复用


        public static void Clear()
        {
            _cache.Clear();
            InEdit_MethodCache.Clear();

        }

        /// <summary>
        /// 获取逆波兰表达式
        /// </summary>
        public static RPN_TokenInfo[] GetRPN(string expression, bool isCondition = false)
        {
            if (!_cache.TryGetValue(expression, out var result))
            {
                RPN_TokenInfo[] rpn = InfixToRPN(expression, isCondition);
                _cache[expression] = rpn;
                result = rpn;
            }

            return result;
        }


        static Stack<string> operatorStack_I = new Stack<string>();   //优化-复用
        // 将中缀表达式转换为逆波兰表达式
        public static RPN_TokenInfo[] InfixToRPN(string expression, bool isCondition = false)
        {
            List<string> outputQueue = new List<string>();
            operatorStack_I.Clear();

            TokenizeMethod tokenize = isCondition ? ConditionTokenize : Tokenize;
            List<string> tokens = tokenize(expression);

            foreach (var token in tokens)
            {
                if (IsOperator(token))
                {
                    while (operatorStack_I.Count > 0 && IsOperator(operatorStack_I.Peek()))
                    {
                        var topOperator = operatorStack_I.Peek();
                        var currentOp = operators[token];
                        var topOp = operators[topOperator];

                        if ((currentOp.associativity == "L" && currentOp.precedence <= topOp.precedence) ||
                            (currentOp.associativity == "R" && currentOp.precedence < topOp.precedence))
                        {
                            outputQueue.Add(operatorStack_I.Pop());
                        }
                        else
                        {
                            break;
                        }
                    }
                    operatorStack_I.Push(token);
                }
                else if (token == "(")
                {
                    operatorStack_I.Push(token);
                }
                else if (token == ")")
                {
                    while (operatorStack_I.Count > 0 && operatorStack_I.Peek() != "(")
                    {
                        outputQueue.Add(operatorStack_I.Pop());
                    }
                    operatorStack_I.Pop(); // 弹出左括号
                }
                else
                {
                    outputQueue.Add(token);
                }
            }

            while (operatorStack_I.Count > 0)
            {
                outputQueue.Add(operatorStack_I.Pop());
            }

            RPN_TokenInfo[] result = new RPN_TokenInfo[outputQueue.Count];

            if (isCondition)
                for (int i = 0; i < outputQueue.Count; i++)
                {
                    result[i] = RPN_TokenInfo.ParseConditionToken(outputQueue[i]);
                }
            else
                for (int i = 0; i < outputQueue.Count; i++)
                {
                    result[i] = new RPN_TokenInfo(outputQueue[i]);
                }

            return result;
        }

        static Regex _regex = new Regex(@"[+\-*/()]");


        // 将表达式字符串分割成 运算符与操作数
        public static List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            var matches = _regex.Matches(expression);

            // 大括号包含的内容不解析，整个方法变量当成一个整体，留着后续去处理。实现方法变量内嵌套运算符
            var temp = expression;

            int cursor = 0;
            // -1 * -1 ,要解决-后面跟数字，把-1作为操作数
            foreach (Match match in matches)
            {
                var oper = match.Value;
                var index = match.Index - cursor;               //找到它的位置,单字符
                // 如果"-"减号后面是数字, 则跳过(错)
                // 如果"-"减号前面是空的, 则跳过(对)
                if (oper == "-" && index == 0)
                    continue;

                // 实现方法的嵌套
                var front = temp.Substring(0, index);
                if (!CheckMethodBracket(front))                 // 可以优化的。目前的问题是 额外耗时在调用次数
                    continue;

                if (index > 0) tokens.Add(front);               //操作数——常量或变量
                tokens.Add(oper);                               //运算符

                int next_start = index + oper.Length;
                temp = temp.Substring(next_start);    //即使传入temp.Length属于特殊情况不会报错，返回空串
                cursor += next_start;
            }

            if (temp.Length > 0)
                tokens.Add(temp);                           //最后剩余的操作数


            return tokens;
        }

        /// <summary>
        /// 可以优化的。目前的问题是 额外耗时在调用次数
        /// </summary>
        public static bool CheckMethodBracket(string expression)
        {
            int balance = 0;
            foreach (char c in expression)
            {
                if (c == '{' || c == '[') balance++;
                else if (c == '}' || c == ']') balance--;

                if (balance < 0) return false; // 右括号多于左括号
            }
            return balance == 0; // 如果平衡则返回true
        }


        public static bool IsOperator(string token)
        {
            return operators.ContainsKey(token);
        }




        /// <summary>
        /// 获取逆波兰表达式
        /// </summary>
        public static void GetMethodParseResult(string expression, out MethodID method_name, out string[] param_list)
        {
            if (!InEdit_MethodCache.TryGetValue(expression, out var result))
            {
                ParseMethodVar(expression, out var methodID, out _, out var paras);
                result = (methodID, paras);
                InEdit_MethodCache[expression] = result;
            }

            method_name = result.Item1;
            param_list = result.Item2;
        }

        /// <summary>
        /// 解析方法的参数。嵌套情况是一层层解析
        /// Add{1,Add{1,1}}  解决"{}"内的","不能分割
        /// </summary>
        public static void ParseMethodVar(string source, out MethodID method_id,
                                        out string method_name, out string[] paras)
        {
            // 目标 source = Add{1,Add{1,1}}   method_name = Add  paras = [1, Add{1,1}]

            var front = source.IndexOf('{');
            // 优化。反括号一定在末尾
            var back = source.Length - 1;
            method_name = source.Substring(0, front);
            var expression = source.Substring(front + 1, back - front - 1);

            var result = new List<string>();
            char[] chars = expression.ToCharArray();
            int pre = 0;        //每次目标串的起点
            int balance = 0;
            int len = chars.Length;
            for (int i = 0; i < len; i++)
            {
                char c = chars[i];
                if (c == '{' || c == '[') balance++;
                else if (c == '}' || c == ']') balance--;
                else if (c == ',' && balance == 0)
                {
                    var variable = expression.Substring(pre, i - pre);
                    result.Add(variable);
                    pre = i + 1;
                }
            }
            if (pre < len)
                result.Add(expression.Substring(pre, len - pre));

            method_id = MethodParseUtil.ParseMethod(method_name);
            paras = result.ToArray();
        }

        /// <summary>
        /// 解析数组常量，输出每个元素字符串
        /// </summary>
        public static void ParseArray(string source, out string[] paras)
        {
            // 目标 source ="[Add{1,1},Add{1,1}]"   paras = [Add{1,1}, Add{1,1}]

            var expression = source.Substring(1, source.Length - 2);

            var result = new List<string>();
            char[] chars = expression.ToCharArray();
            int pre = 0;        //每次目标串的起点
            int balance = 0;
            int len = chars.Length;
            for (int i = 0; i < len; i++)
            {
                char c = chars[i];
                if (c == '{' || c == '[') balance++;
                else if (c == '}' || c == ']') balance--;
                else if (c == ',' && balance == 0)
                {
                    var variable = expression.Substring(pre, i - pre);
                    result.Add(variable);
                    pre = i + 1;
                }
            }
            if (pre < len)
                result.Add(expression.Substring(pre, len - pre));

            paras = result.ToArray();
        }


        public static void ParseArrayVar(string str, out string var_name, out RPN_TokenInfo index_token)
        {
            var index = str.IndexOf('[');
            var_name = str.Substring(0, index);
            var array_index_str = str.Substring(index + 1, str.Length - index - 2);

            index_token = default;
            index_token.RawStr = array_index_str;
            index_token.Param = new RPN_TokenParam();
            if (int.TryParse(array_index_str, out int int_result))
            {
                index_token.Type = RPN_TokenType.Float;
                index_token.Param.Int = int_result;
            }
            else
            {
                index_token.Type = RPN_TokenType.Variable;
            }
        }


        #region EvaluateRPN

        /// <summary>
        /// float类型,计算逆波兰表达式的值
        /// </summary>
        public static float EvaluateRPN(RPN_TokenInfo[] rpn, Func<RPN_TokenInfo, float> operandResolver)
        {
            float[] stack = _methodFList;    //换原生数组
            int count = 0;

            foreach (var token in rpn)
                switch (token.Type)          // switch速度也是慢了，可能项太多
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            float b = stack[--count];
                            float a = stack[--count];
                            stack[count++] = a + b;
                        }
                        break;
                    case RPN_TokenType.OperatorSub:
                        {

                            float b = stack[--count];
                            float a = stack[--count];
                            stack[count++] = a - b;
                        }
                        break;
                    case RPN_TokenType.OperatorMul:
                        {
                            float b = stack[--count];
                            float a = stack[--count];
                            stack[count++] = a * b;
                        }
                        break;
                    case RPN_TokenType.OperatorDvi:
                        {
                            float b = stack[--count];
                            float a = stack[--count];
                            stack[count++] = a / b;
                        }
                        break;
                    case RPN_TokenType.Float:
                        {
                            stack[count++] = token.Param.F;
                        }
                        break;
                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.FieldVar:
                    case RPN_TokenType.ArrayIndexOper:
                        {
                            float result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }

            if (count == 0)
                return 0;

            return stack[--count];
        }


        /// <summary>
        /// Vector2类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector2 EvaluateRPNForVector2(RPN_TokenInfo[] rpn, Func<RPN_TokenInfo, Vector2> operandResolver)
        {
            Vector2[] stack = _methodV2List;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            Vector2 b = stack[--count];
                            Vector2 a = stack[--count];
                            stack[count++] = a + b;
                        }
                        break;
                    case RPN_TokenType.OperatorSub:
                        {
                            Vector2 b = stack[--count];
                            Vector2 a = stack[--count];
                            stack[count++] = a - b;
                        }
                        break;
                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.ArrayIndexOper:
                        {
                            if (operandResolver == null)
                                return Vector2.zero;

                            Vector2 result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }
            }
            if (count == 0)
                return Vector2.zero;

            return stack[--count];
        }




        /// <summary>
        /// Vector4类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector4 EvaluateRPNForVector4(RPN_TokenInfo[] rpn,
                                                    Func<RPN_TokenInfo, Vector4> operandResolver)
        {
            Vector4[] stack = _methodV4List;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            Vector4 b = stack[--count];
                            Vector4 a = stack[--count];
                            stack[count++] = a + b;
                        }
                        break;
                    case RPN_TokenType.OperatorSub:
                        {
                            Vector4 b = stack[--count];
                            Vector4 a = stack[--count];
                            stack[count++] = a - b;
                        }
                        break;
                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.ArrayIndexOper:
                        {
                            Vector4 result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }

            }
            if (count == 0)
                return Vector4.zero;

            return stack[--count];
        }
        /// <summary>
        /// FL类型,计算逆波兰表达式的值
        /// </summary>
        public static float[] EvaluateRPNForFL(RPN_TokenInfo[] rpn,
                                                    Func<RPN_TokenInfo, float[]> operandResolver)
        {
            float[][] stack = _methodFLList;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            float[] b = stack[--count];
                            float[] a = stack[--count];
                            stack[count++] = AutoScriptData.FL_Add(a, b);
                        }
                        break;

                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.Array:
                        {
                            float[] result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }

            }
            if (count == 0)
                return null;

            return stack[--count];
        }
        /// <summary>
        /// V2L类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector2[] EvaluateRPNForV2L(RPN_TokenInfo[] rpn,
                                                    Func<RPN_TokenInfo, Vector2[]> operandResolver)
        {
            Vector2[][] stack = _methodV2LList;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            Vector2[] b = stack[--count];
                            Vector2[] a = stack[--count];
                            stack[count++] = AutoScriptData.V2L_Add(a, b);
                        }
                        break;

                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.Array:
                        {
                            Vector2[] result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }

            }
            if (count == 0)
                return null;

            return stack[--count];
        }

        /// <summary>
        /// V4L类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector4[] EvaluateRPNForV4L(RPN_TokenInfo[] rpn,
                                                    Func<RPN_TokenInfo, Vector4[]> operandResolver)
        {
            Vector4[][] stack = _methodV4LList;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAdd:
                        {
                            Vector4[] b = stack[--count];
                            Vector4[] a = stack[--count];
                            stack[count++] = AutoScriptData.V4L_Add(a, b);
                        }
                        break;

                    case RPN_TokenType.Variable:
                    case RPN_TokenType.MethodVar:
                    case RPN_TokenType.Array:
                        {
                            Vector4[] result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }

            }
            if (count == 0)
                return null;

            return stack[--count];
        }

        #endregion

        #region Condition
        // 将完整的条件表达式拆分到 (包含比较大小操作符的公式) 的粒度
        // 相当于只统计 "&&" 和 "||" 和其参与的括号
        public static List<string> ConditionTokenize(string expression)
        {
            var tokens = new List<string>();
            var chars = expression.ToCharArray();
            int len = chars.Length;

            int pre = 0;        //每次目标串的起点
            char last_char = '\0';
            int balance = 0;    //括号平衡
            int regular_bracket_count = 0;  // 多出来的正括号数
            int reverse_bracket_count = 0;  // 多出来的反括号数

            // (((a)&&(b))||(c))||(d)
            for (int i = 0; i <= len; i++)
            {
                bool isLast = i == len;

                char c = !isLast ? chars[i] : '\0';
                bool isAndOr = IsAndOr(last_char, c);

                if (c == '(') balance++;
                else if (c == ')') balance--;
                // 最后个变量的逻辑也相同，故带上
                else if (isAndOr || isLast)
                {
                    // 如果 balance == 1, 第一个字符一定是"(",后面是变量 （正确）
                    // 如果 balance == 2, 前两个字符一定是"(",后面是变量 （正确）
                    // 如果 balance == -1, 最后字符一定是")",前面是变量 （正确）


                    // 为了取到最后个变量，将i 定位到len + 1
                    if (isLast)
                        i = len + 1;

                    string variable = "";
                    // 添加括号
                    if (balance == 0)
                    {
                        variable = expression.Substring(pre, i - 1 - pre);
                    }
                    if (balance > 0)
                    {
                        regular_bracket_count = balance;
                        int start = pre + regular_bracket_count;
                        variable = expression.Substring(start, i - 1 - start);
                    }
                    else if (balance < 0)
                    {
                        reverse_bracket_count = -balance;
                        variable = expression.Substring(pre, i - 1 - pre - reverse_bracket_count);
                    }
                    // 添加括号
                    for (int k = 0; k < regular_bracket_count; k++)
                    {
                        tokens.Add("(");
                    }

                    // 变量
                    if (variable.Length > 0)
                        tokens.Add(variable);

                    // 添加括号
                    for (int k = 0; k < reverse_bracket_count; k++)
                    {
                        tokens.Add(")");
                    }

                    // 操作符
                    if (!isLast)
                        tokens.Add(new string(c, 2));

                    pre = i + 1;
                    balance = 0;
                    regular_bracket_count = 0;
                    reverse_bracket_count = 0;
                }
                last_char = c;
            }

            return tokens;
        }

        public static bool IsAndOr(char c0, char c1)
        {
            return (c0 == '&' && c1 == '&') || (c0 == '|' && c1 == '|');
        }

        /// <summary>
        /// float类型,计算逆波兰表达式的值
        /// </summary>
        public static bool EvaluateRPNForCondition(RPN_TokenInfo[] rpn, Func<RPN_TokenInfo, bool> operandResolver)
        {
            bool[] stack = _methodBoolList;
            int count = 0;

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case RPN_TokenType.OperatorAnd:
                        {
                            bool b = stack[--count];
                            bool a = stack[--count];
                            stack[count++] = a && b;
                        }
                        break;
                    case RPN_TokenType.OperatorOr:
                        {
                            bool b = stack[--count];
                            bool a = stack[--count];
                            stack[count++] = a || b;
                        }
                        break;
                    case RPN_TokenType.Condition:
                        {
                            bool result = operandResolver(token);
                            stack[count++] = result;
                        }
                        break;
                }
            }
            if (count == 0)
                return false;

            return stack[--count];
        }

        public static Regex conditionRegex = new Regex(@"(==|!=|>=|<=|>|<)");

        /// <summary>
        /// 分隔 比较操作符+左右表达式。如果没有比较操作符，就默认 var > 0
        /// </summary>
        public static void ConditionParseCompareOper(string formula, out string oper, out string left, out string right)
        {
            left = "";
            oper = "";
            right = "";

            var matches = conditionRegex.Matches(formula);
            if (matches.Count == 0)
                left = formula;

            else
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    var left_temp = formula.Substring(0, match.Index);
                    int count = left_temp.Count(c => c == '"');
                    // 比较符在""的外部还是内部，内部不做算
                    //
                    if (count % 2 == 0)
                    {
                        oper = match.Value;
                        left = left_temp;
                        right = formula.Substring(match.Index + oper.Length);
                        break;
                    }
                    else
                    {
                        left = formula;
                    }
                }
        }


        #endregion
    }
    #endregion
}