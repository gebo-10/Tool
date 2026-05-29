using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BuildSystem
{
    /// <summary>
    /// 代表一个只能同时执行一个任务的资源队列
    /// </summary>
    public class ActionQueue
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<(Func<CancellationToken, Task> TaskFactory, TaskCompletionSource<bool> Tcs)> _pending = new();
        private readonly CancellationTokenSource _stopCts = new();
        private readonly Task _workerTask;
        private bool _disposed;

        public string Name { get; }

        public ActionQueue(string name)
        {
            Name = name;
            _workerTask = Task.Run(ProcessQueue);
        }

        /// <summary>
        /// 提交一个任务到队列，返回一个 Task 表示该任务的完成
        /// </summary>
        public Task EnqueueAsync(Func<CancellationToken, Task> taskFactory)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Enqueue((taskFactory, tcs));
            _semaphore.Release(); // 唤醒工作线程
            return tcs.Task;
        }

        private async Task ProcessQueue()
        {
            var token = _stopCts.Token;
            while (!token.IsCancellationRequested)
            {
                await _semaphore.WaitAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                if (_pending.TryDequeue(out var item))
                {
                    try
                    {
                        await item.TaskFactory(token).ConfigureAwait(false);
                        item.Tcs.TrySetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        item.Tcs.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        item.Tcs.TrySetException(ex);
                    }
                }
            }
        }

        public async Task StopAsync()
        {
            _stopCts.Cancel();
            await _workerTask.ConfigureAwait(false);
            _stopCts.Dispose();
            _semaphore.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _stopCts.Cancel();
            _workerTask.Wait(5000);
            _stopCts.Dispose();
            _semaphore.Dispose();
            _disposed = true;
        }
    }
}