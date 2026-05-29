using BuildSystem;

namespace HmiBuild
{
    public class ClearAndroidProject: HmiBuildNode
    {
        public ClearAndroidProject() {
            AddOutput("AndroidProjet", typeof(string));
        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            status = HmiBuildNodeStatus.Running;
            var workspace = Blackboard.Get<Workspace>("workspace");
            if (workspace == null)
                throw new InvalidOperationException("Workspace not found in blackboard.");

            string projectPath = workspace.AndroidProjectPath;
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("Project path is required.");

            try
            {
                await workspace.AndroidQueue.EnqueueAsync(async token =>
                {
                    const int totalDurationMs = 2000;
                    const int intervalMs = 500;
                    int steps = totalDurationMs / intervalMs;

                    for (int i = 0; i <= steps; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        int percentage = (i * 100) / steps;
                        progress = percentage;
                        if (percentage == 100)
                        {
                            status = HmiBuildNodeStatus.Completed;
                        }
                        ReportProgress(percentage);

                        if (i < steps)
                            await Task.Delay(intervalMs, token);
                    }

                    SetOutputValue("AndroidProjet", projectPath);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                status = HmiBuildNodeStatus.Failed;
                ReportProgress(progress);
                // 可选：记录日志
                // Console.WriteLine($"节点 {Id} 执行失败: {ex.Message}");
                throw; // 重新抛出，让 DAG 引擎捕获并处理
            }
        }
    }
}
