
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
using Rect = OpenCvSharp.Rect;

namespace Script.Model.Auto
{
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
        public string Id;                       // id，用于查找
        [JsonProperty("name")]
        protected string Name = "默认名";       // 名称
        [JsonProperty("description")]
        private string Description = "";        // 备注
        [JsonProperty("canvas_id")]
        public string CanvasId = "0";           // 所属画布，兼容的话就是"0"，是默认页
        [JsonProperty("pos")]
        public string Pos_Ser;                  // 序列化坐标

        [JsonProperty("delay")]
        public float Delay = 1;                 // 延迟时间。相当于节点的总时长

        /// <summary>
        /// 节点连接情况，由本节点流出的连接
        /// </summary>
        [JsonProperty("links")]
        public List<NodeLinkInfo> Raw_Links = new List<NodeLinkInfo>();


        [JsonProperty("true_next_nodes")]       // (已废弃，只做兼容) 执行结果为true时的流出节点。
        public List<string> TrueNextNodes = new List<string>();

        [JsonProperty("false_next_nodes")]      // (已废弃，只兼容) 执行结果为flase时的流出节点。
        public List<string> FalseNextNodes = new List<string>();


        #endregion

        #region 变量
        protected AutoScriptData _scriptData;               // 脚本数据，属于哪个脚本
        protected NodeType _nodeType;                       // 节点类型
        [JsonIgnore] public NodeType NodeType => _nodeType;
        // 节点连接情况。单节点流入到另一节点只能一条线。
        [JsonIgnore] public Dictionary<string, NodeLinkInfo> Links = new Dictionary<string, NodeLinkInfo>();
        // 节点连接情况, 反向角度。
        [JsonIgnore] public Dictionary<string, NodeLinkInfo> LastNode = new Dictionary<string, NodeLinkInfo>();
        // 节点的每个门 含连线数。
        [JsonIgnore] public int[] Door_LineCount = new int[4];
        [JsonIgnore] public HashSet<string> LineIds = new HashSet<string>();            // 线段列表
        [JsonIgnore] public HashSet<string> LineNodeIds = new HashSet<string>();        // 线段关联的节点id列表
        [JsonIgnore] public Vector2 Pos;                             // 画布坐标，坐标系—左上角为原点,X右,Y下
        [JsonIgnore] public int Index = 0;                           // 节点创建顺序
        [JsonIgnore] public string IndexStr;                         // Index的string类型

        // 运行时数据
        [JsonIgnore] public NodeStatus Status = NodeStatus.Off;      // 节点状态。In表示流程处在此节点，否则就是Off
        [JsonIgnore] public float Timer = 0;                         // 内部计时器.In时开始累加，直到 Delay执行一次内容
        [JsonIgnore] public float ExcuteTimes = 0;                   // 执行过多少次
        [JsonIgnore] public NodeDoor InflowDoor;                     // 运行时，流入的门
        [JsonIgnore] public string ExcuteLastNodeId;                 // 运行时，上个节点id
        [JsonIgnore] public FormulaVarInfo InData;                   // 输入数据。由上个节点传入 
        [JsonIgnore] public FormulaVarInfo OutData;                  // 输出数据。传出给下个节点  
        [JsonIgnore] public bool CanAction => Timer >= Delay;        // 是否可以执行内容了 

        #endregion

        /// <summary>
        /// 序列化之后调用Init, 子类必须重载。 属于节点配置初始化
        /// ——注意BaseNodeData共有3个初始化时机
        /// Init() 属于节点配置的初始化
        /// Clean() 属于脚本开始运行的初始化。是重置整个脚本的清理。
        /// Inflow() 属于节点每次重新流入的初始化，因为设计上节点可以重复进入。是一种清理。
        /// </summary>
        public virtual void Init(AutoScriptData scriptData)
        {
            _scriptData = scriptData;
            _nodeType = AutoScriptData.GetNodeType(this);
            Index = int.Parse(Id); // id格式为 数字
            IndexStr = $"{Index}";
            _scriptData.Canvas2Node[CanvasId].Add(Id);

            AutoScriptData.SetRules(this);
            scriptData.initLastNode(this);
        }

        /// <summary>
        /// Init之后AfterInit
        /// </summary>
        public virtual void AfterInit()
        {
            RefreshLineIds();
        }


        /// <summary>
        /// 删除节点时调用OnDelete，作用：系统中还原注册
        /// </summary>
        public virtual void OnDelete()
        {
            _scriptData.Canvas2Node[CanvasId].Remove(Id);
        }

        // 执行内容前
        public virtual void BeforeAction() { }
        // 执行内容
        public virtual void Action() { }
        // 帧更新
        public virtual void Update() { }

        // 流出方向。
        public virtual bool GetResult()
        {
            return true;
        }

        /// <summary>
        /// 注意BaseNodeData共有3个初始化时机
        /// Init() 属于节点配置的初始化
        /// Clean() 属于节点运行状态的初始化。是重置整个脚本的清理。
        /// Inflow() 属于节点运行时重新流入的初始化，因为设计上节点可以重复进入。是一种清理。
        /// </summary>
        public virtual void Clean()
        {
            Status = NodeStatus.Off;
            Timer = 0;
            ExcuteTimes = 0;
            ExcuteLastNodeId = null;
            InData = default;
            OutData = default;
        }


        /// <summary>
        /// 流入行为，可以做小清理
        /// </summary>
        public virtual void Inflow()
        {
            Status = NodeStatus.In;
            Timer = 0;
        }

        // 流出行为
        public virtual void Outflow()
        {
            Status = NodeStatus.Off;
            ExcuteTimes++;
        }

        // 反序列化
        public virtual void UnSerialize()
        {
            var posArray = Pos_Ser.Substring(1, Pos_Ser.Length - 2).Split(',');  // 去掉括号,去掉逗号
            Pos = new Vector2(float.Parse(posArray[0]) * 100, float.Parse(posArray[1]) * 100);

            Links.Clear();
            LastNode.Clear();
            {   // 兼容
                if (TrueNextNodes.Count > 0)
                {
                    foreach (string oi in TrueNextNodes)
                        AddLink(NodeDoor.OutTrue, oi.StartsWith("node-") ? oi.Substring(5) : oi, NodeDoor.In);
                    TrueNextNodes.Clear();
                }

                if (FalseNextNodes.Count > 0)
                {
                    foreach (string oi in FalseNextNodes)
                        AddLink(NodeDoor.OutFalse, oi.StartsWith("node-") ? oi.Substring(5) : oi, NodeDoor.In);
                    FalseNextNodes.Clear();
                }

                Id = Id.StartsWith("node-") ? Id.Substring(5) : Id;
            }

            foreach (var data in Raw_Links)
            {
                var oi = data.OtherId;
                oi = oi.StartsWith("node-") ? oi.Substring(5) : oi;
                AddLink(data.SelfDoor, oi, data.OtherDoor);
            }
        }
        // 序列化准备
        public virtual void Serialize()
        {
            Pos_Ser = $"({Math.Round(Pos.x / 100, 3)},{Math.Round(Pos.y / 100, 3)})";
            Raw_Links.Clear();
            foreach (var data in Links.Values)
                Raw_Links.Add(data);
        }

        /// <summary>
        /// 实现接口时，争取做到只用 拷贝序列化相关的字段
        /// </summary>
        public virtual void Copy(BaseNodeData source)
        {
            Name = source.Name;
            Description = source.Description;
            Delay = source.Delay;
        }


        public void RefreshLineIds()
        {
            LineIds.Clear();
            LineNodeIds.Clear();
            // 随着Link变化, 同步LineIds
            foreach (var other_id in Links.Keys)
            {
                LineIds.Add(LineIdFormat(Id, other_id));
                LineNodeIds.Add(other_id);
            }

            foreach (var fromId in LastNode.Keys)
            {
                LineIds.Add(LineIdFormat(fromId, Id));
                LineNodeIds.Add(fromId);
            }
        }


        public void AddLink(NodeDoor self_door, string other_id, NodeDoor other_door)
        {
            if (self_door == NodeDoor.OutTrue || self_door == NodeDoor.OutFalse)
                Links[other_id] = new NodeLinkInfo(self_door, other_id, other_door);
            else
                LastNode[other_id] = new NodeLinkInfo(self_door, other_id, other_door);

            Door_LineCount[(int)self_door]++;
        }
        public void RemoveLink(NodeDoor self_door, string other_id)
        {
            if (self_door == NodeDoor.OutTrue || self_door == NodeDoor.OutFalse)
                Links.Remove(other_id);
            else
                LastNode.Remove(other_id);

            Door_LineCount[(int)self_door]--;
        }

        public List<NodeLinkInfo> GetLinksByDoor(NodeDoor self_door)
        {
            var result = new List<NodeLinkInfo>();
            if (self_door == NodeDoor.OutTrue || self_door == NodeDoor.OutFalse)
            {
                foreach (var info in Links.Values)
                    if (info.SelfDoor == self_door)
                        result.Add(info);
            }
            else
            {
                foreach (var info in LastNode.Values)
                    if (info.SelfDoor == self_door)
                        result.Add(info);
            }
            return result;
        }

        public void SetName(string value)
        {
            Name = SU.SaveString(value);
        }

        public string GetName()
        {
            return SU.GetString(Name);
        }

        public string GetShowName()
        {
            return $"[{Id}] {SU.GetString(Name)}";
        }
        public void SetDescription(string value)
        {
            Description = SU.SaveString(value);
        }

        public string GetDescription()
        {
            return SU.GetString(Description);
        }

        public static string LineIdFormat(string from, string id) { return $"{from}-{id}"; }
    }
    #endregion

    #region 子类







    #region CaptureOper

    /// <summary>
    /// 截图节点。只提供拍摄范围参数，无关图像应用
    /// 按按钮能够显示截图区域
    /// </summary>

    public class CaptureOperNode : BaseNodeData
    {
        [JsonProperty("regions_expression")]
        public string RegionsExpression = "";


        [JsonIgnore] public bool SaveCaptureToLocal = false;    // 只在程序启动时可开启，不存储是怕忘了关

        [JsonIgnore] public Vector4[] Regions;
        [JsonIgnore] public Vector4 TotalRegion;

        bool _needCloseDebug;

        public static CaptureOperNode CreateNew()
        {
            var node = new CaptureOperNode();
            node.Name = SU.JieTu;
            node.RegionsExpression = "";
            return node;
        }


        public override void Inflow()
        {
            base.Inflow();
            _needCloseDebug = false;
            CheckCloseDebug();
        }

        public override void BeforeAction()
        {
            base.BeforeAction();
            Regions = _scriptData.FormulaGetResultV4L(RegionsExpression);
            TotalRegion = RecogUtil.CalBoundingBox(Regions);
        }


        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as CaptureOperNode;
            RegionsExpression = sourceObj.RegionsExpression;
        }

        public override void Clean()
        {
            base.Clean();
            Regions = null;
            TotalRegion = Vector4.zero;
        }

        public override void Update()
        {
            base.Update();
            CheckCloseDebug();

            if (_needCloseDebug)
                _scriptData.Manager.ScreenDrawAllow = false;
        }

        void CheckCloseDebug()
        {
            if (Delay - Timer < Utils.OneFrame && !_needCloseDebug)
            {
                _needCloseDebug = true;
                _scriptData.Manager.ScreenDrawAllow = false;

                // Timer += 10; //debug，延后功能没问题

                // 关闭屏幕绘制的同时又要执行模版匹配是无效的，
                // 需要让模版匹配延后一帧
                if (CanAction)
                {
                    Timer = Delay - Utils.MinFrameTime;
                }
            }
        }
    }

    #endregion
    #region TemplateNode

    /// <summary>
    /// 执行模版匹配。图像应用。(内存泄漏检查合格)
    /// 两个作用：
    /// 1.决定流程方向true or false。是否有满足条件的匹配结果
    /// 2.匹配位置输出到IN
    /// </summary>
    public class TemplateMatchOperNode : BaseNodeData
    {
        public static float ThresholdDefault = 0.98f;   //普通截图一般都是1.0
        public static int CountDefault = 1;

        ////////// debug
        [JsonIgnore] public float Meet_min_score = 2;
        [JsonIgnore] public float Unmeet_max_score = -1;
        //////////

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
            node.Name = SU.MoBanPiPei;
            node.Threshold = ThresholdDefault;
            node.Count = CountDefault;
            node.RegionExpression = AutoScriptData.RegionExpressionDefault;

            return node;
        }



        public override void BeforeAction()
        {
            base.BeforeAction();
            _region = _scriptData.FormulaGetResultV4(RegionExpression);
        }

        // 所有的节点 _region汇总，相当于给 Manager 提截屏需求。Manager最后计算并集FrameCaptureRegion
        // 返回 Mat FrameCapture；节点再input.SubMat(Region)截自己的需求部分
        public override void Action()
        {
            base.Action();


            string templatePath = ImageManager.GetFullPath(TemplatePath);
            if (!File.Exists(templatePath))
                return;

            CVRect inputRect = _scriptData.Manager.FrameCaptureRegion;

            CVMatchResult max_score_r = null;
            _result = null;
            // 匹配结果

            using (Mat template = IU.GetMat(templatePath, true))
            {
                int t_width = template.Width;
                int t_height = template.Height;
                if (template.Width == 0)
                {
                    DU.MessageBox($"模版图片格式不对：{templatePath}");
                    return;
                }

                // DU.RunWithTimer(() =>
                // {
                _scriptData.Manager.UseCapture(_region, (capture) =>
                {
                    using (Mat resultMat = IU.MatchTemplate1(capture, template))
                    {
                        if (resultMat == null)
                            return;

                        // DU.RunWithTimer(() =>
                        // {
                        _result = IU.FindResult(resultMat, t_width, t_height, Threshold, out max_score_r);
                        // }, "FindResult");
                    }
                });
                if (_result == null)
                    return;
                // }, "MatchTemplate1");
            }

            // 转到脚本坐标系
            foreach (var item in _result)
            {
                item.Rect.x += inputRect.x;
                item.Rect.y += inputRect.y;
            }

            // 输出结果
            // 
            if (Count == 1)
            {
                if (_result.Count > 0)
                {
                    _result.Sort((a, b) => b.Score.CompareTo(a.Score));
                    var r = _result[0];
                    OutData = new FormulaVarInfo(FormulaVarType.Vector4, r.Rect.ToVector4());

                    Meet_min_score = Mathf.Min(Meet_min_score, r.Score);
                }
                else
                {
                    Unmeet_max_score = Mathf.Max(Unmeet_max_score, max_score_r.Score);
                }
            }
            else if (Count > 1)
            {
                var temp = new List<Vector4>();
                foreach (var item in _result)
                {
                    temp.Add(item.Rect.ToVector4());
                }
                OutData = new FormulaVarInfo(FormulaVarType.ListVector4, temp);
            }

            // UI 展示
            //
            if (_scriptData.Manager.ScreenDrawDebug)
            {
                var draw = new List<CVMatchResult>(_result);
                draw.Insert(0, new CVMatchResult() { Rect = _scriptData.Manager.FrameCaptureRegion, UIType = 1 }); // 显示截屏范围

                // 为了知道最高分数在哪
                if (_result.Count == 0)
                {
                    max_score_r.Rect.x += inputRect.x;
                    max_score_r.Rect.y += inputRect.y;
                    max_score_r.UIType = 2;
                    draw.Add(max_score_r);
                }

                UIManager.Inst.ShowPanel(PanelEnum.TemplateMatchDrawResultPanel, new List<object> { draw, 3.0f });
            }
        }

        public override bool GetResult()
        {
            if (_result == null || _result.Count == 0)
                return false;

            return _result.Count >= Count;
        }

        public override void Clean()
        {
            base.Clean();
            _region = Vector4.zero;
            _result = null;
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as TemplateMatchOperNode;
            TemplatePath = sourceObj.TemplatePath;
            RegionExpression = sourceObj.RegionExpression;
            Threshold = sourceObj.Threshold;
            Count = sourceObj.Count;
        }





    }

    #endregion


    #region MouseNode

    public class MouseOperNode : BaseNodeData
    {

        [JsonProperty("click_type")]
        public int ClickType = 0;               // 0 左键,1 右键,2 仅移动
        [JsonProperty("click_pos")]
        public string ClickPos = "";            // 点击坐标
        [JsonProperty("hold_time")]
        private float _holdTime;                 // 按压时长

        [JsonIgnore]
        public float HoldTime
        {
            get { return _holdTime; }
            set { _holdTime = value; CheckHoldTime(); }
        }

        bool _down = false;

        public static MouseOperNode CreateNew()
        {
            var node = new MouseOperNode();
            node.Name = SU.ShuBiao;
            node.ClickType = 0;
            node.Delay = 0.5f;
            node._holdTime = 0.1f;
            return node;
        }



        public override void Inflow()
        {
            base.Inflow();
            _down = false;
            CheckMouseDown();
        }


        public override void Update()
        {
            base.Update();
            CheckMouseDown();

            // 检查时机，执行鼠标抬手
            if (Timer >= Delay && ClickType < 2)
            {
                uint oper = ClickType == 0 ? WU.MOUSEEVENTF_LEFTUP : WU.MOUSEEVENTF_RIGHTUP;
                WU.mouse_event(oper, 0, 0, 0, UIntPtr.Zero);

                // DU.LogWarning($"Up {Time.frameCount}");
            }
        }


        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as MouseOperNode;
            ClickType = sourceObj.ClickType;
            ClickPos = sourceObj.ClickPos;
            HoldTime = sourceObj.HoldTime;
        }

        // 检查时机，执行鼠标按压
        void CheckMouseDown()
        {
            if (!_down && Timer >= Delay - HoldTime)
            {
                _down = true;
                Vector2 pos = _scriptData.FormulaGetResultV2(ClickPos);
                int x = (int)pos.x;
                int y = (int)pos.y;
                WU.SetCursorPos(x, y);

                if (ClickType < 2)
                {
                    uint oper = ClickType == 0 ? WU.MOUSEEVENTF_LEFTDOWN : WU.MOUSEEVENTF_RIGHTDOWN;
                    WU.mouse_event(oper, 0, 0, 0, UIntPtr.Zero);
                    // DU.LogWarning($"Down {Time.frameCount} ");
                }


                // 为了防止Down与Up在同一帧，咱们手动给他延后一点
                if (Timer >= Delay)
                {
                    Timer = Delay - Utils.MinFrameTime;
                }
            }


            // 移动鼠标
            //
            // WU.mouse_event(WU.MOUSEEVENTF_MOVE | WU.MOUSEEVENTF_ABSOLUTE
            //     , x * 65535 / 1920, y * 65535 / 1080, 0, UIntPtr.Zero);
        }

        public void CheckHoldTime()
        {
            _holdTime = Math.Clamp(_holdTime, Utils.MinFrameTime, Delay);
        }

    }

    #endregion

    #region KeyBoardNode
    public enum KeyBoardOperType
    {
        FullPress,              // 完整按, 要配置按压时长
        KeyDown,                // 只按压
        KeyUp,                  // 只松开
    }

    public class KeyBoardOperNode : BaseNodeData
    {
        [JsonProperty("type")]
        public KeyBoardOperType Type = KeyBoardOperType.FullPress;
        [JsonProperty("key")]
        public string Key = "";
        [JsonProperty("hold_time")]
        private float _holdTime;              // 按压时长

        [JsonProperty("pipe")]
        public string Pipe = "";
        [JsonIgnore]
        public float HoldTime
        {
            get { return _holdTime; }
            set { _holdTime = value; CheckHoldTime(); }
        }

        bool _down = false;
        bool _legal = false;
        KeyboardEnum _key;

        public static KeyBoardOperNode CreateNew()
        {
            var node = new KeyBoardOperNode();
            node.Name = SU.JianPan;
            node.Key = AutoDataUIConfig.GetKeyboardName(AutoDataUIConfig.DefaultKeyboardEnum);
            node.Delay = 0.5f;
            node.HoldTime = 0.1f;
            return node;
        }


        public override void Inflow()
        {
            base.Inflow();
            _down = false;
            _legal = AutoDataUIConfig.IsLegalKeyboardName(Key);
            if (!_legal)
                return;

            _key = AutoDataUIConfig.KeyboardName2Enum[Key];

            if (Type == KeyBoardOperType.FullPress)
                CheckMouseDown();
        }

        public override void Action()
        {
            base.Action();

            if (!_legal)
                return;

            if (Type == KeyBoardOperType.KeyDown)
                DoKeyboard(_key, true);
            else if (Type == KeyBoardOperType.KeyUp)
                DoKeyboard(_key, false);
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);

            var sourceObj = source as KeyBoardOperNode;
            Type = sourceObj.Type;
            Key = sourceObj.Key;
            HoldTime = sourceObj.HoldTime;
        }



        public override void Update()
        {
            base.Update();
            if (!_legal)
                return;

            if (Type != KeyBoardOperType.FullPress)
                return;

            CheckMouseDown();

            // 检查时机，执行鼠标抬手
            if (Timer >= Delay)
            {
                DoKeyboard(_key, false);
            }
        }

        // 检查时机，执行鼠标按压
        void CheckMouseDown()
        {
            if (!_down && Timer >= Delay - HoldTime)
            {
                _down = true;
                DoKeyboard(_key, true);

                // 为了防止Down与Up在同一帧，咱们手动给他延后一点
                if (Timer >= Delay)
                {
                    Timer = Delay - Utils.MinFrameTime;
                }
            }

        }

        public void CheckHoldTime()
        {
            _holdTime = Math.Clamp(_holdTime, Utils.MinFrameTime, Delay);
        }

        void DoKeyboard(KeyboardEnum key, bool isDown)
        {
            if (Pipe == "")
                WU.keybd_event_packed(key, isDown);
            else
                _scriptData.Manager.AddPipeMsg(Pipe, new KeyboardMsg(key, isDown));
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

        // _varTypeStr 得出 _varTypeEnum 得出 VarType
        private FormulaVarType _varShowType;    // 要序列化
        private FormulaVarType _varType;

        [JsonIgnore]
        public string Formula
        {
            get { return _formula; }
            set { SetVarRef(ref _formula, value, _varShowType); }
        }

        [JsonIgnore]
        public FormulaVarType VarShowType
        {
            get { return _varShowType; }
            set { SetVarRef(ref _formula, _formula, value); }
        }

        [JsonIgnore] public string VarName = "";

        [JsonIgnore] public bool IsTernary = false;                         // 是否为三元表达式
        [JsonIgnore] public string[] TernaryExpressions = new string[3];    // 三元表达式
        [JsonIgnore] public bool IsLegal = false;

        public static AssignOperNode CreateNew()
        {
            var node = new AssignOperNode();
            node.Name = SU.FuZhi;
            node.Delay = 0;
            node._varShowType = FormulaVarType.Undefined;

            return node;
        }

        public override void Serialize()
        {
            base.Serialize();
            _varTypeStr = AutoDataUIConfig.VarType2Name[_varShowType];
        }

        public override void UnSerialize()
        {
            base.UnSerialize();
            AutoDataUIConfig.VarName2Type.TryGetValue(_varTypeStr, out _varShowType);
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            Formula = _formula;             // 在编辑的统计系统中初始化
        }

        public override void OnDelete()
        {
            base.OnDelete();
            Formula = "";                   // 在编辑的统计系统中清除
        }


        public override void Action()
        {
            base.Action();

            if (VarName == "") return;

            if (IsTernary)
            {
                // 先判断操作
                if (_scriptData.FormulaGetResultCondition(TernaryExpressions[0]))
                    _scriptData.RunAssignFormula(TernaryExpressions[1], _varType, InData);
                else
                    _scriptData.RunAssignFormula(TernaryExpressions[2], _varType, InData);
            }
            else
            {
                // 赋值操作
                _scriptData.RunAssignFormula(Formula, _varType, InData);
            }


            // debug_time 测试结果大概是静态调用的1000倍耗时。例如 脚本a = 1 比代码的a = 1 慢1000倍
            //
            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000; i++)
            //         _scriptData.RunAssignFormula(Formula, VarType, InData);
            // }, $"{Id} Action 10000次 ", 2);
        }
        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as AssignOperNode;
            _formula = sourceObj._formula;
            _varShowType = sourceObj._varShowType;
        }

        // 高度封装，让赋值插入到中间。
        void SetVarRef(ref string formula, string formula_value, FormulaVarType show_type)
        {
            //不含[],才录入
            if (VarName.IndexOf('[') < 0)
                _scriptData.DeleteVarRef(VarName, _varShowType, this);

            formula = formula_value;
            _varShowType = show_type;
            _varType = ConvertVarShowType(show_type);
            Refresh();

            if (VarName.IndexOf('[') < 0)
                _scriptData.AddVarRef(VarName, _varShowType, this);
        }

        FormulaVarType ConvertVarShowType(FormulaVarType show_type)
        {
            if (show_type == FormulaVarType.Bool)
                return FormulaVarType.Float;
            return show_type;
        }

        /// <summary>
        /// 先得出 VarName/ Expression/ 三元表达式
        /// 再得出 IsLegal
        /// </summary>
        public void Refresh()
        {
            var equal_index = Formula.IndexOf('=');
            var left_str = "";
            if (equal_index > -1)
            {
                VarName = Formula.Substring(0, equal_index);
                left_str = Formula.Substring(equal_index + 1);
            }
            else
            {
                VarName = "";
            }

            // Expression 可以为三元表达式
            // "?"与":"作为分隔符。
            // 
            var index1 = left_str.IndexOf('?');
            var index2 = left_str.IndexOf(':');
            IsTernary = index1 > -1 && index2 > -1;
            if (IsTernary)
            {
                IsTernary = true;
                TernaryExpressions[0] = left_str.Substring(0, index1);
                TernaryExpressions[1] = $"{VarName}={left_str.Substring(index1 + 1, index2 - index1 - 1)}";
                TernaryExpressions[2] = $"{VarName}={left_str.Substring(index2 + 1)}";
            }


            IsLegal = _scriptData.CheckFormulaLegal(Formula, _varType);
        }

    }
    #endregion
    #region ConditionNode



    /// <summary>
    /// 1. 普通条件语句; 2.For循环语句：赋值+条件+赋值——例如i=0;i<10;i=i+1
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
            set { _formula = value; Refresh(); }
        }

        [JsonIgnore] public bool IsLegal = false;

        private bool _result = false;

        public static ConditionOperNode CreateNew()
        {
            var node = new ConditionOperNode();
            node.Name = SU.TiaoJianPanDuan;
            node.Delay = 0;
            node._formula = "";
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            Formula = _formula;
        }


        public override void Action()
        {
            base.Action();
            _result = _scriptData.FormulaGetResultCondition(Formula);


            // debug_time
            //
            // DU.RunWithTimer(() =>
            // {
            //     for (int i = 0; i < 10000; i++)
            //         _scriptData.FormulaGetResultCondition(Formula);
            // }, $"{Id} Action 10000次 ", 2);
        }

        public override bool GetResult()
        {
            return _result;
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as ConditionOperNode;
            _formula = sourceObj._formula;
        }

        private void Refresh()
        {
            IsLegal = AutoDataUIConfig.ConditionIsLegal(_formula);
        }
    }
    #endregion


    #region ForNode
    /// <summary>
    /// 1. 普通条件语句; 2.For循环语句：赋值+条件+赋值——例如i=0;i<10;i=i+1
    /// 条件判断-节点，两个浮点数表达式比较大小
    /// 先用 "&&| ||"分隔成多个ConditionCell，再对ConditionCell计算
    /// &&与||有优先顺序吗
    /// </summary>
    public class ForOperNode : BaseNodeData
    {
        [JsonProperty("formula")]
        private string _formula = "";

        [JsonIgnore]
        public string Formula
        {
            get { return _formula; }
            set { _formula = value; Refresh(); }
        }

        [JsonIgnore] public string[] ForExpressions = new string[3];    // for循环的三个表达式
        [JsonIgnore] public bool IsLegal = false;

        private bool _result = false;

        public static ForOperNode CreateNew()
        {
            var node = new ForOperNode();
            node.Name = SU.XunHuan;
            node.Delay = 0;
            node._formula = "";
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            Formula = _formula;
        }


        public override void Action()
        {
            base.Action();
            if (!IsLegal)
                throw new Exception("ForOper 不合法语句");
            // For循环语句：赋值+条件+赋值——例如i=0;i<10;i=i+1
            if (InflowDoor == NodeDoor.In)
            {
                _scriptData.RunAssignFormula(ForExpressions[0], FormulaVarType.Float, default);
            }
            else if (InflowDoor == NodeDoor.In1)
            {
                _scriptData.RunAssignFormula(ForExpressions[2], FormulaVarType.Float, default);
            }
            _result = _scriptData.FormulaGetResultCondition(ForExpressions[1]);
        }

        public override bool GetResult()
        {
            return _result;
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as ForOperNode;
            _formula = sourceObj._formula;
        }

        private void Refresh()
        {
            var index0 = _formula.IndexOf(';');
            if (index0 > -1)
            {
                // 以';'分隔。0/2是赋值表达式，1是条件表达式
                ForExpressions = _formula.Split(';');
                IsLegal = _scriptData.CheckForExpressionLegal(_formula);
            }
            else
                IsLegal = false;

        }
    }

    #endregion

    #region WaitNode
    public class WaitNode : BaseNodeData
    {
        public static WaitNode CreateNew()
        {
            var node = new WaitNode();
            node.Name = SU.DengDai;
            node.Delay = 1;
            return node;
        }


    }


    #endregion

    #region TriggerEventN
    /// <summary>
    /// 两种事件类型：
    /// 1. 抛出，监听处直接执行
    /// 2. 先赋值n再抛出，监听处先条件判断n，成功才执行
    /// </summary>
    public class TriggerEventNode : BaseNodeData
    {
        [JsonProperty("event_name")]
        private string _eventName = "";
        [JsonIgnore]
        public string EventName
        { get { return _eventName; } set { SetEventName(value); } }

        /// <summary>
        /// Item1: 原str;
        /// Item2: 是否为Condition事件;这样会先赋值
        /// Item3: event_name;
        /// </summary>
        [JsonIgnore] public (string, bool, string)[] EventList;
        [JsonIgnore] public bool IsCondition = false;

        public static TriggerEventNode CreateNew()
        {
            var node = new TriggerEventNode();
            node.Name = SU.ChuFaShiJian;
            node.Delay = 0;
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            EventName = _eventName;                     // 在编辑的统计系统中新增
        }

        public override void OnDelete()
        {
            base.OnDelete();
            EventName = "";                             // 在编辑的统计系统中删除
        }

        public override void Action()
        {
            base.Action();

            if (EventList == null)
                return;

            foreach (var info in EventList)
            {
                var formula = info.Item1;
                var is_condition = info.Item2;
                var event_name = info.Item3;
                if (is_condition)
                    _scriptData.RunAssignFormula(formula, FormulaVarType.Float, InData);

                _scriptData.TriggerEvent(event_name, Id);
            }

        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);

            var sourceObj = source as TriggerEventNode;
            _eventName = sourceObj._eventName;
        }


        void SetEventName(string value)
        {
            if (EventList != null)
                foreach (var info in EventList)
                    _scriptData.RemoveEditTriggerNode(info.Item3, this);

            _eventName = value;
            if (_eventName == "")
                return;
            var list = _eventName.Split(";");
            EventList = new (string, bool, string)[list.Length];

            for (int i = 0; i < EventList.Length; i++)
            {
                var str = list[i];
                var parse = str != "" ? AutoDataUIConfig.TokenizeFormat(str)[0] : "";
                EventList[i] = (str, parse != str, parse);
            }


            foreach (var info in EventList)
                _scriptData.AddEditTriggerNode(info.Item3, info.Item2, this);
        }
    }
    #endregion

    #region ListenEventN
    /// <summary>
    /// IsCondition = false时，直接触发
    /// IsCondition = true时:
    /// 1.条件满足时才能触发或流过;
    /// 2.如果有last_node: 那么True流出时会锁"监听功能", 只有重新False流出时才会解锁。为了保证"循环模块"最多只有一个"流"
    /// 3.如果没有last_node: 就不存在锁"监听功能"这事
    /// </summary>
    public class ListenEventNode : BaseNodeData
    {
        [JsonProperty("event_name")]
        private string _eventName = "";

        [JsonIgnore]
        public string EventName
        { get { return _eventName; } set { SetEventName(value); } }

        [JsonIgnore] public string EventNameParse = "";
        [JsonIgnore] public bool IsCondition = false;
        bool _locked;

        public static ListenEventNode CreateNew()
        {
            var node = new ListenEventNode();
            node.Name = SU.JianTingShiJian;
            node.Delay = 0;
            return node;
        }
        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            EventName = _eventName;
        }

        public override void OnDelete()
        {
            base.OnDelete();
            EventName = "";
        }


        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);

            var sourceObj = source as ListenEventNode;
            _eventName = sourceObj._eventName;

        }

        public override void Clean()
        {
            base.Clean();
            _locked = false;
        }

        public override bool GetResult()
        {
            if (IsCondition)
            {
                bool condition = _scriptData.FormulaGetResultCondition(EventName);

                bool has_last_node = Door_LineCount[(int)NodeDoor.In] > 0;
                if (has_last_node)      // 有前节点就被当成"循环模块"，如果成功流出就锁住
                    _locked = condition;

                return condition;
            }

            else
                return true;
        }

        public bool CanTrigger()
        {
            if (IsCondition)
            {
                // 没锁住才行
                if (_locked)
                    return false;

                var cur_condition = _scriptData.FormulaGetResultCondition(EventName);
                return cur_condition;
            }

            return true;
        }

        void SetEventName(string value)
        {
            _scriptData.RemoveListenNode(EventNameParse, this);
            _eventName = value;

            if (_eventName == "")
                return;
            var tokens = AutoDataUIConfig.TokenizeFormat(_eventName);
            EventNameParse = tokens[0];
            IsCondition = tokens.Count > 1;
            _scriptData.AddListenNode(EventNameParse, this);
        }
    }

    #endregion
    #region StopScriptN
    /// <summary>
    /// 暂停整个脚本
    /// </summary>
    public class StopScriptNode : BaseNodeData
    {
        public static StopScriptNode CreateNew()
        {
            var node = new StopScriptNode();
            node.Name = SU.ZanTingJiaoBen;
            node.Delay = 0;
            return node;
        }


        public override void Action()
        {
            base.Action();
            _scriptData.StopScript();
        }
    }

    #endregion

    #region MapCaptureN
    /// <summary>
    /// 负责地图识别。(内存泄漏检查合格)
    /// 想改名为recognition。还是不改了，为了兼容旧的json
    /// </summary>
    public class MapCaptureNode : BaseNodeData
    {
        [JsonProperty("region_expression")]
        public string RegionExpression = "";
        [JsonProperty("map_id")]
        public string MapId = "";
        [JsonProperty("color_set")]
        public int ColorSet = 0;
        [JsonProperty("p1_pos")]
        public string P1Pos = "pos_p1";
        [JsonProperty("p2_pos")]
        public string P2Pos = "pos_p2";

        [JsonProperty("item_pos")]
        public string ItemPosList = "v2l_item_pos";
        [JsonProperty("boss_pos")]
        public string BossPosList = "v2l_boss_pos";

        MapData _mapData;

        public static MapCaptureNode CreateNew()
        {
            var node = new MapCaptureNode();
            node.Name = SU.DiTuShiBie;
            node.RegionExpression = AutoScriptData.RegionExpressionDefault;
            node.ColorSet = 0;
            return node;
        }


        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);
            if (MapId == "")
                MapId = $"Map-{scriptData.Config.Id.Substring(7)}";

            LoadData();
        }

        public override void OnDelete()
        {
            base.OnDelete();
            UnloadData();
        }



        public override void Action()
        {
            base.Action();

            var region = _scriptData.FormulaGetResultV4(RegionExpression);
            if (_mapData == null)
            {
                MapDataManager.Inst.Create(MapId, CVRect.ConvertV4Bigger(region));
                _mapData = MapDataManager.Inst.Get(MapId);
            }

            _scriptData.Manager.UseCapture(region, (capture) =>
            {
                if (_scriptData.Manager.SaveMapCaptureStatus)
                {
                    var frame = _mapData.FrameCount + 1; //先拍照，防Capture报错

                    var folder_path = $"{Application.streamingAssetsPath}/SmallMap/{MapId}";
                    if (frame == 1)
                    {
                        PathUtil.DeleteDirectory(folder_path);
                        PathUtil.CreateDirectory(folder_path);
                    }
                    IU.SaveMat(capture, $"{folder_path}/{frame}.png");
                }

                var colors = IU.MatToColor32(capture);

                _mapData.Capture(colors);

                {
                    var list = _mapData.GetIconData(MapIconType.Item).InstList;
                    var list_con = _mapData.ConvertToShow(list);
                    _scriptData.RunAssign(ItemPosList, FormulaVarType.ListVector2, list_con);
                }
                {
                    var list = _mapData.GetIconData(MapIconType.Boss).InstList;
                    var list_con = _mapData.ConvertToShow(list);
                    _scriptData.RunAssign(BossPosList, FormulaVarType.ListVector2, list_con);
                }

                {
                    var pos = _mapData.FindPlayerPosAndHistory(MapIconType.P1, out _);
                    var pos_con = _mapData.ConvertToShow(pos);
                    _scriptData.RunAssign(P1Pos, FormulaVarType.Vector2, pos_con);
                }
                {
                    var pos = _mapData.FindPlayerPosAndHistory(MapIconType.P2, out _);
                    var pos_con = _mapData.ConvertToShow(pos);
                    _scriptData.RunAssign(P2Pos, FormulaVarType.Vector2, pos_con);
                }

            });

        }


        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as MapCaptureNode;
            RegionExpression = sourceObj.RegionExpression;
            ColorSet = sourceObj.ColorSet;
            P1Pos = sourceObj.P1Pos;
            P2Pos = sourceObj.P2Pos;
            ItemPosList = sourceObj.ItemPosList;
            BossPosList = sourceObj.BossPosList;
        }

        public override void Clean()
        {
            base.Clean();
            if (_mapData != null)
            {
                MapDataManager.Inst.Remove(MapId);
                _mapData = null;
            }

        }

        public void UnloadData()
        {
            _scriptData.DeleteVarRef(P1Pos, FormulaVarType.Vector2, this);
            _scriptData.DeleteVarRef(P2Pos, FormulaVarType.Vector2, this);
            _scriptData.DeleteVarRef(ItemPosList, FormulaVarType.ListVector2, this);
            _scriptData.DeleteVarRef(BossPosList, FormulaVarType.ListVector2, this);
        }
        public void LoadData()
        {
            _scriptData.AddVarRef(P1Pos, FormulaVarType.Vector2, this);
            _scriptData.AddVarRef(P2Pos, FormulaVarType.Vector2, this);
            _scriptData.AddVarRef(ItemPosList, FormulaVarType.ListVector2, this);
            _scriptData.AddVarRef(BossPosList, FormulaVarType.ListVector2, this);
        }
    }

    #endregion

    #region MapPathFindingN

    /// <summary>
    /// 地图寻路节点。作用：寻路后输出，赋值X轴速度，赋值Y轴速度
    /// 例如：ExploreFog模式：Input = [map_id]| Output = [方向(V2)]
    /// </summary>
    public class MapPathFindingNode : BaseNodeData
    {
        [JsonProperty("finding_mode")]
        private PathFindingType _find_mode;              // 第一顺位

        [JsonProperty("input_param")]                   // 输入参数列表, new string[2]默认值是[null,null]
        public string[] _input_param;
        [JsonProperty("output_param")]                  // 输出参数列表
        public string[] _output_param;


        [JsonIgnore]
        public PathFindingType FindMode
        { get { return _find_mode; } set { SetFindMode(ref value); } }

        MapData _mapData;

        public static MapPathFindingNode CreateNew()
        {
            var node = new MapPathFindingNode();
            node.Name = SU.DiTuXunLu;
            return node;
        }

        public override void Init(AutoScriptData scriptData)
        {
            base.Init(scriptData);

            // 兼容
            if (FindMode == PathFindingType.Undefined)
                FindMode = PathFindingType.ExploreFog;
            LoadData();
        }
        public override void OnDelete()
        {
            base.OnDelete();
            UnloadData();
        }

        public override void Action()
        {
            base.Action();
            var mapId = _input_param[0];
            if (string.IsNullOrEmpty(mapId))
                throw new Exception($"MapId为空");

            if (_mapData == null)
            {
                _mapData = MapDataManager.Inst.Get(mapId);
                if (_mapData == null)
                    throw new Exception($"MapPathFindingNode找不到地图数据，MapId:{mapId}");
            }


            if (_find_mode == PathFindingType.ExploreFog)
            {
                MapIconType executor = StringToPlayer(_input_param[1]);
                float avoid_factor = float.Parse(_input_param[2]);
                float team_max_distance = float.Parse(_input_param[3]);
                Vector2Int dir = default;

                var findPath = _mapData.FindNearestFogAStar(executor, false, avoid_factor);
                if (findPath.Status == PathFindingResult.Success)
                {
                    dir = _mapData.PathResultGetDirection(findPath);
                }

                bool distance_fit = _mapData.CheckPlayerDistance(executor, team_max_distance);
                if (!distance_fit)
                {
                    dir = default;
                }


                _scriptData.RunAssign(_output_param[0], FormulaVarType.Vector2, (Vector2)dir);
            }
            else if (_find_mode == PathFindingType.FollowPlayer)
            {
                // follow_target/executor1/executor2
                MapIconType follow_target = StringToPlayer(_input_param[1]);
                MapIconType executor1 = StringToPlayer(_input_param[2]);
                MapIconType executor2 = StringToPlayer(_input_param[3]);
                float distance = float.Parse(_input_param[4]);
                float avoid_factor = float.Parse(_input_param[5]);

                if (executor1 != MapIconType.Undefined)
                {
                    Vector2Int dir = _mapData.GetDir_FollowTarget(executor1, follow_target, distance, avoid_factor, out _, out _, out _);
                    _scriptData.RunAssign(_output_param[0], FormulaVarType.Vector2, (Vector2)dir);
                }

                if (executor2 != MapIconType.Undefined)
                {
                    Vector2Int dir = _mapData.GetDir_FollowTarget(executor2, follow_target, distance, avoid_factor, out _, out _, out _);
                    _scriptData.RunAssign(_output_param[1], FormulaVarType.Vector2, (Vector2)dir);
                }

            }
            else if (_find_mode == PathFindingType.ReachPos)
            {
                MapIconType executor = StringToPlayer(_input_param[1]);
                var temp = _scriptData.FormulaGetResultV2(_input_param[2]);
                var target_pos = _mapData.ConvertToData(temp);
                float stop_distance = _scriptData.FormulaGetResult(_input_param[3]);
                float avoid_factor = float.Parse(_input_param[4]);
                float team_max_distance = float.Parse(_input_param[5]);
                Vector2Int dir = default;

                var executor_pos = _mapData.FindPlayerPosAndHistory(executor, out _);

                bool stop_distance_fit = (executor_pos - target_pos).magnitude > stop_distance;
                bool team_distance_fit = _mapData.CheckPlayerDistance(executor, team_max_distance);
                if (team_distance_fit && stop_distance_fit)
                {
                    var findPath = _mapData.PlayerToTileAStar(executor_pos, target_pos, avoid_factor);
                    if (findPath.Status == PathFindingResult.Success)
                    {
                        dir = _mapData.PathResultGetDirection(findPath);
                    }
                }
                else
                {
                    dir = default;
                }


                _scriptData.RunAssign(_output_param[0], FormulaVarType.Vector2, (Vector2)dir);
            }


        }



        public static MapIconType StringToPlayer(string str)
        {
            if (str == "P1")
                return MapIconType.P1;
            else if (str == "P2")
                return MapIconType.P2;
            else if (str == "TeamP1")
                return MapIconType.TeamP1;
            else if (str == "ElseP")
                return MapIconType.ElseP;
            else
                return MapIconType.Undefined;
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as MapPathFindingNode;
            _find_mode = sourceObj._find_mode;
            _input_param = sourceObj._input_param.ToArray();
            _output_param = sourceObj._output_param.ToArray();

        }

        public override void Clean()
        {
            base.Clean();
            _mapData = null;
        }

        void SetFindMode(ref PathFindingType mode)
        {
            if (mode == _find_mode)
                return;

            UnloadData();
            _find_mode = mode;
            switch (mode)
            {
                case PathFindingType.ExploreFog:
                    // Map_Id|Player|Avoid_Factor|Team_Max_Distance
                    _input_param = new string[4] { "", "P1", "2", "50" };
                    _output_param = new string[1];      // Move_Dir
                    break;
                case PathFindingType.FollowPlayer:
                    // Map_Id|P1|P2|Follow_Target|Stop_Distance|Avoid_Factor
                    _input_param = new string[6] { "", "P1", "P2", "", "7", "2" };
                    _output_param = new string[2];      // P1_Dir|P2_Dir
                    break;
                case PathFindingType.ReachPos:
                    // Map_Id|Player|Target_Pos|Stop_Distance|Avoid_Factor|Team_Max_Distance
                    _input_param = new string[6] { "", "P1", "", "30", "2", "50" };
                    _output_param = new string[1];      // Move_Dir
                    break;
            }
            _input_param[0] = $"Map-{_scriptData.Config.Id.Substring(7)}";
            LoadData();
        }


        public string[] GetInputParam()
        {
            return _input_param;
        }
        public string[] GetOutputParam()
        {
            return _output_param;
        }

        public void SetInputParam(string[] input)
        {
            UnloadData();
            _input_param = input;
            LoadData();
        }
        public void SetOutputParam(string[] output)
        {
            UnloadData();
            _output_param = output;
            LoadData();
        }

        void UnloadData()
        {
            switch (FindMode)
            {
                case PathFindingType.ExploreFog:
                    _scriptData.DeleteVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    break;
                case PathFindingType.FollowPlayer:
                    if (_output_param.Length < 2) return;
                    _scriptData.DeleteVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    _scriptData.DeleteVarRef(_output_param[1], FormulaVarType.Vector2, this);
                    break;
                case PathFindingType.ReachPos:
                    _scriptData.DeleteVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    break;
            }
        }
        void LoadData()
        {

            switch (FindMode)
            {
                case PathFindingType.ExploreFog:
                    _scriptData.AddVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    break;
                case PathFindingType.FollowPlayer:
                    if (_output_param.Length < 2) return;
                    _scriptData.AddVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    _scriptData.AddVarRef(_output_param[1], FormulaVarType.Vector2, this);
                    break;
                case PathFindingType.ReachPos:
                    _scriptData.AddVarRef(_output_param[0], FormulaVarType.Vector2, this);
                    break;
            }
        }
    }

    #endregion

    #region ItemGridRecogN
    /// <summary>
    /// 识别物品格。
    /// 都是固定位置。既然如此，位置信息浓缩为一个参数
    /// 输入:
    /// 1. 格子区域枚举 (背包、仓库)
    /// 2. 窗口偏移变量 (基于此计算识别区域)
    /// 输出:
    /// 1. 识别结果变量名的前缀 => 变量名= 前缀 + 固定格式 => 赋值(id,count,状态) => 懒生成数组
    /// </summary>
    public class ItemGridRecogNode : BaseNodeData
    {

        // 输入 

        [JsonProperty("recog_type")]      // 0 = id与num; 1 = frame
        public int RecogType;

        [JsonProperty("pos_type")]
        public ItemGridPosType PosType;

        [JsonProperty("start_pos")]
        public string StartPos = "";

        // ————————————————————————————

        // 输出

        [JsonProperty("result_prefix")]
        public string ResultPrefix = "";

        // ————————————————————————————

        public static ItemGridRecogNode CreateNew()
        {
            var node = new ItemGridRecogNode();
            node.Name = SU.WuPingGeShiBie;
            return node;
        }

        public override void Copy(BaseNodeData source)
        {
            base.Copy(source);
            var sourceObj = source as ItemGridRecogNode;
            PosType = sourceObj.PosType;
            StartPos = sourceObj.StartPos;
            ResultPrefix = sourceObj.ResultPrefix;
        }


        public override void Action()
        {
            base.Action();

            if (RecogType == 0)
            {

            }


            GameItemCfgManager Config = GameItemCfgManager.Inst;
            // 识别区域。先要根据PosType/StartPos计算它。
            Vector2 start_pos = _scriptData.FormulaGetResultV2(StartPos);

            if (PosType == ItemGridPosType.StashCurrency)
            {
                Vector4[] result_v4L = new Vector4[50];
                Vector4[] score_v4L = new Vector4[50];
                var size = Config.CurrCellSize;

                for (int i = 0; i < result_v4L.Length; i++)
                {
                    Vector4 result = new Vector4(-1, -1, -1, 0);
                    Vector4 scores = new Vector4(-1, -1, -1, 0);
                    var cfg = Config.GetItem(i);
                    if (cfg != null)
                    {
                        CVRect region = new CVRect(cfg.Pos.x + (int)start_pos.x, cfg.Pos.y + (int)start_pos.y
                                                    , size.x, size.y);
                        var item_img = _scriptData.Manager.GetCaptureImg(region);

                        if (RecogType == 0)
                        {
                            result.x = cfg.Id;
                            RecogUtil.IdentifyNumber(item_img, true, out var item_count);
                            result.y = item_count > -1 ? item_count : 0;
                        }
                        else if (RecogType == 1)
                        {

                        }
                    }

                    result_v4L[i] = result;
                    score_v4L[i] = scores;
                }

                SetIdResult(result_v4L, score_v4L);


                var cell_off = Config.CurrCusCellOff;
                int grid_w = 7, grid_h = 2;
                Vector4[] custom_v4L = new Vector4[14];
                for (int i = 0; i < grid_h; i++)
                    for (int j = 0; j < grid_w; j++)
                    {
                        var off = Config.CurrCusCellStart + start_pos;
                        float x = j * cell_off.x + off.x;
                        float y = i * cell_off.y + off.y;
                        CVRect region = new CVRect((int)x, (int)y, size.x, size.y);
                        var item_img = _scriptData.Manager.GetCaptureImg(region);
                        RecogUtil.IdentifyNumber(item_img, true, out var item_count);

                        int grid_index = (grid_h - 1 - i) * grid_w + j;
                        custom_v4L[grid_index] = new Vector4(-1, item_count, -1, 0);
                    }

                var custom_cur_name = ResultPrefix + "_stash_cur_custom";
                _scriptData.AddVarRef_InRun(custom_cur_name);
                _scriptData.RunAssign(custom_cur_name, FormulaVarType.ListVector4, custom_v4L);


            }
            else
            {

                Vector4[] regions = RecogUtil.GetRegions(PosType, start_pos);

                Vector4[] result_v4L = new Vector4[regions.Length];
                Vector4[] score_v4L = new Vector4[regions.Length];

                for (int i = 0; i < regions.Length; i++)
                {
                    var region = CVRect.ConvertV4(regions[i]);
                    var item_img = _scriptData.Manager.GetCaptureImg(region);

                    Vector4 result = new Vector4(-1, -1, -1, 0);
                    Vector4 scores = new Vector4(-1, -1, -1, 0);


                    RecogUtil.IdentifyItem(item_img, out int item_id, out float score, out float diff);
                    RecogUtil.IdentifyFrame(item_img, false, out ItemGridFrame status);
                    if (item_id > 0)
                    {
                        result.x = item_id;
                        scores.x = score > 1 ? (int)score : score;

                        RecogUtil.IdentifyNumber(item_img, false, out var item_count);
                        result.y = item_count > -1 ? item_count : 1;
                    }
                    result.z = (int)status;
                    // scores.z = score1;
                    result_v4L[i] = result;
                    score_v4L[i] = scores;
                }

                SetIdResult(result_v4L, score_v4L);
            }

        }

        void RecogFrame()
        {

        }

        void GetResultVarName(out string ResultVar, out string ScoreVar)
        {
            ResultVar = "";
            ScoreVar = "";

            switch (PosType)
            {
                case ItemGridPosType.Bag:
                    ResultVar = ResultPrefix + "_bag";
                    ScoreVar = ResultPrefix + "_bag_score";
                    break;
                case ItemGridPosType.StashCurrency:
                    ResultVar = ResultPrefix + "_stash_cur";
                    ScoreVar = ResultPrefix + "_stash_cur_score";
                    break;
                case ItemGridPosType.StashPage:
                    ResultVar = ResultPrefix + "_stash_page";
                    ScoreVar = ResultPrefix + "_stash_page_score";
                    break;
                case ItemGridPosType.Body:
                    ResultVar = ResultPrefix + "_body";
                    ScoreVar = ResultPrefix + "_body_score";
                    break;
            }
        }

        /// <summary>
        /// 把识别结果赋值到"前缀_result"的V4[]数组中
        /// "items" (建议)
        /// </summary>
        void SetIdResult(Vector4[] result_v4L, Vector4[] score_v4L)
        {

            GetResultVarName(out string ResultVar, out string ScoreVar);

            _scriptData.AddVarRef_InRun(ResultVar);
            _scriptData.AddVarRef_InRun(ScoreVar);

            var info = _scriptData.GetVarValue(ResultVar);
            if (info == null)
                _scriptData.RunAssign(ResultVar, FormulaVarType.ListVector4, result_v4L);
            else
            {
                var list = (Vector4[])info.Value;
                for (int i = 0; i < list.Length; i++)
                {
                    var data = result_v4L[i];
                    var ori = list[i];
                    ori.x = data.x;
                    ori.y = data.y;
                    list[i] = ori;
                }
            }

            _scriptData.RunAssign(ScoreVar, FormulaVarType.ListVector4, score_v4L);
        }

        void SetFrameResult(Vector4[] result_v4L)
        {
            GetResultVarName(out string ResultVar, out _);
            _scriptData.AddVarRef_InRun(ResultVar);

            var info = _scriptData.GetVarValue(ResultVar);
            if (info == null)
                _scriptData.RunAssign(ResultVar, FormulaVarType.ListVector4, result_v4L);
            else
            {
                var list = (Vector4[])info.Value;
                for (int i = 0; i < list.Length; i++)
                {
                    var data = result_v4L[i];
                    var ori = list[i];
                    ori.z = data.z;
                    list[i] = ori;
                }
            }
        }


    }



    #endregion


    #endregion


    /// <summary>
    /// 枚举顺序不能变。会序列化
    /// </summary>
    public enum PathFindingType
    {
        Undefined,
        ExploreFog,                 // 探索迷雾。没有迷雾 就输出成功，否则输出失败
        FollowPlayer,               // 跟踪一个Player。打算做成跟参数形式，跟哪个P
        ReachPos,                   // 抵达目的地。寻路直到与Pos相距一定距离之内停止。

    }

    /// <summary>
    /// 物品格识别节点——识别区域Type。
    /// 用Type直接控制识别区域
    /// </summary>
    public enum ItemGridPosType
    {
        Undefined,
        Bag,                // 背包格子
        StashCurrency,      // 仓库的通货页
        StashPage,          // 仓库的普通页
        Body,               // 身上的装备格子
    }

    public enum ItemGridFrame
    {
        Empty = 0,          // 无框
        Selected = 1,       // 选中框。 上下左右移动的选中框
        Target = 2,         // 目标框。当使用强化石时，提示可作为强化对象
    }
}