using BuildSystem;
using DagEngine;
namespace BuildSystem
{
    public class BuildApk : HmiBuildNode
    {
        public BuildApk()
        {
            AddInput("Project", typeof(string));
            AddOutput("ApkPath", typeof(string));
        }
        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            // 获取输入的项目路径
            var projectPath = GetInputValue<string>("Project");
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("Project input is required.");

            var workspace = Blackboard.Get<Workspace>("workspace");
            if (workspace == null)
                throw new InvalidOperationException("Workspace not found in blackboard.");
            var pipeline = Blackboard.Get<Pipeline>("pipeline");

            var workPath=Path.Combine(pipeline.WorkDir, pipeline.Guid);
            var logPath= Path.Combine(workPath, Id + ".log");
            //var logfile=File.Create(logPath);

            // 使用 StreamWriter 写入日志，设置 AutoFlush 保证实时落盘
            using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };


            try
            {
                await logWriter.WriteLineAsync($"[{DateTime.Now}] Build started.");
                await workspace.UnityQueue.EnqueueAsync(async token =>
                {
                    await logWriter.WriteLineAsync($"[{DateTime.Now}] Entering ActionQueue.");
                    // 模拟打包：总耗时 5 秒，每 0.5 秒报告一次进度
                    const int totalDurationMs = 5000;
                    const int intervalMs = 500;
                    int steps = totalDurationMs / intervalMs; // 10 步

                    for (int i = 0; i <= steps; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int percentage = (i * 100) / steps;
                        progress = percentage;
                        if (percentage == 100)
                        {
                            status = HmiBuildStatus.Completed;
                        }
                        ReportProgress(percentage);  // 触发进度事件
                        await logWriter.WriteLineAsync($"[{DateTime.Now}] Progress: {percentage}%");
                        //Console.WriteLine("BuildApk working");
                        if (i < steps)  // 最后一步不需要再 Delay
                            await Task.Delay(intervalMs, cancellationToken);
                    }

                    // 模拟生成 APK 文件路径
                    string apkPath = System.IO.Path.Combine(projectPath, "app.apk");
                    SetOutputValue("ApkPath", apkPath);
                    await logWriter.WriteLineAsync($"[{DateTime.Now}] APK path set to: {apkPath}");
                });
                await logWriter.WriteLineAsync($"[{DateTime.Now}] Build completed successfully.");
            }
            catch (OperationCanceledException)
            {
                status = HmiBuildStatus.Cancelled;
                // 可选：报告取消时的进度
                ReportProgress(progress);
                await logWriter.WriteLineAsync($"[{DateTime.Now}] Build cancelled.");
                // 重新抛出取消异常，让 DAG 引擎知道是被取消的
                throw;
            }
            catch (Exception ex)
            {
                status = HmiBuildStatus.Failed;
                ReportProgress(progress);
                // 记录日志
                await logWriter.WriteLineAsync($"[{DateTime.Now}] Build failed: {ex}");
                throw;
            }
            
        }
    }
}
