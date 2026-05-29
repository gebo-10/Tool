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
            await workspace.UnityQueue.EnqueueAsync(async token =>
            {
                // 模拟打包：总耗时 5 秒，每 0.5 秒报告一次进度
                const int totalDurationMs = 5000;
                const int intervalMs = 500;
                int steps = totalDurationMs / intervalMs; // 10 步

                for (int i = 0; i <= steps; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int percentage = (i * 100) / steps;
                    ReportProgress(percentage);  // 触发进度事件
                    //Console.WriteLine("BuildApk working");
                    if (i < steps)  // 最后一步不需要再 Delay
                        await Task.Delay(intervalMs, cancellationToken);
                }

                // 模拟生成 APK 文件路径
                string apkPath = System.IO.Path.Combine(projectPath, "app.apk");
                SetOutputValue("ApkPath", apkPath);
            });
  
        }
    }
}
