using HmiBuildSystem;

namespace BuildSystem
{
    /// <summary>
    /// 打包任务，包含 DAG 定义和执行逻辑。
    /// </summary>
    public class Pipeline
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public Dictionary<string, object> Parameters { get; }

        private DagEngine.Dag _dag;
        private Workspace _workspace;

        public Pipeline(Dictionary<string, object> parameters)
        {
            Parameters = parameters ?? new Dictionary<string, object>();
            Name = Parameters.TryGetValue("Name", out var n) ? n.ToString() : Id;
        }

        /// <summary>
        /// 根据参数构建 DAG（由子类重写，或通过工厂注入）
        /// </summary>
        protected virtual DagEngine.Dag BuildDag()
        {
            // 示例实现：根据参数创建一个简单的 DAG
            var dag = new DagEngine.Dag();
            // 添加节点（实际应从参数读取节点类型和连接）
            // 此处仅为演示，真正使用时需根据业务逻辑构建
            return dag;
        }

        /// <summary>
        /// 绑定工作区，并执行打包流程
        /// </summary>
        internal async Task ExecuteAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _dag = BuildDag();

            // 可选：将 Workspace 路径注入到节点参数中（例如通过全局变量）
            InjectWorkspaceToNodes(_dag, workspace.DirectoryPath);

            // 执行 DAG
            await _dag.ExecuteAllAsync(maxConcurrency: -1, cancellationToken).ConfigureAwait(false);
        }

        private void InjectWorkspaceToNodes(DagEngine.Dag dag, string workspacePath)
        {
            // 遍历所有节点，如果节点类型支持设置工作区路径，则进行注入
            foreach (var node in dag.Nodes.Values)
            {
                if (node is IWorkspaceAware aware)
                    aware.SetWorkspacePath(workspacePath);
            }
        }

        public override string ToString() => $"Pipeline {Name} ({Id})";
    }

    /// <summary>
    /// 节点可实现的接口，用于获取工作区路径
    /// </summary>
    public interface IWorkspaceAware
    {
        void SetWorkspacePath(string path);
    }
}