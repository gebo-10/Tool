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
            // 获取项目路径（可选，用于模拟清空操作）
            string projectPath = workspace.AndroidProjectPath;
            if (string.IsNullOrEmpty(projectPath))
                throw new InvalidOperationException("Project input is required.");

            const int totalDurationMs = 2000;   // 总耗时 2 秒
            const int intervalMs = 500;         // 每 0.5 秒报告一次
            int steps = totalDurationMs / intervalMs; // 4 步

            for (int i = 0; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int percentage = (i * 100) / steps;  // 0, 25, 50, 75, 100
                ReportProgress(percentage);          // 触发进度事件

                if (i < steps)
                    await Task.Delay(intervalMs, cancellationToken);
            }

            SetOutputValue("AndroidProjet", projectPath);
            // 模拟清空操作（例如删除临时文件等）
            // 此处仅为演示，实际可调用文件系统操作
            await Task.CompletedTask;

        }
    }
}
