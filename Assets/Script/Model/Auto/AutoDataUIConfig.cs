
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Script.Util;

namespace Script.Model.Auto
{

    public static class AutoDataUIConfig
    {
        public static List<(NodeType, string)> NodeTypes = new List<(NodeType, string)>()
        {
            (NodeType.CaptureOper,SU.JieTu),
            (NodeType.TemplateMatchOper,SU.MoBanPiPei),
            (NodeType.AssignOper,SU.FuZhi),
            (NodeType.ConditionOper,SU.TiaoJianPanDuan),
            (NodeType.ForOper,SU.XunHuan),
            (NodeType.MouseOper,SU.ShuBiao),
            (NodeType.KeyBoardOper,SU.JianPan),
            (NodeType.WaitOper,SU.DengDai),
            (NodeType.TriggerEvent,SU.ChuFaShiJian),
            (NodeType.ListenEvent,SU.JianTingShiJian),
            (NodeType.StopScript,SU.ZanTingJiaoBen),
            (NodeType.MapCapture,SU.DiTuShiBie),
            (NodeType.MapPathFinding,SU.DiTuXunLu),
            (NodeType.ItemGridRecog,SU.WuPingGeShiBie),

        };

        public static List<string> GetNodeTypeNameList()
        {
            var list = NodeTypes.ConvertAll(t => SU.GetString(t.Item2));
            return list;
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
            { KeyboardEnum.Comma, "," },
            { KeyboardEnum.Period, "." },
            { KeyboardEnum.Semicolon, ";" },
            { KeyboardEnum.Quote, "'" },
            { KeyboardEnum.Slash, "/" },
            { KeyboardEnum.Backslash, "\\" },
            { KeyboardEnum.BracketLeft, "[" },
            { KeyboardEnum.BracketRight, "]" },
            { KeyboardEnum.Minus, "-" },
            { KeyboardEnum.Equals, "=" },
            { KeyboardEnum.Grave, "`" },
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


        public static List<KeyBoardOperType> KeyboardOperTypes = new List<KeyBoardOperType>()
        {
            KeyBoardOperType.FullPress,
            KeyBoardOperType.KeyDown,
            KeyBoardOperType.KeyUp,
        };

        public static Dictionary<KeyBoardOperType, string> KeyboardOperTypeNames = new Dictionary<KeyBoardOperType, string>()
        {
            { KeyBoardOperType.FullPress, SU.QiaoJian },
            { KeyBoardOperType.KeyDown, SU.ZhiAnXia },
            { KeyBoardOperType.KeyUp, SU.ZhiTaiQi },

        };

        public static List<string> GetKeyboardOperTypeNameList()
        {
            var list = new List<string>();
            foreach (var type in KeyboardOperTypes)
            {
                list.Add(SU.GetString(KeyboardOperTypeNames[type]));
            }
            return list;
        }


        #endregion

        #region 鼠标UI

        public static List<string> MouseClickTypes = new List<string>()
        {
            SU.ZuoJian,
            SU.YouJian,
            SU.YiDong,
        };

        #endregion


        #region 赋值UI

        /// <summary>
        /// 保持本块结构
        /// </summary>
        public static List<FormulaVarType> VarTypes = new List<FormulaVarType>()
        {
            FormulaVarType.Undefined,
            FormulaVarType.Float,
            FormulaVarType.Bool,
            FormulaVarType.Vector2,
            FormulaVarType.Vector4,
            FormulaVarType.ListFloat,
            FormulaVarType.ListVector2,
            FormulaVarType.ListVector4,
            FormulaVarType.String,
            FormulaVarType.ListString,
        };

        public static Dictionary<string, FormulaVarType> VarName2Type = new Dictionary<string, FormulaVarType>()
        {
            { "undef", FormulaVarType.Undefined },
            { "float", FormulaVarType.Float },
            { "bool", FormulaVarType.Bool },
            { "vector2", FormulaVarType.Vector2 },
            { "vector4", FormulaVarType.Vector4 },
            { "string", FormulaVarType.String },
            { "float[]", FormulaVarType.ListFloat },
            { "vector2[]", FormulaVarType.ListVector2 },
            { "vector4[]", FormulaVarType.ListVector4 },
            { "string[]", FormulaVarType.ListString },
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

        public static FormulaVarType ConvertVarType(FormulaVarType type, bool has_bracket)
        {
            if (has_bracket)
                switch (type)
                {
                    case FormulaVarType.ListFloat: return FormulaVarType.Float;
                    case FormulaVarType.ListVector2: return FormulaVarType.Vector2;
                    case FormulaVarType.ListVector4: return FormulaVarType.Vector4;
                    case FormulaVarType.ListString: return FormulaVarType.String;
                }
            return type;
        }

        #endregion


        #region 地图寻路UI

        public static List<(PathFindingType, string)> MapPFTypes = new List<(PathFindingType, string)>()
        {
            (PathFindingType.Undefined, SU.Wu),
            (PathFindingType.ExploreFog, SU.TanSuoMiWu) ,
            (PathFindingType.FollowPlayer, SU.GenSuiMuBiao) ,
            (PathFindingType.ReachPos, SU.QuWangMuDiDi) ,
        };


        public static List<string> GetMapPFTypeNameList()
        {
            var list = new List<string>();
            foreach (var type in MapPFTypes)
            {
                list.Add(SU.GetString(type.Item2));
            }

            return list;
        }
        #endregion
        #region 格式化

        static HashSet<string> _symbols = new HashSet<string>()
        {
            "+", "-", "*", "/", "=", ",","(",")", "==", "!=", ">", "<", ">=", "<=", "&&", "||"
            ,".","{","}",":","?",";"
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
                string front = i > 0 ? list[i - 1] : "";
                string back = i < list.Count - 1 ? list[i + 1] : "";

                var token = list[i];
                if (IsSymbol(token))
                {
                    if (token == "(" || token == ")"  || token == "}" || token == "."
                        || token == "\"")
                        result.Append(token);
                    else if (token == ";" || token == "," || token == "{")
                        result.Append($"{token} ");
                    else if (front == "\"" || back == "\"")
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

        static Regex _tokenizeRegex = new Regex(@">=|<=|==|!=|&&|\|\||\[|\]|[+\-*/()=,><.\{\}:\?;""]");
        // 将表达式字符串分割成 运算符与操作数。一次性全部完成
        public static List<string> TokenizeFormat(string expression)
        {
            var tokens = new List<string>();
            var matches = _tokenizeRegex.Matches(expression);

            var temp = expression;

            int cursor = 0;
            foreach (Match match in matches)
            {
                var oper = match.Value;
                var index = match.Index - cursor;

                // 如果"-"后面是数字, 则跳过
                if (oper == "-" && index == 0)
                    continue;

                if (index > 0)
                {
                    var front = temp.Substring(0, index);
                    tokens.Add(front);   //操作数——常量或变量
                }

                tokens.Add(oper);                              //运算符

                int next_start = index + oper.Length;
                temp = temp.Substring(next_start);    //即使传入temp.Length属于特殊情况不会报错，返回空串
                cursor += next_start;
            }

            if (temp.Length > 0)
                tokens.Add(temp);                           //最后剩余的操作数


            return tokens;
        }

        #endregion

        #region Other
        // 搜索匹配的变量名
        // 要求: 除了开头第一个字母不变，小写变量名能搜出来大写的变量名
        public static List<TipMatchItem> GetAssignMatchList(string search, List<TipMatchItem> varRef)
        {
            int MatchLimit = 20;

            List<TipMatchItem> r = new List<TipMatchItem>();
            if (string.IsNullOrEmpty(search))
                return r;

            if (search.Length > 1)
                search = search.Substring(0, 1) + search.Substring(1, search.Length - 1).ToLower();

            foreach (TipMatchItem match in varRef)
            {
                if (match.MatchStr.StartsWith(search))
                    r.Add(match);
                if (r.Count >= MatchLimit)
                    break;
            }

            r.Sort((aI, bI) =>
            {
                var a = aI.OriStr;
                var b = bI.OriStr;
                if (a.Length != b.Length)
                    return a.Length.CompareTo(b.Length);
                else
                    return string.Compare(a, b, StringComparison.Ordinal);
            });
            return r;
        }

        // 搜索匹配的事件名，编辑监听节点的事件名时提示已存在的事件。
        // 要求: 小写变量名能搜出来大写的变量名
        public static List<string> GetEventMatchList(string search
                    , Dictionary<string, (List<TriggerEventNode>, string, bool)> varRef)
        {
            List<string> r = new List<string>();
            if (string.IsNullOrEmpty(search))
                return r;

            search = search.ToLower();

            foreach (var pair in varRef)
            {
                var event_name = pair.Key;
                var event_lower_name = pair.Value.Item2;
                if (event_lower_name.Contains(search))
                    r.Add(event_name);
            }

            Utils.CommonSort(r);
            return r;
        }

        // 提取在编辑中的表达式中最后一个关键词
        // 光标放到带有"." 目前会失效
        public static void GetAssignKeyword(string formula, int cursorPos,
                                            out string keyword, out int startIndex, out string back)
        {
            keyword = "";
            back = "";
            startIndex = 0;

            var index = cursorPos - 1;
            var list = TokenizeFormat(formula);
            if (list.Count == 0)
                return;

            int start = 0;
            var last_token = list[0];
            var key_index = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var token = list[i];
                if (index - start - token.Length < 0)
                {
                    last_token = token;
                    key_index = i;
                    break;
                }

                start += token.Length;
            }

            if (IsSymbol(last_token))
                return;

            keyword = last_token;
            startIndex = start;
            if (key_index + 1 < list.Count)
            {
                back = list[key_index + 1];
            }
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

                // 以后要换思路写了
                // var is_method = token.IndexOf("{") >= 0;
                // if (is_method)
                // {
                //     if (token.IndexOf("}") < 0) return false; // 不完整
                //     // 方法变量
                //     RPNCalculator.GetMethodParseResult(token, out _, out string[] param_list);
                //     foreach (var param in param_list)
                //     {
                //         if (!ExpressionIsLegal(param))
                //             return false;
                //     }
                // }

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

    public struct TipMatchItem
    {
        public string OriStr;
        public string MatchStr;
        public int Type;            // 0-变量；1-方法名

        public TipMatchItem(string oriStr, string matchStr, int type)
        {
            OriStr = oriStr; MatchStr = matchStr; Type = type;
        }
    }
}