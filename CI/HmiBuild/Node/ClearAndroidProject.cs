using BuildSystem;

namespace BuildSystem
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
            catch (OperationCanceledException)
            {
                status = HmiBuildNodeStatus.Cancelled;
                // 可选：报告取消时的进度
                ReportProgress(progress);
                // 重新抛出取消异常，让 DAG 引擎知道是被取消的
                throw;
            }
            catch (Exception ex)
            {
                status = HmiBuildNodeStatus.Failed;
                ReportProgress(progress);
                // 记录日志
                throw;
            }
        }
    }
}
