
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.Model.Auto
{

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

        // 缓存解析的结果
        static Dictionary<string, List<string>> _cache = new Dictionary<string, List<string>>();
        // 缓存方法解析的结果
        static Dictionary<string, (string, string[])> _methodCache = new Dictionary<string, (string, string[])>();
        // 缓存浮点数解析的结果
        static Dictionary<string, float> _floatCache = new Dictionary<string, float>();
        // 缓存访问字段解析的结果
        static Dictionary<string, string[]> _accessCache = new Dictionary<string, string[]>();

        public static void Clear()
        {
            _cache.Clear();
            _methodCache.Clear();
            _floatCache.Clear();
            _accessCache.Clear();
        }

        /// <summary>
        /// 获取逆波兰表达式
        /// </summary>
        public static List<string> GetRPN(string expression, bool isCondition = false)
        {
            if (!_cache.TryGetValue(expression, out var result))
            {
                List<string> rpn = InfixToRPN(expression, isCondition);
                _cache[expression] = rpn;
                result = rpn;
            }

            return result;
        }


        static Stack<string> operatorStack_I = new Stack<string>();   //优化-复用
        // 将中缀表达式转换为逆波兰表达式
        public static List<string> InfixToRPN(string expression, bool isCondition = false)
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

            return outputQueue;
        }

        static System.Text.RegularExpressions.Regex _regex =
        new System.Text.RegularExpressions.Regex(@"[+\-*/()]");


        // 将表达式字符串分割成 运算符与操作数
        public static List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            var matches = _regex.Matches(expression);

            // 大括号包含的内容不解析，整个方法变量当成一个整体，留着后续去处理。实现方法变量内嵌套运算符
            var temp = expression;
            int index = -1;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var oper = match.Value;
                index = temp.IndexOf(oper, index + 1);          //一定能找到

                // 实现嵌套方法
                var front = temp.Substring(0, index);
                if (!CheckBracket(front))
                {
                    continue;
                }

                tokens.Add(front);                              //操作数——常量或变量
                tokens.Add(oper);                              //运算符

                temp = temp.Substring(index + oper.Length);    //即使传入temp.Length属于特殊情况不会报错，返回空串
                index = -1;
            }

            if (temp.Length > 0)
                tokens.Add(temp);                           //最后剩余的操作数


            return tokens;
        }

        public static bool CheckBracket(string expression)
        {
            int balance = 0;
            foreach (char c in expression)
            {
                if (c == '{') balance++;
                else if (c == '}') balance--;

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
        public static void GetMethodParseResult(string expression, out string method_name, out string[] param_list)
        {
            if (!_methodCache.TryGetValue(expression, out var result))
            {
                result = ParseMethodParam(expression);
                _methodCache[expression] = result;
            }

            method_name = result.Item1;
            param_list = result.Item2;
        }

        // Add{1,Add{1,1}}  "{}"内的","不能分割
        public static (string, string[]) ParseMethodParam(string source)
        {
            // method_name = "Add";
            // param_list = new string[2] { "1", "1" };

            var front = source.IndexOf("{");
            // 优化。反括号一定在末尾
            // var back = source.LastIndexOf("}");
            var back = source.Length - 1;
            var method_name = source.Substring(0, front);
            var expression = source.Substring(front + 1, back - front - 1);

            var result = new List<string>();
            char[] chars = expression.ToCharArray();
            int pre = 0;        //每次目标串的起点
            int balance = 0;
            int len = chars.Length;
            for (int i = 0; i < len; i++)
            {
                char c = chars[i];
                if (c == '{') balance++;
                else if (c == '}') balance--;
                else if (c == ',' && balance == 0)
                {
                    var variable = expression.Substring(pre, i - pre);
                    result.Add(variable);
                    pre = i + 1;
                }
            }
            if (pre < len)
                result.Add(expression.Substring(pre, len - pre));

            var param_list = result.ToArray();
            return (method_name, param_list);
        }

        public static bool IsFloat(string expression, out float result)
        {
            if (_floatCache.TryGetValue(expression, out result))
            {
                return true;
            }

            bool isFloat = float.TryParse(expression, out result);
            if (isFloat)
            {
                _floatCache[expression] = result;
            }

            return isFloat;
        }

        public static string[] GetAccessParse(string expression)
        {
            if (!_accessCache.TryGetValue(expression, out var result))
            {
                result = expression.Split('.');
                _accessCache[expression] = result;
            }

            return result;
        }




        #region EvaluateRPN

        static Stack<float> _stack_EvaluateRPN = new Stack<float>();   //优化，快3ms
        /// <summary>
        /// float类型,计算逆波兰表达式的值
        /// </summary>
        public static float EvaluateRPN(List<string> rpn, Func<string, float> operandResolver)
        {
            Stack<float> stack = _stack_EvaluateRPN;
            stack.Clear();

            foreach (var token in rpn)
            {
                if (IsOperator(token))
                {
                    float b = stack.Pop();
                    float a = stack.Pop();
                    switch (token)
                    {
                        case "+":
                            stack.Push(a + b);
                            break;
                        case "-":
                            stack.Push(a - b);
                            break;
                        case "*":
                            stack.Push(a * b);
                            break;
                        case "/":
                            stack.Push(a / b);
                            break;
                    }
                }
                else
                {
                    //处理操作数
                    bool isFloat = IsFloat(token, out var number);
                    if (isFloat)
                    {
                        stack.Push(number);
                    }
                    else
                    {
                        float result = operandResolver(token);
                        stack.Push(result);
                    }
                }
            }
            if (stack.Count == 0)
                return 0;

            return stack.Pop();
        }


        /// <summary>
        /// Vector2类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector2 EvaluateRPNForVector2(List<string> rpn, Func<string, Vector2> operandResolver)
        {
            Stack<Vector2> stack = new Stack<Vector2>();

            foreach (var token in rpn)
            {
                if (IsOperator(token))
                {
                    Vector2 b = stack.Pop();
                    Vector2 a = stack.Pop();
                    switch (token)
                    {
                        case "+":
                            stack.Push(a + b);
                            break;
                        case "-":
                            stack.Push(a - b);
                            break;
                    }
                }
                else
                {
                    if (operandResolver == null)       //有问题就返回0
                        return Vector2.zero;

                    Vector2 result = operandResolver(token);
                    stack.Push(result);
                }
            }
            if (stack.Count == 0)
                return Vector2.zero;

            return stack.Pop();
        }


        /// <summary>
        /// Vector3类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector3 EvaluateRPNForVector3(List<string> rpn, Func<string, Vector3> operandResolver)
        {
            Stack<Vector3> stack = new Stack<Vector3>();

            foreach (var token in rpn)
            {
                if (IsOperator(token))
                {
                    Vector3 b = stack.Pop();
                    Vector3 a = stack.Pop();
                    switch (token)
                    {
                        case "+":
                            stack.Push(a + b);
                            break;
                        case "-":
                            stack.Push(a - b);
                            break;

                    }
                }
                else
                {
                    Vector3 result = operandResolver(token);
                    stack.Push(result);
                }
            }
            if (stack.Count == 0)
                return Vector3.zero;

            return stack.Pop();
        }

        /// <summary>
        /// Vector4类型,计算逆波兰表达式的值
        /// </summary>
        public static Vector4 EvaluateRPNForVector4(List<string> rpn, Func<string, Vector4> operandResolver)
        {
            Stack<Vector4> stack = new Stack<Vector4>();

            foreach (var token in rpn)
            {
                if (IsOperator(token))
                {
                    Vector4 b = stack.Pop();
                    Vector4 a = stack.Pop();
                    switch (token)
                    {
                        case "+":
                            stack.Push(a + b);
                            break;
                        case "-":
                            stack.Push(a - b);
                            break;

                    }
                }
                else
                {
                    Vector4 result = operandResolver(token);
                    stack.Push(result);
                }
            }
            if (stack.Count == 0)
                return Vector4.zero;

            return stack.Pop();
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
        public static bool EvaluateRPNForCondition(List<string> rpn, Func<string, bool> operandResolver)
        {
            Stack<bool> stack = new Stack<bool>();

            foreach (var token in rpn)
            {
                if (IsOperator(token))
                {
                    bool b = stack.Pop();
                    bool a = stack.Pop();
                    switch (token)
                    {
                        case "&&":
                            stack.Push(a && b);
                            break;
                        case "||":
                            stack.Push(a || b);
                            break;
                    }
                }
                else
                {
                    bool result = operandResolver(token);
                    stack.Push(result);
                }
            }
            if (stack.Count == 0)
                return false;

            return stack.Pop();
        }

        static System.Text.RegularExpressions.Regex _conditionRegex =
        new System.Text.RegularExpressions.Regex(@"(==|>=|<=|>|<)");

        /// <summary>
        /// 分隔 操作符 与 左右表达式
        /// </summary>
        public static void ConditionParseCompareOper(string formula, out string oper, out string left, out string right)
        {
            left = "";
            oper = "";
            right = "";

            var matches = _conditionRegex.Matches(formula);
            if (matches.Count != 1)
                return;

            oper = matches[0].Value;
            var index = formula.IndexOf(oper);
            left = formula.Substring(0, index);
            right = formula.Substring(index + oper.Length);
        }
        #endregion
    }
}