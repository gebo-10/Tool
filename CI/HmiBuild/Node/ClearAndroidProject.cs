using BuildSystem;
using DagEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace HmiBuild
{
    public class ClearAndroidProject: Node
    {
        public ClearAndroidProject() {
            AddOutput("AndroidProjet", typeof(string));
        }

        public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var workspace = Blackboard.Get<Workspace>("workspace");
            if (workspace == null)
                throw new InvalidOperationException("Workspace not found in blackboard.");

            string projectPath = workspace.AndroidProjectPath;
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("Project path is required.");

            // 将当前节点的执行逻辑封装为一个委托，提交到 AndroidQueue 中排队执行
            await workspace.AndroidQueue.EnqueueAsync(async token =>
            {
                // 模拟清空操作（这里使用进度报告模拟耗时任务）
                const int totalDurationMs = 2000;   // 总耗时 2 秒
                const int intervalMs = 500;         // 每 0.5 秒报告一次
                int steps = totalDurationMs / intervalMs;

                for (int i = 0; i <= steps; i++)
                {
                    token.ThrowIfCancellationRequested();

                    int percentage = (i * 100) / steps;  // 0, 25, 50, 75, 100
                    ReportProgress(percentage);          // 触发进度事件

                    if (i < steps)
                        await Task.Delay(intervalMs, token);
                }

                // 设置输出引脚的值
                SetOutputValue("AndroidProjet", projectPath);
            });

        }
    }
}
