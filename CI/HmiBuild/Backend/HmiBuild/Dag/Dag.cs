using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DagEngine
{
    // ---------- Pin 类型 ----------
    public abstract class Pin
    {
        public string Name { get; }
        public Type Type { get; }
        public object? Value { get; set; }

        protected Pin(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        public bool IsTypeCompatible(Pin other) =>
            other.Type.IsAssignableFrom(Type) || Type.IsAssignableFrom(other.Type);
    }

    public class PinIn : Pin
    {
        public PinOut? Connection { get; set; }

        public PinIn(string name, Type type) : base(name, type) { }

        public object? GetValue()
        {
            if (Connection != null && Connection.Type.IsAssignableFrom(Type))
                return Connection.Value;
            return Value;
        }
    }

    public class PinOut : Pin
    {
        public List<PinIn> Connections { get; } = new List<PinIn>();

        public PinOut(string name, Type type) : base(name, type) { }

        public void ConnectTo(PinIn target)
        {
            if (!IsTypeCompatible(target))
                throw new InvalidOperationException($"类型不兼容: {Type.Name} -> {target.Type.Name}");
            target.Connection = this;
            Connections.Add(target);
        }

        public void Disconnect(PinIn target)
        {
            if (target.Connection == this)
                target.Connection = null;
            Connections.Remove(target);
        }
    }

    // ---------- 可扩展节点基类（支持多输入/多输出）----------
    public abstract class Node
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        protected List<PinIn> InputPins { get; } = new List<PinIn>();
        protected List<PinOut> OutputPins { get; } = new List<PinOut>();

        public IReadOnlyList<PinIn> Inputs => InputPins;
        public IReadOnlyList<PinOut> Outputs => OutputPins;

        protected PinIn AddInput(string name, Type type)
        {
            var pin = new PinIn(name, type);
            InputPins.Add(pin);
            return pin;
        }

        protected PinOut AddOutput(string name, Type type)
        {
            var pin = new PinOut(name, type);
            OutputPins.Add(pin);
            return pin;
        }

        protected PinIn GetInputPin(string name) =>
            InputPins.FirstOrDefault(p => p.Name == name)
            ?? throw new ArgumentException($"输入引脚 {name} 不存在");

        protected PinOut GetOutputPin(string name) =>
            OutputPins.FirstOrDefault(p => p.Name == name)
            ?? throw new ArgumentException($"输出引脚 {name} 不存在");

        protected T? GetInputValue<T>(string pinName)
        {
            var pin = GetInputPin(pinName);
            var val = pin.GetValue();
            if (val != null && !(val is T))
                throw new InvalidCastException($"引脚 {pinName} 值类型 {val.GetType()} 无法转换为 {typeof(T)}");
            return (T?)val;
        }

        protected void SetOutputValue(string pinName, object? value)
        {
            var pin = GetOutputPin(pinName);
            pin.Value = value;
        }

        // 异步执行节点逻辑（子类必须实现）
        public abstract Task ExecuteAsync(CancellationToken cancellationToken = default);
    }

    // ---------- DAG 核心 ----------
    public class Dag
    {
        private readonly Dictionary<string, Node> _nodes = new();
        private readonly List<(string FromNode, string FromPin, string ToNode, string ToPin)> _edges = new();

        public IReadOnlyDictionary<string, Node> Nodes => _nodes;
        public IReadOnlyList<(string FromNode, string FromPin, string ToNode, string ToPin)> Edges => _edges;

        public void AddNode(Node node)
        {
            if (_nodes.ContainsKey(node.Id))
                throw new InvalidOperationException($"节点 {node.Id} 已存在");
            _nodes[node.Id] = node;
        }

        public void AddEdge(string fromNodeId, string fromPinName, string toNodeId, string toPinName)
        {
            if (!_nodes.TryGetValue(fromNodeId, out var fromNode))
                throw new ArgumentException($"源节点 {fromNodeId} 不存在");
            if (!_nodes.TryGetValue(toNodeId, out var toNode))
                throw new ArgumentException($"目标节点 {toNodeId} 不存在");

            var fromPin = fromNode.Outputs.FirstOrDefault(p => p.Name == fromPinName)
                ?? throw new ArgumentException($"源节点 {fromNodeId} 无名为 {fromPinName} 的输出引脚");
            var toPin = toNode.Inputs.FirstOrDefault(p => p.Name == toPinName)
                ?? throw new ArgumentException($"目标节点 {toNodeId} 无名为 {toPinName} 的输入引脚");

            if (!fromPin.IsTypeCompatible(toPin))
                throw new InvalidOperationException($"类型不兼容: {fromPin.Type.Name} -> {toPin.Type.Name}");

            toPin.Connection?.Connections.Remove(toPin);
            toPin.Connection = null;
            fromPin.ConnectTo(toPin);
            _edges.Add((fromNodeId, fromPinName, toNodeId, toPinName));
        }



        /// <summary>
        /// 检测图中是否存在环，若无环则返回所有节点的拓扑顺序（可选）
        /// </summary>
        private List<string> GetTopologicalOrderOrThrow()
        {
            var inDegree = new Dictionary<string, int>();
            var adj = new Dictionary<string, List<string>>();

            foreach (var nodeId in _nodes.Keys)
            {
                inDegree[nodeId] = 0;
                adj[nodeId] = new List<string>();
            }

            foreach (var edge in _edges)
            {
                adj[edge.FromNode].Add(edge.ToNode);
                inDegree[edge.ToNode]++;
            }

            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var order = new List<string>();

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                order.Add(nodeId);

                foreach (var neighbor in adj[nodeId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            if (order.Count != _nodes.Count)
                throw new InvalidOperationException("DAG 中存在循环依赖，无法执行");

            return order;
        }


        // ========== 异步并行执行 ==========
        public async Task ExecuteAllAsync(int maxConcurrency = -1, CancellationToken cancellationToken = default)
        {
            // ---------- 1. 构建图并检测环，同时获取下游列表和入度 ----------
            var downstream = new Dictionary<string, List<string>>();
            var inDegree = new Dictionary<string, int>();
            foreach (var nodeId in _nodes.Keys)
            {
                downstream[nodeId] = new List<string>();
                inDegree[nodeId] = 0;
            }
            foreach (var edge in _edges)
            {
                downstream[edge.FromNode].Add(edge.ToNode);
                inDegree[edge.ToNode]++;
            }

            // 拓扑排序检测环（同时得到初始入度）
            var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var topologicalOrder = new List<string>();
            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                topologicalOrder.Add(nodeId);
                foreach (var neighbor in downstream[nodeId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }
            if (topologicalOrder.Count != _nodes.Count)
                throw new InvalidOperationException("DAG 中存在循环依赖，无法执行");

            // 重新构建用于调度的入度（pendingCount）
            var pendingCount = new Dictionary<string, int>();
            foreach (var nodeId in _nodes.Keys)
                pendingCount[nodeId] = 0;
            foreach (var edge in _edges)
                pendingCount[edge.ToNode]++;

            // ---------- 2. 并行调度 ----------
            var state = new Dictionary<string, int>(); // 0=未启动,1=运行中,2=已完成,3=失败
            var remainingNodes = _nodes.Count;
            var tcs = new TaskCompletionSource<bool>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var semaphore = maxConcurrency > 0 ? new SemaphoreSlim(maxConcurrency) : null;
            var lockObj = new object(); // 用于保护 pendingCount 和 state

            // 尝试启动一个节点（在锁保护下决定是否启动）
            void TrySchedule(string nodeId)
            {
                bool shouldStart = false;
                lock (lockObj)
                {
                    if (state.TryGetValue(nodeId, out var status) && status == 0 && pendingCount[nodeId] == 0)
                    {
                        state[nodeId] = 1;
                        shouldStart = true;
                    }
                }
                if (shouldStart)
                {
                    // 异步启动，不阻塞调用者
                    _ = Task.Run(() => ExecuteNodeAsync(nodeId), cts.Token);
                }
            }

            // 执行单个节点（异步）
            async Task ExecuteNodeAsync(string nodeId)
            {
                // 并发度控制
                if (semaphore != null)
                    await semaphore.WaitAsync(cts.Token).ConfigureAwait(false);

                var node = _nodes[nodeId];
                try
                {
                    await node.ExecuteAsync(cts.Token).ConfigureAwait(false);

                    // 节点成功完成
                    List<string>? downstreamNodes = null;
                    lock (lockObj)
                    {
                        state[nodeId] = 2;
                        if (Interlocked.Decrement(ref remainingNodes) == 0)
                            tcs.TrySetResult(true);

                        // 减少所有下游节点的 pendingCount
                        downstreamNodes = downstream[nodeId];
                        foreach (var down in downstreamNodes)
                        {
                            pendingCount[down]--;
                        }
                    }

                    // 在锁外启动下游节点（避免锁内调用 Task.Run）
                    if (downstreamNodes != null)
                    {
                        foreach (var down in downstreamNodes)
                        {
                            // 注意：这里有可能多个上游同时完成，导致 TrySchedule 被多次调用
                            // 但 TrySchedule 内部会检查状态，保证只启动一次
                            TrySchedule(down);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        state[nodeId] = 3;
                    }
                    if (!tcs.Task.IsCompleted)
                    {
                        cts.Cancel(); // 取消其他未启动节点
                        tcs.TrySetException(ex);
                    }
                }
                finally
                {
                    semaphore?.Release();
                }
            }

            // 3. 启动所有初始入度为0的节点
            foreach (var nodeId in _nodes.Keys)
            {
                TrySchedule(nodeId);
            }

            await tcs.Task.ConfigureAwait(false);
        }

        // ---------- JSON 导入导出 ----------
        public class NodeDefinition
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "";
            public Dictionary<string, object?>? Parameters { get; set; }
        }

        public class EdgeDefinition
        {
            public string FromNode { get; set; } = "";
            public string FromPin { get; set; } = "";
            public string ToNode { get; set; } = "";
            public string ToPin { get; set; } = "";
        }

        public class DagData
        {
            public List<NodeDefinition> Nodes { get; set; } = new();
            public List<EdgeDefinition> Edges { get; set; } = new();
        }

        public static Dag FromJson(string json, Func<string, Dictionary<string, object?>?, Node> nodeFactory)
        {
            var data = JsonSerializer.Deserialize<DagData>(json)
                ?? throw new InvalidOperationException("JSON 反序列化失败");

            var dag = new Dag();

            foreach (var nodeDef in data.Nodes)
            {
                var node = nodeFactory(nodeDef.Type, nodeDef.Parameters);
                node.Id = nodeDef.Id;
                dag.AddNode(node);
            }

            foreach (var edgeDef in data.Edges)
            {
                dag.AddEdge(edgeDef.FromNode, edgeDef.FromPin, edgeDef.ToNode, edgeDef.ToPin);
            }

            return dag;
        }

        public string ToJson()
        {
            var data = new DagData
            {
                Nodes = _nodes.Values.Select(n => new NodeDefinition
                {
                    Id = n.Id,
                    Type = n.GetType().AssemblyQualifiedName ?? n.GetType().FullName!,
                    Parameters = SerializeNodeParameters(n)
                }).ToList(),
                Edges = _edges.Select(e => new EdgeDefinition
                {
                    FromNode = e.FromNode,
                    FromPin = e.FromPin,
                    ToNode = e.ToNode,
                    ToPin = e.ToPin
                }).ToList()
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        private static Dictionary<string, object?>? SerializeNodeParameters(Node node)
        {
            // 可根据节点类型扩展，此处返回 null 表示无额外参数
            return null;
        }
    }
}