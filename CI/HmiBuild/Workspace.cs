using BuildSystem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BuildSystem
{
    /// <summary>
    /// 工作区，对应一个物理目录，包含 Unity 工程和 Android 工程。
    /// </summary>
    public class Workspace
    {
        //public enum WorkspaceType
        //{
        //    Development,
        //    Production
        //}
        //public WorkspaceType workspaceType;
        /// <summary>工作区根目录（绝对路径）</summary>
        public string RootPath { get; }

        /// <summary>Unity 工程路径（通常为 RootPath/UnityProject）</summary>
        public string UnityProjectPath => Path.Combine(RootPath, "UnityProject");

        /// <summary>Android 工程路径（通常为 RootPath/AndroidProject）</summary>
        public string AndroidProjectPath => Path.Combine(RootPath, "AndroidProject");

        /// <summary>当前是否被占用</summary>
        public bool IsOccupied { get; private set; }

        /// <summary>占用该工作区的任务标识（用于调试）</summary>
        public string? OccupiedBy { get; private set; }

        /// <summary>工作区状态变更事件（可用于日志）</summary>
        public event Action<Workspace, bool>? OccupancyChanged;


        // 每个 Workspace 拥有独立的 Unity 和 Android 资源队列
        public ActionQueue UnityQueue { get; } = new ActionQueue("Unity");
        public ActionQueue AndroidQueue { get; } = new ActionQueue("Android");

        public Workspace(string rootPath)
        {
            RootPath = Path.GetFullPath(rootPath);
            EnsureDirectoriesExist();
        }

        // 可选：停止所有队列（在释放工作区时调用）
        public async Task StopQueuesAsync()
        {
            await UnityQueue.StopAsync();
            await AndroidQueue.StopAsync();
        }

        /// <summary>确保工作区目录结构存在</summary>
        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(UnityProjectPath);
            Directory.CreateDirectory(AndroidProjectPath);
        }

        /// <summary>
        /// 尝试占用工作区
        /// </summary>
        /// <param name="occupantId">任务标识</param>
        /// <returns>是否成功占用</returns>
        public bool TryOccupy(string occupantId)
        {
            lock (this)
            {
                if (IsOccupied) return false;
                IsOccupied = true;
                OccupiedBy = occupantId;
                OccupancyChanged?.Invoke(this, true);
                return true;
            }
        }

        /// <summary>
        /// 释放工作区
        /// </summary>
        /// <param name="occupantId">必须与当前占用的任务标识一致，否则拒绝释放</param>
        /// <returns>是否成功释放</returns>
        public bool TryRelease(string occupantId)
        {
            lock (this)
            {
                if (!IsOccupied || OccupiedBy != occupantId) return false;
                IsOccupied = false;
                OccupiedBy = null;
                // 可选：清理工作区临时文件
                CleanWorkspace();
                OccupancyChanged?.Invoke(this, false);
                return true;
            }
        }

        /// <summary>
        /// 清空工作区内自动生成的文件（保留目录结构）
        /// </summary>
        private void CleanWorkspace()
        {
            try
            {
                // 可根据实际需求清理特定子目录，例如删除 Build 输出
                var buildDir = Path.Combine(RootPath, "BuildOutput");
                if (Directory.Exists(buildDir))
                    Directory.Delete(buildDir, true);
            }
            catch (Exception ex)
            {
                // 记录日志但不影响释放操作
                Console.WriteLine($"清理工作区 {RootPath} 失败: {ex.Message}");
            }
        }
    }

    
}