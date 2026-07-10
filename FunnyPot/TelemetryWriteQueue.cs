using System.Collections.Concurrent;

namespace FunnyPot;

internal sealed class TelemetryWriteQueue : IDisposable
{
    private readonly BlockingCollection<Action> _queue;
    private readonly Thread _worker;
    private int _disposed;

    internal TelemetryWriteQueue(int capacity = 1024)
    {
        _queue = new BlockingCollection<Action>(Math.Max(1, capacity));
        _worker = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "funnypot-telemetry-writer"
        };
        _worker.Start();
    }

    internal bool TryEnqueue(Action write)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        try
        {
            return _queue.TryAdd(write);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void ProcessQueue()
    {
        try
        {
            foreach (var write in _queue.GetConsumingEnumerable())
            {
                try
                {
                    write();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to write telemetry: {ex.Message}");
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _queue.CompleteAdding();
        if (Thread.CurrentThread.ManagedThreadId != _worker.ManagedThreadId && _worker.Join(TimeSpan.FromSeconds(5)))
            _queue.Dispose();
    }
}
