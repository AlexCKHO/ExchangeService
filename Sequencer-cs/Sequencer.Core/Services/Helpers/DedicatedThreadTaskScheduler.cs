using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DedicatedThreadTaskScheduler : TaskScheduler, IDisposable
{
    private readonly BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
    private readonly Thread _thread;

    public DedicatedThreadTaskScheduler(int? pinToCpuCore = null)
    {
        _thread = new Thread(() =>
        {
            if (pinToCpuCore.HasValue)
            {

            }
            else
            {
                Loop();
            }
        })
        {
            IsBackground = true
        };

        _thread.Start();
    }

    private void Loop()
    {
        foreach (var task in _tasks.GetConsumingEnumerable())
        {
            TryExecuteTask(task);
        }
    }

    protected override void QueueTask(Task task) => _tasks.Add(task);

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (Thread.CurrentThread == _thread)
        {
            return TryExecuteTask(task);
        }
        return false;
    }

    protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

    public void Dispose() => _tasks.CompleteAdding();
}