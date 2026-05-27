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
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _pending = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();
        private readonly Task _workerTask;
        private bool _disposed;

        public ActionQueue(string name)
        {
            Name = name;
            _workerTask = Task.Run(ProcessQueue);
        }

        public string Name { get; }

        /// <summary>
        /// 提交一个任务到队列，任务会排队顺序执行
        /// </summary>
        public void Enqueue(Func<CancellationToken, Task> taskFactory)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            _pending.Enqueue(taskFactory);
            _semaphore.Release(); // 唤醒工作线程
        }

        private async Task ProcessQueue()
        {
            var token = _stopCts.Token;
            while (!token.IsCancellationRequested)
            {
                await _semaphore.WaitAsync(token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                if (_pending.TryDequeue(out var taskFactory))
                {
                    try
                    {
                        // 执行任务，传入取消令牌
                        await taskFactory(token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 记录日志，但继续处理下一个任务
                        Console.WriteLine($"[ActionQueue:{Name}] 任务执行失败: {ex.Message}");
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