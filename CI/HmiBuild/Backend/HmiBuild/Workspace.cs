using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HmiBuildSystem
{
    /// <summary>
    /// 工作区，对应一个物理目录，包含 Unity 工程和 Android 工程。
    /// </summary>
    public class Workspace
    {
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

        public Workspace(string rootPath)
        {
            RootPath = Path.GetFullPath(rootPath);
            EnsureDirectoriesExist();
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
        /// <param name="taskId">任务标识</param>
        /// <returns>是否成功占用</returns>
        public bool TryOccupy(string taskId)
        {
            lock (this)
            {
                if (IsOccupied) return false;
                IsOccupied = true;
                OccupiedBy = taskId;
                OccupancyChanged?.Invoke(this, true);
                return true;
            }
        }

        /// <summary>
        /// 释放工作区
        /// </summary>
        /// <param name="taskId">必须与当前占用的任务标识一致，否则拒绝释放</param>
        /// <returns>是否成功释放</returns>
        public bool TryRelease(string taskId)
        {
            lock (this)
            {
                if (!IsOccupied || OccupiedBy != taskId) return false;
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

    /// <summary>
    /// 管理多个工作区，提供线程安全的申请/释放接口。
    /// </summary>
    public class WorkspaceManager : IDisposable
    {
        private readonly List<Workspace> _workspaces = new List<Workspace>();
        private readonly ConcurrentQueue<Workspace> _freeWorkspaces = new ConcurrentQueue<Workspace>();
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        /// <summary>
        /// 创建工作区管理器
        /// </summary>
        /// <param name="baseDirectories">每个字符串是一个工作区根目录的路径</param>
        public WorkspaceManager(IEnumerable<string> baseDirectories)
        {
            foreach (var dir in baseDirectories)
            {
                var ws = new Workspace(dir);
                _workspaces.Add(ws);
                _freeWorkspaces.Enqueue(ws);
            }
            _semaphore = new SemaphoreSlim(_workspaces.Count, _workspaces.Count);
        }

        /// <summary>
        /// 异步申请一个空闲工作区（等待直到有可用）
        /// </summary>
        /// <param name="taskId">任务标识，释放时需要传入</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>被占用的工作区，调用方使用完毕后必须调用 ReleaseWorkspace</returns>
        public async Task<Workspace> AcquireWorkspaceAsync(string taskId, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_freeWorkspaces.TryDequeue(out var workspace))
                {
                    // 注意：TryOccupy 可能因并发失败（理论上不应该，因为信号量已保证至少一个空闲）
                    if (!workspace.TryOccupy(taskId))
                        throw new InvalidOperationException($"工作区 {workspace.RootPath} 被意外占用");
                    return workspace;
                }
                throw new InvalidOperationException("没有空闲工作区，但信号量却成功获取，逻辑错误");
            }
            catch
            {
                _semaphore.Release(); // 归还信号量
                throw;
            }
        }

        /// <summary>
        /// 释放工作区
        /// </summary>
        /// <param name="workspace">要释放的工作区实例</param>
        /// <param name="taskId">任务标识，必须与占用时一致</param>
        public void Release(Workspace workspace, string taskId)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (!workspace.TryRelease(taskId))
                throw new InvalidOperationException($"释放工作区失败，任务 {taskId} 并非当前占用者");

            _freeWorkspaces.Enqueue(workspace);
            _semaphore.Release();
        }

        /// <summary>
        /// 获取所有工作区状态（用于监控）
        /// </summary>
        public IReadOnlyList<(string Path, bool IsOccupied, string? OccupiedBy)> GetAllWorkspaceStatus()
        {
            lock (_workspaces)
            {
                return _workspaces.Select(w => (w.RootPath, w.IsOccupied, w.OccupiedBy)).ToList();
            }
        }

        /// <summary>
        /// 等待所有工作区变为空闲（用于优雅关闭）
        /// </summary>
        public async Task WaitAllIdleAsync(CancellationToken cancellationToken = default)
        {
            // 简单轮询（实际可用信号量计数，但此场景较少用）
            while (_freeWorkspaces.Count < _workspaces.Count)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _semaphore.Dispose();
            _disposed = true;
        }
    }
}