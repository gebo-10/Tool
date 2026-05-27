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

        private readonly ConcurrentDictionary<string, Pipeline> _activePipelines = new ConcurrentDictionary<string, Pipeline>();

        public event Action<Pipeline, Workspace>? PipelineStarted;
        public event Action<Pipeline, Workspace>? PipelineCompleted;
        public event Action<Pipeline, Exception>? PipelineFailed;
        public event Action<Pipeline>? PipelineCancelled;

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
            if (string.IsNullOrEmpty(pipeline.Id))
                throw new ArgumentException("Pipeline 必须具有非空的 Id 属性");

            if (!_activePipelines.TryAdd(pipeline.Id, pipeline))
                throw new InvalidOperationException($"Pipeline {pipeline.Id} 已存在");

            _pendingQueue.Enqueue(pipeline);
            _queueSignal.Release();
        }

        public bool CancelPipeline(string pipelineId)
        {
            if (!_activePipelines.TryRemove(pipelineId, out var pipeline))
                return false;

            // 调用 Pipeline 自己的取消方法
            pipeline.Cancel();
            PipelineCancelled?.Invoke(pipeline);
            return true;
        }

        private async Task DispatcherLoopAsync()
        {
            var cancellationToken = _stopCts.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                await _queueSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) break;

                // 1. 先出队一个 Pipeline
                if (!_pendingQueue.TryDequeue(out var pipeline))
                    continue;

                // 2. 检查是否已被取消（不在活跃字典中）
                if (!_activePipelines.ContainsKey(pipeline.Id))
                    continue;  // 已取消，丢弃

                // 3. 用 pipeline.Id 申请 Workspace
                Workspace? workspace = null;
                try
                {
                    workspace = await _workspaceManager.AcquireAsync(pipeline.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // 如果申请过程中被停止，将 pipeline 放回队列（可选）
                    _pendingQueue.Enqueue(pipeline);
                    break;
                }

                // 4. 启动执行
                _ = Task.Run(() => ExecutePipelineAsync(pipeline, workspace, _stopCts.Token), _stopCts.Token);
            }
        }


        private async Task ExecutePipelineAsync(Pipeline pipeline, Workspace workspace, CancellationToken stopToken)
        {
            PipelineStarted?.Invoke(pipeline, workspace);
            try
            {
                // Pipeline 内部会合并自己的令牌和 stopToken
                await pipeline.ExecuteAsync(workspace, stopToken).ConfigureAwait(false);
                PipelineCompleted?.Invoke(pipeline, workspace);
            }
            catch (OperationCanceledException)
            {
                PipelineCancelled?.Invoke(pipeline);
            }
            catch (Exception ex)
            {
                PipelineFailed?.Invoke(pipeline, ex);
            }
            finally
            {
                // 从活跃字典中移除并释放 Pipeline 资源
                if (_activePipelines.TryRemove(pipeline.Id, out var _))
                    pipeline.Dispose();

                _workspaceManager.Release(workspace, pipeline.Id);
            }
        }

        public async Task StopAsync()
        {
            _stopCts.Cancel();
            await _dispatcherTask.ConfigureAwait(false);

            // 取消所有活跃的 Pipeline
            foreach (var pipeline in _activePipelines.Values.ToList())
                CancelPipeline(pipeline.Id);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _stopCts.Cancel();
            _stopCts.Dispose();
            _queueSignal.Dispose();
            foreach (var pipeline in _activePipelines.Values)
                pipeline.Dispose();
            _activePipelines.Clear();
            _disposed = true;
        }
    }
}