
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Script.Util;
using Unity.VisualScripting;
using UnityEngine;


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
        CaptureOper,        //截图操作
        TemplateMatchOper,  //模版匹配
        AssignOper,         //赋值操作
        ConditionOper,      //条件判断操作
        ForOper,            //循环操作
        MouseOper,          //鼠标操作
        KeyBoardOper,       //键盘操作
        WaitOper,           //只等待
        TriggerEvent,       //触发事件
        ListenEvent,        //监听事件
        StopScript,         //暂停脚本
        MapCapture,         //小地图拍摄(识别)
        MapPathFinding,     //小地图寻路
        ItemGridRecog,      //物品格识别
    }


    #region AutoScriptConfig

    /// <summary>
    /// 脚本配置
    /// </summary>
    [Serializable]
    public class AutoScriptConfig
    {

        [JsonProperty("version")]
        public string Version = "";             // 版本号。用于兼容
        [JsonProperty("id")]
        public string Id = "";                  // 唯一性。由Setting决定(强制)
        [JsonProperty("name")]
        public string Name = "";                // 名称，与文件名保持一致。由文件名决定(强制)
        [JsonProperty("first_node")]
        public string FirstNode = "";
        [JsonProperty("list")]                  // 节点数据列表
        public List<BaseNodeData> List = new List<BaseNodeData>();

        [JsonProperty("slots")]                 // 固定槽点。key = node_index, value = 固定槽点
        public Dictionary<string, int[]> Slots = new Dictionary<string, int[]>();

        /// <summary>
        /// 所有画布的配置
        /// </summary>
        [JsonProperty("canvas")]
        public List<ScriptCanvasConfig> Canvas = new List<ScriptCanvasConfig>();


        [JsonProperty("end_index")]
        public int EndIndex = -1;
        [JsonProperty("canvas_end_index")]
        public int CanvasEndIndex = -1;
        [JsonProperty("capture_end_index")]
        public int CaptureEndIndex = -1;


        public static string IdStart = "script-";
    }
    [Serializable]
    public class ScriptCanvasConfig
    {
        [JsonProperty("id")]
        public string Id;
        [JsonProperty("name")]
        private string Name;

        [JsonIgnore]
        public float UIWidth;
        [JsonIgnore]
        public bool Changed = false;


        public void SetName(string value)
        {
            var old = Name;
            Name = SU.SaveString(value);
            Changed = old != Name;
        }

        public string GetName()
        {
            return SU.GetString(Name);
        }
    }

    #endregion


    [Serializable]
    public struct NodeLinkInfo          // 描述两节点间的连接
    {
        [JsonProperty("sd")]
        public NodeDoor SelfDoor;       // 自己的门
        [JsonProperty("oi")]

        public string OtherId;          // 对方的Id
        [JsonProperty("od")]

        public NodeDoor OtherDoor;      // 对方的门

        public NodeLinkInfo(NodeDoor self_door, string other_id, NodeDoor other_door)
        {
            SelfDoor = self_door;
            OtherId = other_id;
            OtherDoor = other_door;
        }
    }

    /// <summary>
    /// 门。关系节点如何流动执行
    /// </summary>
    public enum NodeDoor
    {
        In = 0,             // 通用入口
        In1 = 1,            // 备用入口 
        OutTrue = 2,        // 通用True出口
        OutFalse = 3,       // 通用False出口
    }


    #region AutoScriptData
    public partial class AutoScriptData
    {
        // 打印输出
        public Dictionary<string, (int, List<double>)> RunTimeDic = new Dictionary<string, (int, List<double>)>();

        public bool IsRunning => _running;
        public bool IsEnd => _isEnd;
        public bool IsError => _error_count > 0;
        public int ErrorCount => _error_count;
        // 仅编辑模式下展示。 dic<event_name, (list<node>, lower_name)>
        public Dictionary<string, (List<TriggerEventNode>, string, bool)> Edit_TriggerNodes
            = new Dictionary<string, (List<TriggerEventNode>, string, bool)>();

        public AutoScriptManager Manager;


        public bool NeedSaveWhenEnd = false;

        // 跟踪性，热点执行流。目前不智能，除非每个节点设置热点。
        public string HotSpotNodeId;


        #region 内部数据
        public AutoScriptConfig Config;
        public Dictionary<string, BaseNodeData> NodeDatas = new Dictionary<string, BaseNodeData>();

        // 从字典改为队列。如何确保Node的顺序
        public List<BaseNodeData> ActiveNodes = new List<BaseNodeData>();
        public List<BaseNodeData> NextActiveNodes = new List<BaseNodeData>();
        // key = canvas_id, value = node_id
        public Dictionary<string, List<string>> Canvas2Node = new Dictionary<string, List<string>>();
        private Dictionary<string, List<ListenEventNode>> _listenNodes = new Dictionary<string, List<ListenEventNode>>();



        // (_running = true && _isEnd = false)表示运行中
        // (_running = false && _isEnd = false)表示暂停
        // (_running = false && _isEnd = true)表示结束
        private bool _running = false;      // 是否暂停   
        private bool _isEnd = true;         // 是否结束   
        private int _error_count = 0;       // 累计报错数   


        #endregion

        public AutoScriptData(AutoScriptConfig config, AutoScriptManager manager)
        {
            Config = config;
            Manager = manager;

            if (Config.Canvas.Count == 0)         // 具备兼容
            {
                AddCanvas("默认");
            }

            foreach (var item in Config.Canvas)
            {
                Canvas2Node[item.Id] = new List<string>();
            }

            NodeDatas = new Dictionary<string, BaseNodeData>();
            var node_list = Config.List;
            for (int i = 0; i < node_list.Count; i++)
            {
                var item = node_list[i];
                item.UnSerialize();
                NodeDatas[item.Id] = item;

            }

            // Init、AfterInit需要分开
            foreach (var node in NodeDatas.Values)
            {
                node.Init(this);
            }
            foreach (var node in NodeDatas.Values)
            {
                node.AfterInit();
            }

            AddVarRef("debug_status", FormulaVarType.Bool, null);

            // 兼容
            //
            if (Config.Version == "")
            {
                Config.Version = "0.0.0";                   // 升级后版本号
                foreach (var pair in Config.Slots.ToList())
                {
                    var id = pair.Key;
                    var list = pair.Value;
                    if (list.Length == 3)
                    {
                        Config.Slots[id] = new int[4] { list[0], -1, list[1], list[2] };
                    }
                }
            }

            if (Config.Version == "0.0.0")
            {
                Config.Version = "0.0.1";                   // 升级后版本号
                foreach (var node in NodeDatas.Values)
                {
                    node.SetName("");
                    node.SetDescription("");
                }
            }

        }

        public void initLastNode(BaseNodeData node)
        {
            foreach (var pair in node.Links)
            {
                string id = pair.Key;
                var info = pair.Value;
                NodeDatas[id].AddLink(info.OtherDoor, node.Id, info.SelfDoor);
            }
        }

        public int AddCanvas(string name = "")
        {
            Config.CanvasEndIndex++;
            if (name == "")
                name = Config.CanvasEndIndex == 0 ? "默认" : $"默认{Config.CanvasEndIndex}";

            var cfg = new ScriptCanvasConfig() { Id = Config.CanvasEndIndex.ToString() };
            cfg.SetName(name);
            Config.Canvas.Add(cfg);
            Canvas2Node[cfg.Id] = new List<string>();
            return Config.Canvas.Count - 1;
        }

        public void DeleteCanvas(int index)
        {
            // 不能删空。
            // 删画布，删画布内所有的节点。
            var id = Config.Canvas[index].Id;
            Config.Canvas.RemoveAt(index);
            Canvas2Node.Remove(id);

            foreach (var node in NodeDatas.Values.ToArray())
            {
                if (node.CanvasId == id)
                {
                    node.OnDelete();
                    NodeDatas.Remove(node.Id);
                }
            }

        }

        public static BaseNodeData CreateNodeRaw(NodeType type)
        {
            BaseNodeData node = null;
            switch (type)
            {
                case NodeType.CaptureOper:
                    node = CaptureOperNode.CreateNew();
                    break;
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
                case NodeType.ForOper:
                    node = ForOperNode.CreateNew();
                    break;
                case NodeType.WaitOper:
                    node = WaitNode.CreateNew();
                    break;
                case NodeType.TriggerEvent:
                    node = TriggerEventNode.CreateNew();
                    break;
                case NodeType.ListenEvent:
                    node = ListenEventNode.CreateNew();
                    break;
                case NodeType.StopScript:
                    node = StopScriptNode.CreateNew();
                    break;
                case NodeType.MapCapture:
                    node = MapCaptureNode.CreateNew();
                    break;
                case NodeType.MapPathFinding:
                    node = MapPathFindingNode.CreateNew();
                    break;
                case NodeType.ItemGridRecog:
                    node = ItemGridRecogNode.CreateNew();
                    break;
            }
            return node;
        }

        /// <summary>
        /// pos:画布位置；id:如果为空则用顺延id来创建
        /// </summary>
        public BaseNodeData CreateNode(string canvas_id, Vector2 pos, NodeType type, string id = null)
        {
            BaseNodeData node = CreateNodeRaw(type);
            if (string.IsNullOrEmpty(id))
            {
                Config.EndIndex++;
                node.Id = Config.EndIndex.ToString();
            }
            else
            {
                node.Id = id;
            }

            node.CanvasId = canvas_id;
            node.Pos = pos;
            NodeDatas[node.Id] = node;

            node.Init(this);
            node.AfterInit();

            return node;
        }

        public BaseNodeData CopyNode(string canvas_id, Vector2 pos, string target_id)
        {
            if (!NodeDatas.ContainsKey(target_id)) return null;
            var source = NodeDatas[target_id];

            BaseNodeData node = CreateNodeRaw(source.NodeType);

            Config.EndIndex++;
            node.Id = Config.EndIndex.ToString();

            node.CanvasId = canvas_id;
            node.Pos = pos;
            NodeDatas[node.Id] = node;
            node.Copy(source);

            node.Init(this);
            node.AfterInit();

            return node;
        }

        public void DeleteNode(string id)
        {
            var node = NodeDatas[id];
            NodeDatas.Remove(id);

            node.OnDelete();

            // 删关联线段
            RefreshSlot(node);
            foreach (var info in node.LastNode.Values)
            {
                var fromId = info.OtherId;
                var fromNode = NodeDatas[fromId];
                fromNode.RemoveLink(info.OtherDoor, id);
                fromNode.RefreshLineIds();
                RefreshSlot(fromNode);
            }
            foreach (var info in node.Links.Values)
            {
                var toId = info.OtherId;
                var toNode = NodeDatas[toId];
                toNode.RemoveLink(info.OtherDoor, id);
                toNode.RefreshLineIds();
                RefreshSlot(toNode);
            }

        }

        public void RefreshSlot(BaseNodeData node)
        {
            var all_slots = Config.Slots;
            if (all_slots.TryGetValue(node.IndexStr, out var slots))
            {
                for (int i = 0; i < 4; i++)
                    if (node.Door_LineCount[i] == 0)
                        slots[i] = -1;
            }
        }


        public void AddSlot(BaseNodeData node, int type, int value)
        {
            var all_slots = Config.Slots;
            if (!all_slots.TryGetValue(node.IndexStr, out var slots))
            {
                slots = new int[] { -1, -1, -1, -1 }; // 0-in, 1-in1, 2-outTrue, 3-outFalse
                all_slots[node.IndexStr] = slots;
            }

            slots[type] = value;
        }

        public int[] GetSlot(BaseNodeData node)
        {
            if (Config.Slots.TryGetValue(node.IndexStr, out var slots))
            {

            }

            return slots;
        }

        /// <summary>
        /// 找type类型的节点，返回第一个找到的结果
        /// </summary>
        public BaseNodeData GetNodeByType(NodeType type)
        {

            foreach (var node in NodeDatas.Values)
            {
                if (node.NodeType == type)
                {
                    return node;
                }
            }
            return null;
        }


        public MapPathFindingNode GetMapPathFindingNode(PathFindingType type)
        {

            foreach (var node in NodeDatas.Values)
            {
                if (node.NodeType == NodeType.MapPathFinding)
                {
                    var n = (MapPathFindingNode)node;
                    if (n.FindMode == type)
                    {
                        return n;
                    }
                }
            }
            return null;
        }

        public static NodeType GetNodeType(BaseNodeData node)
        {
            if (node is CaptureOperNode)
                return NodeType.CaptureOper;
            else if (node is TemplateMatchOperNode)
                return NodeType.TemplateMatchOper;
            else if (node is AssignOperNode)
                return NodeType.AssignOper;
            else if (node is ConditionOperNode)
                return NodeType.ConditionOper;
            else if (node is ForOperNode)
                return NodeType.ForOper;
            else if (node is MouseOperNode)
                return NodeType.MouseOper;
            else if (node is KeyBoardOperNode)
                return NodeType.KeyBoardOper;
            else if (node is WaitNode)
                return NodeType.WaitOper;
            else if (node is TriggerEventNode)
                return NodeType.TriggerEvent;
            else if (node is ListenEventNode)
                return NodeType.ListenEvent;
            else if (node is StopScriptNode)
                return NodeType.StopScript;
            else if (node is MapCaptureNode)
                return NodeType.MapCapture;
            else if (node is MapPathFindingNode)
                return NodeType.MapPathFinding;
            else if (node is ItemGridRecogNode)
                return NodeType.ItemGridRecog;

            return NodeType.CaptureOper;
        }
        public static void SetRules(BaseNodeData node)
        {
            switch (node.NodeType)
            {
                // 地图截图与物品格截图不能主动截图，依赖于"截图节点"。故要配合使用并且它们的Delay都设为0
                case NodeType.TemplateMatchOper:
                case NodeType.MapCapture:
                case NodeType.MapPathFinding:
                case NodeType.ItemGridRecog:
                    node.Delay = 0;
                    break;
            }
        }


        public void DoUpdate(float deltaTime)
        {
            if (!_running) return;
            foreach (var n in ActiveNodes)
            {
                n.Timer += deltaTime;
                try { n.Update(); }
                catch (Exception e)
                {
                    _error_count++;
                    TerminateScript(false);
                    DU.LogError($"[DoActionNode][node_id = {n.Id}]\n{e.Message}\n{e.StackTrace}");
                }
            }
        }


        /// <summary>
        /// 因为要先统计截图的范围，再去执行匹配。所以把Action拆成两个部分，
        /// </summary>
        public void BeforeAction(float deltaTime)
        {
            if (!_running) return;
            foreach (var n in ActiveNodes)
            {
                if (n.CanAction)
                    n.BeforeAction();
            }
        }

        #region DoAction
        // 执行时机：全部 Update => 全部 Action && TransferNode
        // 参考Unity，节点创建帧执行Awake和Start，再后一帧才执行Updatei
        public void DoAction(float deltaTime)
        {
            if (!_running) return;

            foreach (var n in ActiveNodes)
            {
                if (n.CanAction)
                {
                    DoActionNode(n);
                    TransferNode(n);
                }
                else
                {
                    NextActiveNodes.Add(n);
                }
            }

            ActiveNodes = new List<BaseNodeData>(NextActiveNodes);
            NextActiveNodes.Clear();

            // ActiveNodes 现在是有序的。同条流水线的节点优先级越高。
            if (ActiveNodes.Count > 0)
                HotSpotNodeId = ActiveNodes[0].Id;

            // 判断是否结束
            if (ActiveNodes.Count == 0)
            {
                _running = false;
                _isEnd = true;
                ActiveNodes.Clear();
                Manager.OnChangeScriptStatus?.Invoke();

                if (NeedSaveWhenEnd)
                {
                    NeedSaveWhenEnd = false;
                    Manager.SaveScript(Config.Id);
                }
            }
        }

        void DoActionNode(BaseNodeData n)
        {
            var ms = DU.RunWithTimer(() =>
            {
                try { n.Action(); }
                catch (Exception e)
                {
                    _error_count++;
                    TerminateScript(false);
                    DU.LogError($"[DoActionNode][node_id = {n.Id}]\n{e.Message}\n{e.StackTrace}");
                }
            });

            Record($"节点 {n.Id} 执行", ms);
        }

        #endregion


        public void StartScript()
        {
            if (NodeDatas.Count == 0)
            {
                DU.LogWarning("没有节点，无法启动");
                return;
            }

            _running = true;
            _isEnd = false;
            _error_count = 0;

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

            // 清理节点内的上一把的缓存数据
            Clear();

            // 设定起点
            HotSpotNodeId = Config.FirstNode;
            StartNode(NodeDatas[Config.FirstNode], null, NodeDoor.In, default);
        }

        public void StopScript()
        {
            _running = false;
        }
        public void ContinueScript()
        {
            _running = true;
        }

        public void TerminateScript(bool doClear = true)
        {
            _running = false;
            _isEnd = true;

            if (doClear)
                Clear();
            if (NeedSaveWhenEnd)
            {
                NeedSaveWhenEnd = false;
                Manager.SaveScript(Config.Id);
            }
        }

        void Clear()
        {
            foreach (var n in NodeDatas.Values)
            {
                n.Clean();
            }

            ActiveNodes.Clear();
            ClearRunData();
        }

        public void StartNode(BaseNodeData node, string last_id, NodeDoor inflow_door, FormulaVarInfo in_data)
        {
            // 单节点最多运行一个流水线
            if (node.Status == NodeStatus.In)
                return;

            node.ExcuteLastNodeId = last_id;
            node.InData = in_data;
            node.InflowDoor = inflow_door;
            node.Inflow();

            if (node.CanAction)
            {
                node.BeforeAction();
                DoActionNode(node);
                TransferNode(node);
            }
            else
            {
                NextActiveNodes.Add(node);
                Manager.OnChangeNodeStatus?.Invoke(node.Id);
            }
        }

        /// <summary>
        /// 流程链中行进节点，遇到delay=0的节点会一口气执行完
        /// </summary>
        public void TransferNode(BaseNodeData node)
        {
            if (!_running)          //停止
                return;

            bool is_hot = HotSpotNodeId == node.Id || HotSpotNodeId == null;

            node.Outflow();
            NodeDoor result = node.GetResult() ? NodeDoor.OutTrue : NodeDoor.OutFalse;

            string hotId = null;
            foreach (var info in node.Links.Values)
            {
                if (info.SelfDoor != result)
                    continue;

                string next_id = info.OtherId;
                if (hotId == null)
                    hotId = next_id;
                var next = NodeDatas[next_id];

                StartNode(next, node.Id, info.OtherDoor, node.OutData);
            }

            if (is_hot)
                HotSpotNodeId = hotId;

            Manager.OnChangeNodeStatus?.Invoke(node.Id);
        }

        public string GetSaveJson()
        {
            //_autoData.List用_nodeData重新序列化,其他字段不动

            List<BaseNodeData> list = new List<BaseNodeData>(NodeDatas.Count);

            foreach (var node in NodeDatas.Values)      // 保个险
            {
                if (Canvas2Node.ContainsKey(node.CanvasId))
                {
                    list.Add(node);
                }
            }
            list.Sort((a, b) =>
            {
                return a.Index.CompareTo(b.Index);
            });

            foreach (var n in list)
            {
                n.Serialize();
            }

            var all_slots = Config.Slots;
            foreach (var id in all_slots.Keys.ToList())
                if (!NodeDatas.ContainsKey(id))
                    all_slots.Remove(id);
                else
                {
                    var slots = all_slots[id];
                    if (slots[0] == -1 && slots[1] == -1 && slots[2] == -1)
                        all_slots.Remove(id);
                }

            Config.List = list;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            string json = JsonConvert.SerializeObject(Config, settings);
            return json;

        }


        public void AddListenNode(string event_name, ListenEventNode node)
        {
            if (event_name == "") return;

            if (!_listenNodes.TryGetValue(event_name, out var list))
            {
                list = new List<ListenEventNode>();
                _listenNodes[event_name] = list;
            }
            list.Add(node);

            // 排个序, 按node.Index
            //
            int len = list.Count;
            int cur = node.Index;
            for (int i = 0; i < len - 1; i++)
            {
                if (list[i].Index > cur)
                {
                    cur = list[i].Index;
                    var temp = list[i];
                    list[i] = list[len - 1];
                    list[len - 1] = temp;
                }
            }
        }

        public void RemoveListenNode(string event_name, ListenEventNode node)
        {
            if (event_name == "") return;
            if (_listenNodes.TryGetValue(event_name, out var list))
            {
                list.Remove(node);
            }
        }

        // 改动：触发后立马生效
        public void TriggerEvent(string eventName, string node_id)
        {
            if (_listenNodes.TryGetValue(eventName, out var list))
            {
                foreach (var next in list)
                {
                    if (!next.CanTrigger()) continue;

                    StartNode(next, node_id, NodeDoor.In, default);
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



        public void AddEditTriggerNode(string event_name, bool isCondition, TriggerEventNode node)
        {
            if (event_name == "") return;
            if (!Edit_TriggerNodes.TryGetValue(event_name, out var pair))
            {
                var list = new List<TriggerEventNode>();
                pair = (list, event_name.ToLower(), isCondition);
                Edit_TriggerNodes[event_name] = pair;
            }
            pair.Item1.Add(node);
        }

        public void RemoveEditTriggerNode(string event_name, TriggerEventNode node)
        {
            if (event_name == "") return;
            if (Edit_TriggerNodes.TryGetValue(event_name, out var pair))
            {
                var list = pair.Item1;
                list.Remove(node);
                if (list.Count == 0)
                    Edit_TriggerNodes.Remove(event_name);
            }
        }

        public List<TriggerEventNode> GetEditTriggerNodes(string eventName)
        {
            if (Edit_TriggerNodes.TryGetValue(eventName, out var list))
            {
                return list.Item1;
            }
            return new List<TriggerEventNode>();
        }

        // 做成，n次-100ms-100ms-100ms的形式。
        // >=1ms 才会被记录时间
        public void Record(string message, double ms, bool print = true)
        {
            // if (print)
            //     DU.Log($"[{message}] 耗时{ms}ms  帧时刻{Time.frameCount}");

            if (!RunTimeDic.TryGetValue(message, out var tuple))
            {
                tuple = (0, new List<double>());
            }
            tuple.Item1 += 1;


            tuple.Item2.Insert(0, ms);
            if (tuple.Item2.Count > 5)
            {
                tuple.Item2.RemoveAt(tuple.Item2.Count - 1);
            }

            RunTimeDic[message] = tuple;
        }


        public string GetCapturePath()
        {
            return $"{Application.streamingAssetsPath}/Capture/{Config.Name}_{Config.Id.Substring(7)}";
        }

        public static string RegionExpressionDefault = "Screen{}";

    }

    #endregion





}