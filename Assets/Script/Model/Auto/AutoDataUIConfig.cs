
using System.Collections.Generic;
using System.Text;
using Script.Util;

namespace Script.Model.Auto
{
    #region UI

    public static class AutoDataUIConfig
    {
        public static List<NodeType> NodeTypes = new List<NodeType>()
        {
            NodeType.TemplateMatchOper,
            NodeType.MouseOper,
            NodeType.KeyBoardOper,
            NodeType.AssignOper,
            NodeType.ConditionOper,
            NodeType.TriggerEvent,
            NodeType.ListenEvent,
            NodeType.MapCapture,
            NodeType.StopScript,
        };

        public static Dictionary<NodeType, string> NodeTypeNames = new Dictionary<NodeType, string>()
        {
            { NodeType.TemplateMatchOper, "模版匹配" },
            { NodeType.MouseOper, "鼠标" },
            { NodeType.KeyBoardOper, "键盘" },
            { NodeType.AssignOper, "赋值" },
            { NodeType.ConditionOper, "条件" },
            { NodeType.TriggerEvent, "触发事件" },
            { NodeType.ListenEvent, "监听" },
            { NodeType.MapCapture, "地图截图" },
            { NodeType.StopScript, "暂停脚本" },
        };

        public static List<string> GetNodeTypeNameList()
        {
            var list = new List<string>();
            foreach (var type in NodeTypes)
            {
                list.Add(NodeTypeNames[type]);
            }

            return list;
        }

        public static string GetNodeId(BaseNodeData node)
        {
            return "id:" + node.Id.Substring(5);
        }

        #region 键盘UI
        public static Dictionary<KeyboardEnum, string> KeyboardEnum2Name = new Dictionary<KeyboardEnum, string>()
        {
            { KeyboardEnum.Esc, "Esc" },
            { KeyboardEnum.Tab, "Tab" },
            { KeyboardEnum.CapsLock, "CapsLock" },
            { KeyboardEnum.Shift, "Shift" },
            { KeyboardEnum.Ctrl, "Ctrl" },
            { KeyboardEnum.Alt, "Alt" },
            { KeyboardEnum.Space, "Space" },
            { KeyboardEnum.Enter, "Enter" },
            { KeyboardEnum.Backspace, "Backspace" },
            { KeyboardEnum.Delete, "Delete" },
            { KeyboardEnum.Insert, "Insert" },
            { KeyboardEnum.Home, "Home" },
            { KeyboardEnum.End, "End" },
            { KeyboardEnum.PageUp, "PageUp" },
            { KeyboardEnum.PageDown, "PageDown" },
            { KeyboardEnum.LeftWin, "Win" },
            { KeyboardEnum.Menu, "Menu" },
            { KeyboardEnum.Up, "Up" },
            { KeyboardEnum.Down, "Down" },
            { KeyboardEnum.Left, "Left" },
            { KeyboardEnum.Right, "Right" },
            { KeyboardEnum.D0, "0" },
            { KeyboardEnum.D1, "1" },
            { KeyboardEnum.D2, "2" },
            { KeyboardEnum.D3, "3" },
            { KeyboardEnum.D4, "4" },
            { KeyboardEnum.D5, "5" },
            { KeyboardEnum.D6, "6" },
            { KeyboardEnum.D7, "7" },
            { KeyboardEnum.D8, "8" },
            { KeyboardEnum.D9, "9" },
            { KeyboardEnum.A, "A" },
            { KeyboardEnum.B, "B" },
            { KeyboardEnum.C, "C" },
            { KeyboardEnum.D, "D" },
            { KeyboardEnum.E, "E" },
            { KeyboardEnum.F, "F" },
            { KeyboardEnum.G, "G" },
            { KeyboardEnum.H, "H" },
            { KeyboardEnum.I, "I" },
            { KeyboardEnum.J, "J" },
            { KeyboardEnum.K, "K" },
            { KeyboardEnum.L, "L" },
            { KeyboardEnum.M, "M" },
            { KeyboardEnum.N, "N" },
            { KeyboardEnum.O, "O" },
            { KeyboardEnum.P, "P" },
            { KeyboardEnum.Q, "Q" },
            { KeyboardEnum.R, "R" },
            { KeyboardEnum.S, "S" },
            { KeyboardEnum.T, "T" },
            { KeyboardEnum.U, "U" },
            { KeyboardEnum.V, "V" },
            { KeyboardEnum.W, "W" },
            { KeyboardEnum.X, "X" },
            { KeyboardEnum.Y, "Y" },
            { KeyboardEnum.Z, "Z" },
        };
        public static Dictionary<string, KeyboardEnum> _keyboardName2Enum;
        public static Dictionary<string, KeyboardEnum> _keyboardLowercaseName2Enum;
        public static Dictionary<string, KeyboardEnum> KeyboardName2Enum
        {
            get
            {
                if (_keyboardName2Enum == null)
                {
                    _keyboardName2Enum = new Dictionary<string, KeyboardEnum>();
                    foreach (var kvp in KeyboardEnum2Name)
                    {
                        _keyboardName2Enum[kvp.Value] = kvp.Key;
                    }
                }
                return _keyboardName2Enum;
            }
        }
        public static Dictionary<string, KeyboardEnum> KeyboardLowercaseName2Enum
        {
            get
            {
                if (_keyboardLowercaseName2Enum == null)
                {
                    _keyboardLowercaseName2Enum = new Dictionary<string, KeyboardEnum>();
                    foreach (var kvp in KeyboardEnum2Name)
                    {
                        _keyboardLowercaseName2Enum[kvp.Value.ToLower()] = kvp.Key;
                    }
                }
                return _keyboardLowercaseName2Enum;
            }
        }

        public static KeyboardEnum DefaultKeyboardEnum = KeyboardEnum.Space; // 默认键盘枚举

        public static string GetKeyboardName(KeyboardEnum key)
        {
            if (KeyboardEnum2Name.TryGetValue(key, out var name))
            {
                return name;
            }
            return "未存在";
        }
        public static KeyboardEnum GetKeyboardEnum(string name)
        {

            if (KeyboardName2Enum.TryGetValue(name, out var key))
            {
                return key;
            }

            return DefaultKeyboardEnum;
        }

        public static List<string> GetKeyboardMatchList(string search)
        {
            List<string> r = new List<string>();
            if (string.IsNullOrEmpty(search))
                return r;

            search = search.ToLower();
            foreach (var kvp in KeyboardLowercaseName2Enum)
            {
                if (kvp.Key.Contains(search))
                {
                    var s = GetKeyboardName(kvp.Value);
                    r.Add(s);
                }
            }
            Utils.CommonSort(r);
            return r;
        }

        public static bool IsLegalKeyboardName(string name)
        {
            return KeyboardName2Enum.ContainsKey(name);
        }


        #endregion

        #region 鼠标UI

        public static List<string> MouseClickTypes = new List<string>()
        {
            "左键",
            "右键",
            "移动",
        };

        #endregion


        #region 赋值UI

        public static List<FormulaVarType> VarTypes = new List<FormulaVarType>()
        {
            FormulaVarType.Undefined,
            FormulaVarType.Float,
            FormulaVarType.Vector2,
            FormulaVarType.Vector4,
        };

        public static Dictionary<string, FormulaVarType> VarName2Type = new Dictionary<string, FormulaVarType>()
        {
            { "undef", FormulaVarType.Undefined },
            { "float", FormulaVarType.Float },
            { "vector2", FormulaVarType.Vector2 },
            { "vector4", FormulaVarType.Vector4 },
        };

        private static Dictionary<FormulaVarType, string> _varType2Name;

        public static Dictionary<FormulaVarType, string> VarType2Name
        {
            get
            {
                if (_varType2Name == null)
                {
                    _varType2Name = new Dictionary<FormulaVarType, string>();
                    foreach (var kvp in VarName2Type)
                        _varType2Name[kvp.Value] = kvp.Key;
                }
                return _varType2Name;
            }
        }

        private static List<string> _varTypeNames;
        public static List<string> VarTypeNames
        {
            get
            {
                if (_varTypeNames == null)
                {
                    _varTypeNames = new List<string>();
                    foreach (var type in VarTypes)
                        _varTypeNames.Add(VarType2Name[type]);
                }
                return _varTypeNames;
            }
        }

        #region 格式化

        static HashSet<string> _symbols = new HashSet<string>()
        {
            "+", "-", "*", "/", "=", ",","(",")", ">", "<", ">=", "<=", "==", "!=", "&&", "||"
            ,".","{","}"
        };
        static HashSet<string> _operators = new HashSet<string>()
        {
            "+", "-", "*", "/"
        };
        static HashSet<string> _conditionConnect = new HashSet<string>()
        {
            "&&", "||"
        };

        public static bool IsSymbol(string token)
        {
            return _symbols.Contains(token);
        }

        /// <summary>
        /// 格式化后用于UI显示的表达式—补充括号
        /// </summary>
        public static string FormulaFormat(string formula)
        {
            var list = TokenizeFormat(formula);
            var result = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                var token = list[i];
                if (IsSymbol(token))
                {
                    if (token == "(" || token == ")" || token == "{" || token == "}" || token == ".")
                        result.Append(token);
                    else
                        result.Append($" {token} ");
                }
                else
                {
                    result.Append(token);
                }
            }

            return result.ToString();
        }

        static System.Text.RegularExpressions.Regex _tokenizeRegex =
            new System.Text.RegularExpressions.Regex(@">=|<=|==|!=|&&|\|\||[+\-*/()=,><.\{\}]");
        // 将表达式字符串分割成 运算符与操作数。一次性全部完成
        public static List<string> TokenizeFormat(string expression)
        {
            var tokens = new List<string>();
            var matches = _tokenizeRegex.Matches(expression);

            var temp = expression;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var s = match.Value;
                var index = temp.IndexOf(s);

                if (index > 0)
                {
                    var front = temp.Substring(0, index);
                    tokens.Add(front);   //操作数——常量或变量
                }

                tokens.Add(s);                              //运算符

                temp = temp.Substring(index + s.Length);    //即使传入temp.Length属于特殊情况不会报错，返回空串
            }

            if (temp.Length > 0)
                tokens.Add(temp);                           //最后剩余的操作数


            return tokens;
        }

        #endregion

        // 搜索匹配的变量名
        public static List<string> GetAssignMatchList(string search, Dictionary<string, FormulaVarInfo> varRef)
        {
            List<string> r = new List<string>();
            if (string.IsNullOrEmpty(search))
                return r;

            search = search.ToLower();
            foreach (var tuple in varRef)
            {
                var var_name = tuple.Key;
                var var_info = tuple.Value;
                if (var_name.Contains(search))
                {
                    r.Add(var_info.VarName);
                }
            }
            Utils.CommonSort(r);
            return r;
        }

        // 提取在编辑中的表达式中最后一个关键词
        // 光标放到 带有"."目前会失效
        public static void GetAssignKeyword(string formula, int cursorPos, out string keyword, out int startIndex)
        {
            keyword = "";
            startIndex = 0;

            var index = cursorPos - 1;
            var list = TokenizeFormat(formula);
            if (list.Count == 0)
                return;

            int start = 0;
            var last_token = list[0];
            foreach (var token in list)
            {
                if (index - start - token.Length < 0)
                {
                    last_token = token;
                    break;
                }

                start += token.Length;
            }

            if (IsSymbol(last_token))
                return;

            keyword = last_token;
            startIndex = start;
        }

        // 不含"="表达式
        public static bool ExpressionIsLegal(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return false;

            List<string> tokens = RPNCalculator.Tokenize(formula);

            int parenCount = 0;
            string lastToken = null;
            foreach (var token in tokens)
            {
                if (token == "(") parenCount++;
                else if (token == ")") parenCount--;
                if (parenCount < 0) return false; // 右括号多于左括号

                if (_operators.Contains(token) && (lastToken == null || _operators.Contains(lastToken)))
                    return false; // 连续运算符或开头是运算符

                var is_method = token.IndexOf("{") >= 0;
                if (is_method)
                {
                    if (token.IndexOf("}") < 0) return false; // 不完整
                    // 方法变量
                    RPNCalculator.GetMethodParseResult(token, out string method_name, out string[] param_list);
                    foreach (var param in param_list)
                    {
                        if (!ExpressionIsLegal(param))
                            return false;
                    }
                }

                lastToken = token;
            }
            if (parenCount != 0) return false; // 括号不配对
            if (_operators.Contains(lastToken)) return false; // 结尾不配对

            return true;
        }

        public static bool ConditionIsLegal(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return false;

            List<string> tokens = RPNCalculator.ConditionTokenize(formula);

            int parenCount = 0;
            string lastToken = null;
            foreach (var token in tokens)
            {
                if (token == "(") parenCount++;
                else if (token == ")") parenCount--;
                if (parenCount < 0) return false; // 右括号多于左括号

                if (_conditionConnect.Contains(token) && (lastToken == null || _conditionConnect.Contains(lastToken)))
                    return false; // 连续运算符或开头是运算符

                if (!_conditionConnect.Contains(token) && token != "(" && token != ")")
                {
                    RPNCalculator.ConditionParseCompareOper(token, out var oper, out var left, out var right);
                    if (oper == "")
                        return false;   // 不含比较符

                    if (!ExpressionIsLegal(left) || !ExpressionIsLegal(right))
                        return false;   // 表达式非法

                }

                lastToken = token;
            }
            if (parenCount != 0) return false; // 括号不配对
            if (_conditionConnect.Contains(lastToken)) return false; // 结尾不配对

            return true;
        }
        #endregion
    }
    #endregion
}