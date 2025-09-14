
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenCvSharp;
using Script.Framework.AssetLoader;
using Script.Framework.UI;
using Script.Util;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Script.Model.Auto
{
    /// <summary>
    /// Off,不在执行中。In,在执行中
    /// </summary>
    public enum NodeStatus
    {
        Off,
        In,
    }

    public enum NodeType
    {
        Base,               //基类，相当于none
        TemplateMatchOper,  //模版匹配
        MouseOper,          //鼠标操作
        KeyBoardOper,       //键盘操作
        AssignOper,         //赋值操作
        ConditionOper,      //条件判断操作
        TriggerEvent,       //触发事件
        ListenEvent,        //监听事件
    }

    #region AutoScriptSettings

    /// <summary>
    /// 脚本配置
    /// </summary>
    [Serializable]
    public class AutoScriptSettings
    {
        [JsonProperty("end_index")]
        public int EndIndex = -1;
        [JsonProperty("id_dic")]   // <id, 简化路径>
        public Dictionary<string, string> _idDic = new Dictionary<string, string>();
        [JsonProperty("collections")]
        public List<string> Collections = new List<string>();

        [JsonIgnore]  // <id, 完整路径>
        public Dictionary<string, string> IdDic = new Dictionary<string, string>();

        // 序列化 额外工作
        public void Serialize()
        {
            _idDic.Clear();
            foreach (var kv in IdDic)
            {
                _idDic[kv.Key] = kv.Value.Substring(Application.streamingAssetsPath.Length + 1);
            }
        }

        // 反序列化 额外工作
        public void UnSerialize()
        {
            IdDic.Clear();
            foreach (var kv in _idDic)
            {
                IdDic[kv.Key] = $"{Application.streamingAssetsPath}/{kv.Value}";
            }
        }

        public Dictionary<string, string> GetPath2Id()
        {
            var result = new Dictionary<string, string>();
            foreach (var kv in IdDic)
            {
                result[kv.Value] = kv.Key;
            }
            return result;
        }
    }


    #endregion
    #region AutoScriptConfig

    /// <summary>
    /// 脚本配置
    /// </summary>
    [Serializable]
    public class AutoScriptConfig
    {
        [JsonProperty("id")]
        public string Id = "";                  // 唯一性
        [JsonProperty("name")]
        public string Name = "";                // 名称，与文件名保持一致。效果上：文件名会覆盖Name
        [JsonProperty("first_node")]
        public string FirstNode = "";
        [JsonProperty("end_index")]
        public int EndIndex = -1;
        [JsonProperty("list")]
        public List<BaseNodeData> List = new List<BaseNodeData>();

        public static string IdStart = "script-";
    }


    #endregion
    #region BaseNodeData

    /// <summary>
    /// 一个流程节点
    /// 1.实现环环相扣思想。流入流出。严格按照时间线逻辑执行
    /// 2.流入事件 => Off转In
    /// 3.执行条件 = In状态 && timer >= delay && (外部事件)。 否则阻断，此节点保持In
    /// 4.执行内容:自由扩展,一般不耗时
    /// 5.流出行为: 
    /// In转Off。
    /// 计算执行结果bool。如果true则继续流入trueNextNodes；如果flase则继续流入flaseNextNodes
    /// 6.可以有多个流入节点，多个流出节点。一对一只能是true或者false关系
    /// 7.关于起点节点。起点有个标志.
    /// </summary>
    [Serializable]
    public class BaseNodeData
    {
        #region 序列化
        [JsonProperty("id")]
        public string Id;                  // id，用于查找
        [JsonProperty("name")]
        public string Name = "默认名";                 // 名称
        [JsonProperty("description")]
        public string Description = "";          // 备注
        [JsonProperty("pos")]
        public string Pos_Ser;                  // 序列化坐标
        /// <summary>
        /// 执行结果为true时的流出节点。
        /// </summary>
        [JsonProperty("true_next_nodes")]
        public List<string> TrueNextNodes = new List<string>();
        /// <summary>
        /// 执行结果为flase时的流出节点。
        /// </summary>
        [JsonProperty("false_next_nodes")]
        public List<string> FalseNextNodes = new List<string>();
        /// <summary>
        /// 延迟时间。相当于节点的总时长
        /// </summary>
        [JsonProperty("delay")]
        public float Delay = 1;

        #endregion

        #region 变量

        protected NodeType _nodeType;                     // 节点类型
        [JsonIgnore]
        public NodeType NodeType => _nodeType;
        [JsonIgnore]
        public List<string> LastNode = new List<string>();      // 上个节点
        [JsonIgnore]
        public Vector2 Pos;                             // 画布坐标
        [JsonIgnore]
        public float ExcuteTimes = 0;                   // 执行过多少次
        [JsonIgnore]
        public int Index = 0;                           // 节点创建顺序
        [JsonIgnore]
        protected AutoScriptData _scriptData;           // 脚本数据，管理者引用



        [JsonIgnore]
        public NodeStatus Status = NodeStatus.Off;      // 节点状态。In表示流程处在此节点，否则就是Off
        [JsonIgnore]
        public float Timer = 0;                         // 内部计时器.In时开始累加，直到 Delay执行一次内容
        [JsonIgnore]
        public bool CanAction => Timer >= Delay;        // 是否可以执行内容了 
        [JsonIgnore]
        public object InData;                           // 输入数据。由上个节点传入 
        [JsonIgnore]
        public object OutData;                          // 输出数据。传出给下个节点  

        #endregion



        public virtual void Init(AutoScriptData scriptData)
        {
            _scriptData = scriptData;
        }
        public virtual void AfterInit() { }

        // 执行内容
        public virtual void BeforeAction() { }
        // 执行内容
        public virtual void Action() { }
        // 执行结果判断方法
        public virtual bool GetResult()
        {
            return true;
        }

        public virtual void Clear()
        {
            InData = null;
            OutData = null;
        }


        // 流入行为
        public virtual void Inflow()
        {
            Status = NodeStatus.In;
            Timer = 0;
        }

        // 流出行为
        public virtual void Outflow()
        {
            Status = NodeStatus.Off;
            Timer = 0;
            ExcuteTimes++;
        }

        // 反序列化
        public virtual void UnSerialize()
        {
            var posArray = Pos_Ser.Substring(1, Pos_Ser.Length - 2).Split(',');  // 去掉括号,去掉逗号
            Pos = new Vector2(float.Parse(posArray[0]) * 100, float.Parse(posArray[1]) * 100);
            Index = int.Parse(Id.Split('-')[1]); // id格式为id-0, id-1, ...
        }
        // 序列化准备
        public virtual void Serialize()
        {
            Pos_Ser = $"({Math.Round(Pos.x / 100, 3)},{Math.Round(Pos.y / 100, 3)})";
        }

        public static string IdStart = "node-";
        public static string LineIdStart = "line-";
        public static string LineIdFormat(string from, string id) { return LineIdStart + $"{from}-{id}"; }


    }
    #region 子类






    #region TemplateNode

    /// <summary>
    /// 执行模版匹配-节点。
    /// 两个作用：
    /// 1.决定流程方向true or false。是否有满足条件的匹配结果
    /// 2.匹配位置输出到IN
    /// </summary>
    public class TemplateMatchOperNode : BaseNodeData
    {
        public static float ThresholdDefault = 0.95f;
        public static int CountDefault = 1;
        public static string RegionExpressionDefault = "Screen{}";

        [JsonProperty("template_path")]
        public string TemplatePath = "";
        [JsonProperty("region_expression")]
        public string RegionExpression = "";
        [JsonProperty("threshold")]
        public float Threshold;
        [JsonProperty("count")]
        public int Count;

        Vector4 _region;
        List<CVMatchResult> _result;
        public static TemplateMatchOperNode CreateNew()
        {
            var node = new TemplateMatchOperNode();
            node.Name = "默认名";
            node.Threshold = ThresholdDefault;
            node.Count = CountDefault;
            node.RegionExpression = RegionExpressionDefault;

            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.TemplateMatchOper;
        }

        public Vector4 GetRegion()
        {
            return _region;
        }

        public override void BeforeAction()
        {
            base.BeforeAction();
            _region = _scriptData.FormulaGetResultV4(RegionExpression);
        }

        public override void Action()
        {
            base.Action();

            Mat input = _scriptData.Manager.FrameCapture;
            if (input == null)
                return;

            string templatePath = ImageManager.GetFullPath(TemplatePath);
            if (!File.Exists(templatePath))
                return;

            Mat template = IU.GetMat(templatePath, true);
            CVRect inputRect = _scriptData.Manager.FrameCaptureRegion;

            // 匹配结果
            Mat resultMat = null;
            DU.RunWithTimer(() => resultMat = IU.MatchTemplate1(input, template), "MatchTemplate1");

            DU.RunWithTimer(() => _result = IU.FindResult(resultMat, template.Width, template.Height, Threshold), "FindResult");
            foreach (var item in _result)
            {
                item.Rect.x += inputRect.x;
                item.Rect.y += inputRect.y;
            }

            // 输出结果
            // 
            if (_result.Count == 1)
            {
                OutData = _result[0].Rect.ToVector4();
            }
            else if (_result.Count > 1)
            {
                var temp = new List<Vector4>();
                foreach (var item in _result)
                {
                    temp.Add(item.Rect.ToVector4());
                }
                OutData = temp;
            }


            // UI 展示
            //
            var draw = new List<CVMatchResult>(_result);
            draw.Add(new CVMatchResult() { Rect = _scriptData.Manager.FrameCaptureRegion, Score = 100 }); // 显示截屏范围

            UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { draw, 3.0f });
        }

        public override bool GetResult()
        {
            if (_result == null || _result.Count == 0)
                return false;

            return _result.Count >= Count;
        }

        public override void Clear()
        {
            base.Clear();
            _region = Vector4.zero;
            _result = null;
        }
    }

    #endregion


    #region MouseNode

    public class MouseOperNode : BaseNodeData
    {

        [JsonProperty("click_type")]
        public int clickType = 0;             // 0左键，1右键
        [JsonProperty("click_pos")]
        public string ClickPos = "";          // 点击坐标

        public static MouseOperNode CreateNew()
        {
            var node = new MouseOperNode();
            node.Name = "鼠标";
            node.clickType = 0;
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.MouseOper;

        }

        public override void Action()
        {
            base.Action();
            Vector2 pos = _scriptData.FormulaGetResultV2(ClickPos);
            int x = (int)pos.x;
            int y = (int)pos.y;

            // 移动鼠标
            //
            // WU.mouse_event(WU.MOUSEEVENTF_MOVE | WU.MOUSEEVENTF_ABSOLUTE
            //     , x * 65535 / 1920, y * 65535 / 1080, 0, UIntPtr.Zero);

            WU.SetCursorPos(x, y);
            WU.mouse_event(WU.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            WU.mouse_event(WU.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

    }

    #endregion

    #region KeyBoardNode

    public class KeyBoardOperNode : BaseNodeData
    {
        [JsonProperty("key")]
        public string Key = "";

        public static KeyBoardOperNode CreateNew()
        {
            var node = new KeyBoardOperNode();
            node.Name = "键盘";
            node.Key = AutoDataUIConfig.GetKeyboardName(AutoDataUIConfig.DefaultKeyboardEnum);
            return node;
        }
        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.KeyBoardOper;
        }

        public override void Action()
        {
            base.Action();
            int key = (int)AutoDataUIConfig.KeyboardName2Enum[Key];
            WU.keybd_event_packed(key, true);
            WU.keybd_event_packed(key, false);
        }
    }
    #endregion

    #region AssignNode
    /// <summary>
    /// 赋值操作-节点
    /// </summary>
    public class AssignOperNode : BaseNodeData
    {
        [JsonProperty("formula")]
        private string _formula = "";
        [JsonProperty("var_type")]
        private string _varTypeStr = "";


        [JsonIgnore]
        public string Formula
        {
            get { return _formula; }
            set
            {
                _scriptData.DeleteVarRef(this);
                _formula = value.Replace(" ", "");
                Refresh();
                CheckLegal();
                _scriptData.AddVarRef(this);
            }
        }

        private FormulaVarType _varTypeEnum;

        [JsonIgnore]
        public FormulaVarType VarType
        {
            get { return _varTypeEnum; }
            set
            {
                _scriptData.DeleteVarRef(this);
                _varTypeEnum = value;
                CheckLegal();
                _scriptData.AddVarRef(this);
            }
        }

        [JsonIgnore]
        public bool IsLegal = false;
        [JsonIgnore]
        public string VarName = "";
        [JsonIgnore]
        public string VarNameLower = "";
        [JsonIgnore]
        public string Expression = "";

        public static AssignOperNode CreateNew()
        {
            var node = new AssignOperNode();
            node.Name = "赋值";
            node.Delay = 0;
            node._varTypeEnum = FormulaVarType.Undefined;

            return node;
        }

        public override void Serialize()
        {
            base.Serialize();
            _varTypeStr = AutoDataUIConfig.VarType2Name[_varTypeEnum];
        }

        public override void UnSerialize()
        {
            base.UnSerialize();
            AutoDataUIConfig.VarName2Type.TryGetValue(_varTypeStr, out _varTypeEnum);
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.AssignOper;

            Refresh();
            _scriptData.AddVarRef(this);
        }
        public override void AfterInit()
        {
            CheckLegal();
        }

        public void Refresh()
        {
            var equal_index = Formula.IndexOf("=");
            if (equal_index > 0)
            {
                VarName = Formula.Substring(0, equal_index);
                VarNameLower = VarName.ToLower();
                Expression = Formula.Substring(equal_index + 1);
            }
            else
            {
                VarName = "";
                VarNameLower = "";
                Expression = "";
            }
        }

        public void CheckLegal()
        {
            if (VarType == FormulaVarType.Undefined || VarName == "")
            {
                IsLegal = false;
                return;
            }

            IsLegal = _scriptData.CheckFormula(VarNameLower, Expression, VarType);
        }


        public override void Action()
        {
            base.Action();
            if (VarName == "") return;

            // 赋值操作
            _scriptData.RunAssignFormula(VarName, Expression, VarType, InData);


            // 测试结果，大概是静态调用的500倍耗时。例子a = 1 比代码的a = 1 慢1000倍
            // Action action = () =>
            // {
            //     for (int i = 0; i < 10000; i++)
            //     {
            //         _scriptData.RunFormula(VarName, Expression, VarType);
            //     }
            // };

            // DU.RunWithTimer(action, $"AssignOperNode Action 10000次 ", 2);
        }


    }
    #endregion
    #region ConditionNode



    /// <summary>
    /// 条件判断-节点，两个浮点数表达式比较大小
    /// 先用 "&&| ||"分隔成多个ConditionCell，再对ConditionCell计算
    /// &&与||有优先顺序吗
    /// </summary>
    public class ConditionOperNode : BaseNodeData
    {
        [JsonProperty("formula")]
        private string _formula = "";

        [JsonIgnore]
        public string Formula
        {
            get { return _formula; }
            set
            {
                _formula = value.Replace(" ", "");
                // RPNCalculator.ParseConditionFormula(_formula, out Oper, out LeftExp, out RightExp);
                CheckLegal();

            }
        }

        [JsonIgnore]
        public bool IsLegal = false;
        [JsonIgnore]
        public string Oper = "";

        private bool _result = false;

        public static ConditionOperNode CreateNew()
        {
            var node = new ConditionOperNode();
            node.Name = "条件判断";
            node.Delay = 0;
            node._formula = "";
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.ConditionOper;
        }


        public void CheckLegal()
        {
            // var left_legal = AutoDataUIConfig.ExpressionIsLegal(LeftExp);
            // var right_legal = AutoDataUIConfig.ExpressionIsLegal(RightExp);

            // IsLegal = left_legal && right_legal;
        }

        public override void Action()
        {
            base.Action();
            _result = _scriptData.FormulaGetResultCondition(Formula);
        }

        public override bool GetResult()
        {
            return _result;
        }
    }

    public struct ConditionCell
    {
        public string Oper;       // 操作符
        public string LeftExp;    // 左表达式
        public string RightExp;   // 右表达式
        public ConditionCell(string oper, string leftExp, string rightExp)
        {
            Oper = oper;
            LeftExp = leftExp;
            RightExp = rightExp;
        }
    }

    #endregion

    #region TriggerEventN
    /// <summary>
    /// 不能流出,只能流入
    /// </summary>
    public class TriggerEventNode : BaseNodeData
    {
        [JsonProperty("event_name")]
        public string _eventName = "";
        [JsonIgnore]
        public string EventName
        {
            get { return _eventName; }
            set
            {
                _scriptData.RemoveEditTriggerNode(this);
                value = value.Replace(" ", "");
                _eventName = value;
                _scriptData.AddEditTriggerNode(this);
            }
        }
        public static TriggerEventNode CreateNew()
        {
            var node = new TriggerEventNode();
            node.Name = "触发事件";
            node.Delay = 0;
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.TriggerEvent;
            _scriptData.AddEditTriggerNode(this);
        }

        public override void Action()
        {
            base.Action();
            _scriptData.TriggerEvent(_eventName);
        }
    }
    #region ListenEventN
    /// <summary>
    /// 不能流入,只能流出
    /// 尝试 1.Inflow()后并在事件抛出时计时器才会开启
    /// </summary>
    public class ListenEventNode : BaseNodeData
    {
        [JsonProperty("event_name")]
        private string _eventName = "";
        [JsonIgnore]
        public string EventName
        {
            get { return _eventName; }
            set
            {
                _scriptData.RemoveListenNode(this);
                value = value.Replace(" ", "");
                _eventName = value;
                _scriptData.AddListenNode(this);
            }
        }
        public static ListenEventNode CreateNew()
        {
            var node = new ListenEventNode();
            node.Name = "监听事件";
            node.Delay = 0;
            return node;
        }
        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            _nodeType = NodeType.ListenEvent;
            _scriptData.AddListenNode(this);
        }

    }

    #endregion
    #endregion
    #endregion
    #endregion
    #region AutoScriptData
    public partial class AutoScriptData
    {
        public bool Running => _running;
        // 仅编辑模式下展示
        public Dictionary<string, List<TriggerEventNode>> Edit_TriggerNodes = new Dictionary<string, List<TriggerEventNode>>();
        public AutoScriptConfig Config;
        public AutoScriptManager Manager;
        public Dictionary<string, BaseNodeData> NodeDatas = new Dictionary<string, BaseNodeData>();
        public Dictionary<string, BaseNodeData> ActiveNodes = new Dictionary<string, BaseNodeData>();
        private Dictionary<string, List<ListenEventNode>> _listenNodes = new Dictionary<string, List<ListenEventNode>>();
        private bool _running = false;

        public AutoScriptData(AutoScriptConfig config, AutoScriptManager manager)
        {
            Config = config;
            Manager = manager;

            NodeDatas = new Dictionary<string, BaseNodeData>();
            for (int i = 0; i < Config.List.Count; i++)
            {
                var item = Config.List[i];
                item.UnSerialize();
                NodeDatas[item.Id] = item;
            }

            // 初始化每个节点的 LastNode
            foreach (var item in NodeDatas.Values)
            {
                item.Init(this);
                foreach (var id in item.TrueNextNodes)
                {
                    NodeDatas[id].LastNode.Add(item.Id);
                }

                foreach (var id in item.FalseNextNodes)
                {
                    NodeDatas[id].LastNode.Add(item.Id);
                }
            }

            foreach (var item in NodeDatas.Values)
            {
                item.AfterInit();
            }

        }

        /// <summary>
        /// pos:画布位置；id:如果为空则用顺延id来创建
        /// </summary>
        public BaseNodeData CreateNode(NodeType type, Vector2 pos, string id = null)
        {
            BaseNodeData node = null;
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                    node = TemplateMatchOperNode.CreateNew();
                    break;
                case NodeType.MouseOper:
                    node = MouseOperNode.CreateNew();
                    break;
                case NodeType.KeyBoardOper:
                    node = KeyBoardOperNode.CreateNew();
                    break;
                case NodeType.AssignOper:
                    node = AssignOperNode.CreateNew();
                    break;
                case NodeType.ConditionOper:
                    node = ConditionOperNode.CreateNew();
                    break;
                case NodeType.TriggerEvent:
                    node = TriggerEventNode.CreateNew();
                    break;
                case NodeType.ListenEvent:
                    node = ListenEventNode.CreateNew();
                    break;
            }


            if (string.IsNullOrEmpty(id))
            {
                Config.EndIndex++;
                id = BaseNodeData.IdStart + $"{Config.EndIndex}";
            }
            node.Id = id;
            node.Pos = pos;
            node.Init(this);
            node.AfterInit();


            NodeDatas[id] = node;

            return node;
        }
        public void DeleteNode(string id)
        {
            var node = NodeDatas[id];
            NodeDatas.Remove(id);
            //删线段
            foreach (var fromId in node.LastNode)
            {
                var fromNode = NodeDatas[fromId];
                fromNode.TrueNextNodes.Remove(id);
                fromNode.FalseNextNodes.Remove(id);
            }
            foreach (var toId in node.TrueNextNodes)
            {
                var toNode = NodeDatas[toId];
                toNode.LastNode.Remove(id);
            }
            foreach (var toId in node.FalseNextNodes)
            {
                var toNode = NodeDatas[toId];
                toNode.LastNode.Remove(id);
            }



            //如果是AssignOperNode,删除引用
            if (node is AssignOperNode)
            {
                DeleteVarRef((AssignOperNode)node);
            }
        }

        /// <summary>
        /// 按需给update切分成两个部分，因为中间要统计截图的范围
        /// </summary>
        public void BeforeUpdate(float deltaTime)
        {
            if (!_running) return;

            var list = ActiveNodes.Values.ToList();

            foreach (var n in list)
            {
                // 排除的情况，以后写
                n.Timer += deltaTime;
                if (n.CanAction)
                {
                    n.BeforeAction();
                }
            }
        }

        public void Update(float deltaTime)
        {
            if (!_running) return;

            var list = ActiveNodes.Values.ToList();

            foreach (var n in list)
            {
                if (n.CanAction)
                {
                    DU.RunWithTimer(() => n.Action(), $"节点 {n.Id} 执行", 2);
                    // n.Action();
                    TransferNode(n);
                }
            }

            if (ActiveNodes.Count == 0)
                _running = false;
        }


        public void StartScript()
        {
            if (NodeDatas.Count == 0)
            {
                DU.LogWarning("没有节点，无法启动");
                return;
            }

            _running = true;

            //如果起点被删了，修改Config.FirstNode
            if (Config.FirstNode == "" || !NodeDatas.ContainsKey(Config.FirstNode))
            {
                List<BaseNodeData> list = NodeDatas.Values.ToList();
                list.Sort((a, b) =>
                {
                    return a.Index.CompareTo(b.Index);
                });

                Config.FirstNode = list[0].Id;
            }

            // 清理节点内可能的上一把的缓存数据
            foreach (var n in NodeDatas.Values)
            {
                n.Clear();
            }

            // 设定起点
            StartNode(NodeDatas[Config.FirstNode]);
        }

        public void StopScript()
        {
            _running = false;
        }

        public void StartNode(BaseNodeData node)
        {
            if (node.Status == NodeStatus.In)
            {
                DU.LogWarning($"节点 {node.Id} 已经在执行中，不能重复启动");
                return;
            }

            ActiveNodes[node.Id] = node;
            node.Inflow();
            Manager.StatusChange?.Invoke(node.Id);
        }

        /// <summary>
        /// 流程链中行进节点，遇到delay=0的节点会一口气执行完
        /// </summary>
        public void TransferNode(BaseNodeData node)
        {
            node.Outflow();
            ActiveNodes.Remove(node.Id);
            bool result = node.GetResult();
            List<string> nextList = result ? node.TrueNextNodes : node.FalseNextNodes;
            foreach (var id in nextList)
            {
                var next = NodeDatas[id];
                next.InData = node.OutData;
                StartNode(next);
                if (next.CanAction)
                {
                    next.BeforeAction();
                    DU.RunWithTimer(() => next.Action(), $"节点 {next.Id} 执行", 2);
                    // next.Action();
                    TransferNode(next);
                }
            }

            Manager.StatusChange?.Invoke(node.Id);
        }

        public string Save()
        {
            //_autoData.List用_nodeData重新序列化,其他字段不动

            List<BaseNodeData> list = NodeDatas.Values.ToList();
            list.Sort((a, b) =>
            {
                return a.Index.CompareTo(b.Index);
            });

            foreach (var n in list)
            {
                n.Serialize();
            }


            Config.List = list;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            string json = JsonConvert.SerializeObject(Config, settings);
            return json;


        }


        public void AddListenNode(ListenEventNode node)
        {
            if (node.EventName == "") return;

            if (!_listenNodes.TryGetValue(node.EventName, out var list))
            {
                list = new List<ListenEventNode>();
                _listenNodes[node.EventName] = list;
            }
            list.Add(node);
        }

        public void RemoveListenNode(ListenEventNode node)
        {
            if (_listenNodes.TryGetValue(node.EventName, out var list))
            {
                list.Remove(node);
            }
        }

        // 下帧才开始生效
        public void TriggerEvent(string eventName)
        {
            if (_listenNodes.TryGetValue(eventName, out var list))
            {
                foreach (var node in list)
                {
                    if (node.Status == NodeStatus.Off)
                    {
                        StartNode(node);
                    }
                }
            }
        }

        public List<ListenEventNode> GetListenNodes(string eventName)
        {
            if (_listenNodes.TryGetValue(eventName, out var list))
            {
                return list;
            }
            return new List<ListenEventNode>();
        }



        public void AddEditTriggerNode(TriggerEventNode node)
        {
            if (node.EventName == "") return;

            if (!Edit_TriggerNodes.TryGetValue(node.EventName, out var list))
            {
                list = new List<TriggerEventNode>();
                Edit_TriggerNodes[node.EventName] = list;
            }
            list.Add(node);
        }

        public void RemoveEditTriggerNode(TriggerEventNode node)
        {
            if (Edit_TriggerNodes.TryGetValue(node.EventName, out var list))
            {
                list.Remove(node);
            }
        }

        public List<TriggerEventNode> GetEditTriggerNodes(string eventName)
        {
            if (Edit_TriggerNodes.TryGetValue(eventName, out var list))
            {
                return list;
            }
            return new List<TriggerEventNode>();
        }
    }

    #endregion


    #region Manager

    /// <summary>
    /// 总控。
    /// </summary>
    public partial class AutoScriptManager
    {
        private static AutoScriptManager _inst;
        public static AutoScriptManager Inst
        { get { if (_inst == null) _inst = new AutoScriptManager(); return _inst; } }
        public static string AutoScriptDirectoryPath = $"{Application.streamingAssetsPath}/Script";
        public static string AutoScriptSettingsPath = $"{Application.streamingAssetsPath}/Script/settings.json";
        public static string FolderIdStart = "folder-";
        public static string ScriptIdStart = "script-";
        public static string NodeIdStart = "node-";
        public static string LineIdStart = "line-";
        public event Action Tick;      //通知ui刷新
        public Action<string> StatusChange;  //通知ui刷新
        public bool InfoPanelFolded = true;


        public AutoScriptSettings Settings;

        /// <summary>
        /// 脚本目录.<id, (path,fileName)>
        /// </summary>
        private Dictionary<string, (string, string)> _scriptDirectory = new Dictionary<string, (string, string)>();
        /// <summary>
        /// 加载过的脚本.<id, Data>
        /// </summary>
        private Dictionary<string, AutoScriptData> _scriptDatas = new Dictionary<string, AutoScriptData>();

        private CVRect _frameCaptureRegion = default;   // 当前帧屏幕截屏范围
        private Mat _frameCapture = null;               // 当前帧屏幕截屏

        AutoScriptManager()
        {
            Init();
        }
        void Init()
        {
            LoadScriptJsonDirectory();
            // _scriptData = Simulate();
        }

        // public AutoScriptData Simulate()
        // {
        //     //模拟数据
        //     int count = 10;
        //     var autoScriptConfig = new AutoScriptConfig
        //     {
        //         Name = "New Auto Script",
        //         FirstNode = BaseNodeData.IdStart + "0",
        //         EndIndex = count - 1,
        //         List = new List<BaseNodeData>()
        //     };
        //     var scriptData = new AutoScriptData(autoScriptConfig, this);

        //     scriptData.NodeDatas = new Dictionary<string, BaseNodeData>();
        //     for (int i = 0; i < count; i++)
        //     {
        //         var id = BaseNodeData.IdStart + $"{i}";
        //         BaseNodeData item = null;
        //         if (i < 9)
        //         {
        //             item = new TemplateMatchOperNode();
        //         }
        //         else
        //         {
        //             item = new MouseOperNode();
        //             ((MouseOperNode)item).click = 0;
        //         }
        //         _scriptData.NodeDatas[id] = item;
        //         item.Id = id;
        //         item.Index = i;
        //         item.Pos = new Vector2(200 + i * 300, 300);
        //         item.Delay = 5 + Random.Range(0, 2);

        //         if (i - 1 >= 0)
        //             item.LastNode.Add(BaseNodeData.IdStart + $"{i - 1}");
        //         if (i + 1 < count)
        //             item.TrueNextNodes.Add(BaseNodeData.IdStart + $"{i + 1}");
        //     }
        //     return scriptData;
        // }




        #region 脚本

        public AutoScriptData GetScriptData(string id)
        {
            if (!_scriptDirectory.TryGetValue(id, out (string, string) tuple))
            {
                DU.LogError($"没有找到脚本 {id}");
                return null;
            }
            string path = tuple.Item1;
            if (!_scriptDatas.TryGetValue(id, out var data))
            {
                data = LoadScriptJson(path);
                _scriptDatas[id] = data;
                // 检查名字和文件名一致
                //
                data.Config.Name = tuple.Item2;
            }

            return data;
        }

        public bool IsRuning(string id)
        {
            return _scriptDatas[id].Running;
        }


        public void StartScript(string id)
        {
            var scriptData = _scriptDatas[id];
            if (!scriptData.Running)
            {
                scriptData.StartScript();
            }
        }


        public void StopScript(string id)
        {
            _scriptDatas[id].StopScript();
        }

        public void OnUpdate(float deltaTime)
        {
            // 预更新
            foreach (var script in _scriptDatas.Values)
            {
                script.BeforeUpdate(deltaTime);
            }
            // 按需把AutoScriptData.Update切分成两个部分，因为中间要统计截图的范围
            _frameCaptureRegion = CalculateFrameCaptureRegion();

            // 更新
            foreach (var script in _scriptDatas.Values)
            {
                script.Update(deltaTime);
            }
            // 使FrameCapture获取最新的帧截屏
            _frameCapture = null;

            // 通知UI刷新
            Tick?.Invoke();
        }
        // 当前帧屏幕截屏
        public Mat FrameCapture
        {
            get
            {
                if (_frameCapture == null && _frameCaptureRegion != default)
                {
                    Bitmap bitmap = null;
                    Action action = () =>
                    {
                        bitmap = WU.CaptureWindow(_frameCaptureRegion);
                    };
                    Action action1 = () =>
                    {
                        _frameCapture = IU.BitmapToMat(bitmap);
                    };
                    DU.RunWithTimer(action, "CaptureWindow");   // 25ms
                    DU.RunWithTimer(action1, "BitmapToMat");    // 0ms
                }
                return _frameCapture;
            }
        }
        public CVRect FrameCaptureRegion => _frameCaptureRegion;

        // 计算所有脚本中，所有模版匹配节点的区域的并集
        CVRect CalculateFrameCaptureRegion()
        {
            List<CVRect> regions = new List<CVRect>();
            foreach (var script in _scriptDatas.Values)
            {
                foreach (var node in script.ActiveNodes.Values)
                {
                    if (node.CanAction && node is TemplateMatchOperNode templateNode)
                    {
                        var r = templateNode.GetRegion();
                        regions.Add(new CVRect(r.x, r.y, r.z, r.w));
                    }
                }
            }

            // 计算并集, 有左上角和右下角两点决定矩形。
            Vector2 left_top = default;
            Vector2 right_bottom = default;

            foreach (var r in regions)
            {
                var lt = r.LeftTop;
                var rb = r.RightBottom;
                // lt在left_top左上方，则要更新
                if (left_top == default || (lt.x < left_top.x && lt.y < left_top.y))
                {
                    left_top = lt;
                }

                if (right_bottom == default || (rb.x > right_bottom.x && rb.y > right_bottom.y))
                {
                    right_bottom = rb;
                }
            }

            return new CVRect(left_top, right_bottom);
        }

        public void RenameScript(string id, string newName)
        {
            if (!_scriptDirectory.TryGetValue(id, out (string, string) tuple))
            {
                DU.LogError($"没有找到脚本 {id}");
                return;
            }
            string oldPath = tuple.Item1;
            string dir = Path.GetDirectoryName(oldPath).Replace("\\", "/");
            string newPath = $"{dir}/{newName}.json";

            // 重命名文件
            if (File.Exists(newPath))
            {
                DU.LogError($"脚本重命名失败，文件已存在 {newPath}");
                return;
            }
            File.Move(oldPath, newPath);

            // 更新目录
            _scriptDirectory[id] = (newPath, newName);
            Settings.IdDic[id] = newPath;
            SaveAutoScriptSettings();

            // 更新数据
            var scriptData = _scriptDatas[id];
            scriptData.Config.Name = newName;

            DU.Log($"脚本重命名成功");
        }

        /// <summary>
        /// 创建脚本。返回：成功否，脚本id
        /// </summary>
        public bool CreateScript(string name, string dir, out string id)
        {
            id = AutoScriptConfig.IdStart + (Settings.EndIndex + 1);
            var path = $"{Application.streamingAssetsPath}/{dir}/{name}.json";
            // 重命名文件
            if (File.Exists(path))
            {
                DU.LogError($"创建脚本失败，文件已存在 {path}");
                return false;
            }

            Settings.EndIndex++;

            Settings.IdDic[id] = path;
            SaveAutoScriptSettings();

            _scriptDirectory[id] = (path, name);

            var scriptData = new AutoScriptData(new AutoScriptConfig
            {
                Id = id,
                Name = name,
            }, this);
            _scriptDatas[id] = scriptData;
            SaveScript(id);

            return true;
        }

        // 编辑界面 每两分钟保存一次，并且OnDisable时保存。
        public void SaveScript(string id)
        {
            var scriptData = _scriptDatas[id];
            var json = scriptData.Save();

            // 保存到文件。
            string path = _scriptDirectory[id].Item1;
            File.WriteAllText(path, json);

            // 这个耗时以后测测看
            //
            // DU.LogWarning($"{scriptData.Config.Name} 脚本保存成功 至 {path}");
        }

        #region 加载
        // StreamingAssets/Script 目录下存所有的json脚本
        // StreamingAssets/Script/config.json 是此类型文件的配置

        public void LoadScriptJsonDirectory()
        {
            // 文件夹
            if (!Directory.Exists(AutoScriptDirectoryPath))
            {
                Directory.CreateDirectory(AutoScriptDirectoryPath);
            }
            // 加载settings
            Settings = LoadAutoScriptSettings();
            var path2id = Settings.GetPath2Id();

            // 遍历目录下所有文件的名    todo  封装同步两个列表的方法
            string[] files = Directory.GetFiles(AutoScriptDirectoryPath, "*.json", SearchOption.AllDirectories);
            var files_set = new HashSet<string>();
            foreach (string _ in files)
            {
                // 排除掉 settings.json 总配置
                if (_.EndsWith("settings.json"))
                    continue;
                string path = _.Replace("\\", "/");
                files_set.Add(path);
            }


            // 删无效的
            foreach (var kv in path2id)
            {
                if (!files_set.Contains(kv.Key))
                    Settings.IdDic.Remove(kv.Value);
            }

            // 更新
            foreach (string path in files_set)
            {
                if (path2id.TryGetValue(path, out string id))
                {
                    _scriptDirectory[id] = (path, Path.GetFileNameWithoutExtension(path));
                }
                else
                {
                    // 新增的脚本
                    Settings.EndIndex++;
                    id = AutoScriptConfig.IdStart + Settings.EndIndex;
                    Settings.IdDic[id] = path;
                    _scriptDirectory[id] = (path, Path.GetFileNameWithoutExtension(path));
                }
            }

            // 保存settings
            SaveAutoScriptSettings();
        }

        public AutoScriptSettings LoadAutoScriptSettings()
        {
            if (File.Exists(AutoScriptSettingsPath))
            {
                string json = File.ReadAllText(AutoScriptSettingsPath);
                var autoScriptConfig = JsonConvert.DeserializeObject<AutoScriptSettings>(json);
                autoScriptConfig.UnSerialize();
                return autoScriptConfig;
            }
            else
            {
                return new AutoScriptSettings();
            }
        }

        void SaveAutoScriptSettings()
        {
            Settings.Serialize();
            var json = JsonConvert.SerializeObject(Settings);
            File.WriteAllText(AutoScriptSettingsPath, json);
        }


        public AutoScriptData LoadScriptJson(string path)
        {
            string json = File.ReadAllText(path);
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            var autoScriptConfig = JsonConvert.DeserializeObject<AutoScriptConfig>(json, settings);
            var scriptData = new AutoScriptData(autoScriptConfig, this);
            return scriptData;
        }


        #endregion

        #region 搜索

        public List<string> BrowseDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "Script";

            List<string> r = new List<string>();

            path = $"{Application.streamingAssetsPath}/{path}";

            // 获取所有子文件夹
            string[] directories = Directory.GetDirectories(path);
            foreach (string _ in directories)
            {
                var dir = _.Replace("\\", "/");
                r.Add(FolderIdStart + dir);
            }

            // 获取所有文件
            var path2id = Settings.GetPath2Id();

            string[] files = Directory.GetFiles(path, "*.json");
            foreach (string _ in files)
            {
                var file_path = _.Replace("\\", "/");
                if (path2id.ContainsKey(file_path))
                    r.Add(path2id[file_path]);
            }

            return r;
        }


        public List<string> SearchScripts(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return new List<string>();

            List<string> r = new List<string>();
            foreach (var kv in _scriptDirectory)
            {
                if (kv.Value.Item2.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    r.Add(kv.Key);
                }
            }
            Utils.CommonSort(r);
            return r;
        }

        #endregion
        #endregion

        #region 节点

        public BaseNodeData GetNode(AutoScriptData scriptData, string id)
        {
            return scriptData.NodeDatas[id];
        }

        public BaseNodeData CreateNode(AutoScriptData scriptData, NodeType type, Vector2 pos, string id = null)
        {
            return scriptData.CreateNode(type, pos, id);
        }

        public void DeleteNode(AutoScriptData scriptData, string id)
        {
            scriptData.DeleteNode(id);
        }

        public float GetNodeMinDalay(NodeType type)
        {
            switch (type)
            {
                case NodeType.TemplateMatchOper:
                case NodeType.MouseOper:
                case NodeType.KeyBoardOper:
                    return 0.01f;
                case NodeType.AssignOper:
                case NodeType.ConditionOper:
                case NodeType.TriggerEvent:
                case NodeType.ListenEvent:
                    return 0;
                default:
                    return 0.01f;
            }
        }

        #endregion


        #region 线段

        public void AddLine(AutoScriptData scriptData, string fromId, string toId, bool isTrue)
        {
            var fromNode = GetNode(scriptData, fromId);
            var toNode = GetNode(scriptData, toId);

            var list = isTrue ? fromNode.TrueNextNodes : fromNode.FalseNextNodes;
            var other = isTrue ? fromNode.FalseNextNodes : fromNode.TrueNextNodes;

            if (!list.Contains(toId))
                list.Add(toId);

            if (other.Contains(toId))
                other.Remove(toId);

            toNode.LastNode.Add(fromId);
        }

        public void DeleteLine(AutoScriptData scriptData, string fromId, string toId)
        {
            var fromNode = GetNode(scriptData, fromId);
            var toNode = GetNode(scriptData, toId);

            fromNode.TrueNextNodes.Remove(toId);
            fromNode.FalseNextNodes.Remove(toId);


            toNode.LastNode.Remove(fromId);
        }


        public HashSet<string> GetLinesByNode(AutoScriptData scriptData, string id)
        {
            BaseNodeData node = GetNode(scriptData, id);

            HashSet<string> r = new HashSet<string>();
            foreach (var nextId in node.TrueNextNodes)
            {
                r.Add(BaseNodeData.LineIdFormat(id, nextId));
            }
            foreach (var nextId in node.FalseNextNodes)
            {
                r.Add(BaseNodeData.LineIdFormat(id, nextId));
            }
            foreach (var fromId in node.LastNode)
            {
                r.Add(BaseNodeData.LineIdFormat(fromId, id));
            }

            return r;
        }

        #endregion
    }

    #endregion
}