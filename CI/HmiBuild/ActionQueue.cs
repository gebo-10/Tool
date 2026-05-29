using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class ActionQueue
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0); // 初始计数 0
    private readonly ConcurrentQueue<(Func<CancellationToken, Task> TaskFactory, TaskCompletionSource<bool> Tcs, CancellationToken CancellationToken)> _pending = new();
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
    /// 将任务加入队列，返回一个 Task 表示该任务的完成。
    /// </summary>
    /// <param name="taskFactory">接受 CancellationToken 的异步任务委托</param>
    /// <param name="cancellationToken">外部取消令牌，会与队列停止令牌合并</param>
    public Task EnqueueAsync(Func<CancellationToken, Task> taskFactory, CancellationToken cancellationToken = default)
    {
        if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue((taskFactory, tcs, cancellationToken));
        _semaphore.Release(); // 通知有任务
        return tcs.Task;
    }

    private async Task ProcessQueue()
    {
        var stopToken = _stopCts.Token;
        while (!stopToken.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(stopToken).ConfigureAwait(false);
            if (stopToken.IsCancellationRequested) break;

            if (_pending.TryDequeue(out var item))
            {
                // 合并队列停止令牌和调用者传入的取消令牌
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stopToken, item.CancellationToken);
                try
                {
                    await item.TaskFactory(cts.Token).ConfigureAwait(false);
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
                // 无需手动释放 cts，using 块已处理
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