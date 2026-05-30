
using DagEngine;
using System.Text.Json;

namespace BuildSystem
{

    //public class PipelineProgress
    //{
    //    public string PipelineId { get; set; }
    //    public string Stage { get; set; }          // 当前阶段名称，如 "Building Unity", "Building Android"
    //    public int Percentage { get; set; }        // 0-100 总体进度
    //    public string? CurrentNodeId { get; set; } // 正在执行的节点 ID
    //    public int CompletedNodes { get; set; }    // 已完成节点数
    //    public int TotalNodes { get; set; }        // 总节点数
    //}

    /// <summary>
    /// 打包任务，包含 DAG 定义和执行逻辑。
    /// </summary>
    public class Pipeline : IDisposable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }

        public HmiBuildStatus status= HmiBuildStatus.Pending;

        public Dictionary<string, object> Parameters { get; }

        private DagEngine.Dag _dag;
        private Workspace _workspace;

        private bool _disposed;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        protected CancellationToken CancellationToken => _cts.Token;

        //private IProgress<PipelineProgress>? _progress;
        public event Action<Pipeline, NodeProgressEventArgs>? PipelineProgress;

        public Pipeline(Dictionary<string, object> parameters)
        {
            Parameters = parameters ?? new Dictionary<string, object>();
            Name = Parameters.TryGetValue("Name", out var n) ? n.ToString() : Id;

            var dag = new DagEngine.Dag();
            var node1 = new ClearAndroidProject()
            {
                Name = "ClearAndroid1"
            };
            dag.AddNode(node1);
            _dag = dag;
        }


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }

        public void Cancel()
        {
            if (_disposed) return;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放，忽略
            }
        }


        //public void SetProgressReporter(IProgress<PipelineProgress> progress)
        //{
        //    _progress = progress;
        //}


        /// <summary>
        /// 根据参数构建 DAG（由子类重写，或通过工厂注入）
        /// </summary>
        protected virtual DagEngine.Dag BuildDag(Workspace workspace)
        {
            // 示例实现：根据参数创建一个简单的 DAG
            var dag = new DagEngine.Dag();
            dag.Blackboard.Set("workspace", workspace);
            // 添加节点（实际应从参数读取节点类型和连接）
            // 此处仅为演示，真正使用时需根据业务逻辑构建
            {
                var node1 = new ClearAndroidProject()
                {
                    Name = "ClearAndroid1"
                };
                var node2 = new BuildApk()
                {
                    Name = "BuildApk1"
                };
                dag.AddNode(node1);
                dag.AddNode(node2);

                dag.AddEdge(node1.Id, "AndroidProjet", node2.Id, "Project");
            }

            {
                var node1 = new ClearAndroidProject()
                {
                    Name = "ClearAndroid2"
                };
                var node2 = new BuildApk()
                {
                    Name = "BuildApk2"
                };
                dag.AddNode(node1);
                dag.AddNode(node2);

                dag.AddEdge(node1.Id, "AndroidProjet", node2.Id, "Project");
            }

            {
                var node1 = new ClearAndroidProject()
                {
                    Name = "ClearAndroid3"
                };
                var node2 = new BuildApk()
                {
                    Name = "BuildApk3"
                };
                dag.AddNode(node1);
                dag.AddNode(node2);

                dag.AddEdge(node1.Id, "AndroidProjet", node2.Id, "Project");
            }


            dag.NodeProgressUpdated += (s, e) =>
            {
                //Console.WriteLine($"节点 {e.NodeId} {e.NodeType} {e.NodeName} 进度: {e.Percentage}%");
                //var json = JsonSerializer.Serialize(e.Node.Serialize(), new JsonSerializerOptions { WriteIndented = true });
                //Console.WriteLine(json);
                // 这里可以将节点进度转换为 PipelineProgress 并报告
                // _progress?.Report(new PipelineProgress { ... });
                PipelineProgress?.Invoke(this, e);
            };


            var json=dag.ToJson();

            return dag;
        }

        /// <summary>
        /// 绑定工作区，并执行打包流程
        /// </summary>
        public async Task ExecuteAsync(Workspace workspace, CancellationToken externalToken = default)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));


            // 合并外部令牌和内部令牌
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
            var combinedToken = linkedCts.Token;


            _dag = BuildDag(workspace);

            // 执行 DAG
            await _dag.ExecuteAllAsync(maxConcurrency: -1, combinedToken).ConfigureAwait(false);
        }

        public override string ToString() => $"Pipeline {Name} ({Id})";

        public string ToJson()
        {
            return _dag.ToJson();
        }
    }

}