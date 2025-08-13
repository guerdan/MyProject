
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Script.Util;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Script.Model.Auto
{
    public class AutoData
    {
        public List<string> DebugLogs = new List<string>();

        public AutoData()
        {

        }

    }

    /// <summary>
    /// Off,不在执行中。In,在执行中
    /// </summary>
    public enum NodeStatus
    {
        Off,
        In,
    }

    /// <summary>
    /// 一个自动化脚本
    /// </summary>
    [Serializable]
    public class AutoScriptData
    {
        [JsonProperty("name")]
        public string Name;
        [JsonProperty("first_node")]
        public string FirstNode;
        [JsonProperty("end_index")]
        public int EndIndex;
        [JsonProperty("list")]
        public List<BaseNodeData> List = new List<BaseNodeData>();

    }


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
        [JsonProperty("pos")]
        public string Pos_Ser;                  // 序列化坐标
        /// <summary>
        /// 上个节点。发生流入事件是本节点启动的必要条件
        /// </summary>
        [JsonIgnore]
        public List<string> LastNode = new List<string>();
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
        public float Delay = 0;

        #endregion

        #region 变量

        [JsonIgnore]
        public Vector2 Pos;      //画布坐标
        [JsonIgnore]
        public NodeStatus Status = NodeStatus.Off; // 节点状态。In表示流程处在此节点，否则就是Off
        [JsonIgnore]
        public float Timer = 0; //内部计时器.In时开始累加，直到 Delay执行一次内容
        [JsonIgnore]
        public float ExcuteTimes = 0;  //执行过多少次

        [JsonIgnore]
        public int Index = 0;    // 节点创建顺序

        #endregion

        // public BaseNodeData()
        // {
        // }
        // public BaseNodeData(string id)
        // {
        //     Id = id;
        // }

        // 执行内容
        public virtual void Action() { }
        // 执行结果判断方法
        public virtual bool GetResult()
        {
            return true;
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


        public static string IdFromat = "node-";


    }

    public class MouseOperNode : BaseNodeData
    {

        [JsonProperty("oper")]
        public int Oper;

        // public MouseOperNode(string id) : base(id)
        // {

        // }
    }
    public class KeyBoardOperNode : BaseNodeData
    {
        // public KeyBoardOperNode(string id) : base(id)
        // {

        // }
    }

    /// <summary>
    /// 只在基类上加逻辑：Inflow()后并在事件抛出时计时器才会开启
    /// </summary>
    public class ListenEventNode : BaseNodeData
    {
        // public ListenEventNode(string id) : base(id)
        // {

        // }

        public override void Action() { }

        // 流入行为
        public override void Inflow()
        {
            base.Inflow();
            Timer = 0;
        }
    }

    #region Manager

    /// <summary>
    /// 所有面板上的节点都是实例，只有一份。如果不是一份的话，可视化就很麻烦了。
    /// </summary>
    public class ProcessNodeManager
    {
        private static ProcessNodeManager _inst;
        public static ProcessNodeManager Inst
        {
            get
            {
                if (_inst == null) _inst = new ProcessNodeManager();
                return _inst;
            }
        }

        public event Action Tick;      //通知ui刷新
        public event Action<string> StatusChange;  //通知ui刷新

        public AutoScriptData _autoData;

        public Dictionary<string, BaseNodeData> _activeNodes = new Dictionary<string, BaseNodeData>();
        public Dictionary<string, BaseNodeData> _nodeData;

        private bool running = false;


        ProcessNodeManager()
        {
            Init();
        }
        void Init()
        {
            ParseJson();
            // Simulate();
        }

        public void ParseJson()
        {
            string path = Application.streamingAssetsPath + "/nodes.json";
            string json = File.ReadAllText(path);
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

            _autoData = JsonConvert.DeserializeObject<AutoScriptData>(json, settings);
            _nodeData = new Dictionary<string, BaseNodeData>();
            for (int i = 0; i < _autoData.List.Count; i++)
            {
                var item = _autoData.List[i];
                item.UnSerialize();
                _nodeData[item.Id] = item;


            }

            // 初始化每个节点的 LastNode
            foreach (var item in _nodeData.Values)
            {
                foreach (var id in item.TrueNextNodes)
                {
                    _nodeData[id].LastNode.Add(item.Id);
                }

                foreach (var id in item.FalseNextNodes)
                {
                    _nodeData[id].LastNode.Add(item.Id);
                }
            }



        }

        public void Simulate()
        {
            //模拟数据
            int count = 10;
            _autoData = new AutoScriptData
            {
                Name = "New Auto Script",
                FirstNode = BaseNodeData.IdFromat + "0",
                EndIndex = count - 1,
                List = new List<BaseNodeData>()
            };

            _nodeData = new Dictionary<string, BaseNodeData>();
            for (int i = 0; i < count; i++)
            {
                var id = BaseNodeData.IdFromat + $"{i}";
                BaseNodeData item = null;
                if (i < 9)
                {
                    item = new BaseNodeData();
                }
                else
                {
                    item = new MouseOperNode();
                    ((MouseOperNode)item).Oper = 1; // 模拟鼠标操作
                }
                _nodeData[id] = item;
                item.Id = id;
                item.Index = i;
                item.Pos = new Vector2(200 + i * 300, 300);
                item.Delay = 5 + Random.Range(0, 2);

                if (i - 1 >= 0)
                    item.LastNode.Add(BaseNodeData.IdFromat + $"{i - 1}");
                if (i + 1 < count)
                    item.TrueNextNodes.Add(BaseNodeData.IdFromat + $"{i + 1}");
            }

        }

        public bool IsRuning()
        {
            return running;
        }
        public void Start()
        {
            running = true;
        }

        public void Stop()
        {
            running = false;
        }

        public void OnUpdate(float deltaTime)
        {
            if (running)
            {
                var list = _activeNodes.Values.ToList();

                if (list.Count == 0)
                {
                    //设定起点
                    StartNode(_nodeData[_autoData.FirstNode]);
                }
                else
                {
                    foreach (var n in list)
                    {
                        // 排除的情况，以后写
                        n.Timer += deltaTime;
                        if (n.Timer >= n.Delay)
                        {
                            n.Action();
                            TransferNode(n);
                        }
                    }
                }
            }

            // 通知UI刷新
            Tick?.Invoke();
        }



        public void TransferNode(BaseNodeData node)
        {
            node.Outflow();
            StatusChange?.Invoke(node.Id);
            _activeNodes.Remove(node.Id);
            bool result = node.GetResult();
            List<string> nextList = result ? node.TrueNextNodes : node.FalseNextNodes;
            foreach (var id in nextList)
            {
                StartNode(_nodeData[id]);
            }

        }

        public void StartNode(BaseNodeData node)
        {
            if (node.Status == NodeStatus.In)
            {
                DU.LogWarning($"节点 {node.Id} 已经在执行中，不能重复启动");
                return;
            }

            _activeNodes[node.Id] = node;
            node.Inflow();
            StatusChange?.Invoke(node.Id);
        }

        public void Save()
        {
            //_autoData.List用_nodeData重新序列化,其他字段不动

            List<BaseNodeData> list = _nodeData.Values.ToList();
            list.Sort((a, b) =>
            {
                return a.Index.CompareTo(b.Index);
            });

            foreach (var n in list)
            {
                n.Serialize();
            }


            _autoData.List = list;
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            string json = JsonConvert.SerializeObject(_autoData, settings);

            // 保存到文件。可能考虑persistentDataPath
            string path = Application.streamingAssetsPath + "/nodes.json";
            File.WriteAllText(path, json);

            DU.LogWarning("保存成功");
        }

        #region 节点

        public BaseNodeData GetNode(string id)
        {
            return _nodeData[id];
        }
        public BaseNodeData CreateNode(Vector2 pos)
        {
            BaseNodeData node = new BaseNodeData();
            _autoData.EndIndex++;
            node.Id = BaseNodeData.IdFromat + $"{_autoData.EndIndex}";
            node.Pos = pos;

            _nodeData[node.Id] = node;

            //自动将起点设为 {node.Id}
            if (_autoData.FirstNode == null)
            {
                _autoData.FirstNode = node.Id;
            }

            return node;
        }

        public void DeleteNode(string id)
        {

            var node = _nodeData[id];
            _nodeData.Remove(id);
            //删线段
            foreach (var fromId in node.LastNode)
            {
                var fromNode = _nodeData[fromId];
                fromNode.TrueNextNodes.Remove(id);
                fromNode.FalseNextNodes.Remove(id);
            }
            foreach (var toId in node.TrueNextNodes)
            {
                var toNode = _nodeData[toId];
                toNode.LastNode.Remove(id);
            }
            foreach (var toId in node.FalseNextNodes)
            {
                var toNode = _nodeData[toId];
                toNode.LastNode.Remove(id);
            }
            node.LastNode.Clear();
            node.TrueNextNodes.Clear();
            node.FalseNextNodes.Clear();

            //如果被删的是起点，改StartNode
            //找没有LastNode的节点
            if (_autoData.FirstNode == id)
            {
                List<BaseNodeData> list = _nodeData.Values.ToList();
                list.Sort((a, b) =>
                {
                    return a.Index.CompareTo(b.Index);
                });

                _autoData.FirstNode = list.Count > 0 ? list[0].Id : null;
            }
        }

        #endregion

        #region 线段

        public void AddLine(string fromId, string toId, bool isTrue)
        {
            _nodeData.TryGetValue(fromId, out var fromNode);
            _nodeData.TryGetValue(toId, out var toNode);

            var list = isTrue ? fromNode.TrueNextNodes : fromNode.FalseNextNodes;
            var other = isTrue ? fromNode.FalseNextNodes : fromNode.TrueNextNodes;

            if (!list.Contains(toId))
                list.Add(toId);

            if (other.Contains(toId))
                other.Remove(toId);

            toNode.LastNode.Add(fromId);
        }

        public void DeleteLine(string fromId, string toId, bool isTrue)
        {
            _nodeData.TryGetValue(fromId, out var fromNode);
            _nodeData.TryGetValue(toId, out var toNode);

            if (isTrue)
                fromNode.TrueNextNodes.Remove(toId);
            else
                fromNode.FalseNextNodes.Remove(toId);


            toNode.LastNode.Remove(fromId);
        }

        #endregion
    }

    #endregion





}