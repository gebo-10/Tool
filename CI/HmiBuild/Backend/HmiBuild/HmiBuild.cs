using HmiBuildSystem;
using System.Collections.Concurrent;

namespace BuildSystem
{
    /// <summary>
    /// 负责管理 Pipeline 队列和 Workspace 池，按需执行打包任务。
    /// </summary>
    public class HmiCi : IDisposable
    {
        private readonly WorkspaceManager _workspaceManager;
        private readonly ConcurrentQueue<Pipeline> _pendingQueue = new ConcurrentQueue<Pipeline>();
        private readonly SemaphoreSlim _queueSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();
        private readonly Task _dispatcherTask;
        private bool _disposed;

        public event Action<Pipeline, Workspace>? PipelineStarted;
        public event Action<Pipeline, Workspace>? PipelineCompleted;
        public event Action<Pipeline, Exception>? PipelineFailed;

        public HmiCi(IEnumerable<string> workspaceDirs)
        {
            _workspaceManager = new WorkspaceManager(workspaceDirs);
            _dispatcherTask = Task.Run(DispatcherLoopAsync);
        }

        /// <summary>
        /// 提交一个 Pipeline 到队列中等待执行
        /// </summary>
        public void EnqueuePipeline(Pipeline pipeline)
        {
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            _pendingQueue.Enqueue(pipeline);
            _queueSignal.Release();
        }

        /// <summary>
        /// 调度器主循环：等待队列非空且有空闲工作区，取出 Pipeline 并执行
        /// </summary>
        private async Task DispatcherLoopAsync()
        {
            var cancellationToken = _stopCts.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                // 等待队列中有任务
                await _queueSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) break;

                // 等待有空闲工作区（异步等待）
                Workspace? workspace = null;
                try
                {
                    workspace = await _workspaceManager.AcquireAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // 从队列中取出一个 Pipeline
                if (!_pendingQueue.TryDequeue(out var pipeline))
                {
                    // 理论上不应该发生，因为队列信号已经增加
                    _workspaceManager.Release(workspace);
                    continue;
                }

                // 启动 Pipeline 执行（不等待，避免阻塞调度器）
                _ = Task.Run(() => ExecutePipelineAsync(pipeline, workspace, cancellationToken), cancellationToken);
            }
        }

        private async Task ExecutePipelineAsync(Pipeline pipeline, Workspace workspace, CancellationToken cancellationToken)
        {
            PipelineStarted?.Invoke(pipeline, workspace);
            try
            {
                await pipeline.ExecuteAsync(workspace, cancellationToken).ConfigureAwait(false);
                PipelineCompleted?.Invoke(pipeline, workspace);
            }
            catch (Exception ex)
            {
                PipelineFailed?.Invoke(pipeline, ex);
            }
            finally
            {
                _workspaceManager.Release(workspace);
            }
        }

        public async Task StopAsync()
        {
            _stopCts.Cancel();
            await _dispatcherTask.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _stopCts.Cancel();
            _stopCts.Dispose();
            _queueSignal.Dispose();
            _disposed = true;
        }
    }
}