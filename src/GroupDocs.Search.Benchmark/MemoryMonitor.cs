using System;
using System.Threading;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Samples the working set of the process at regular intervals and keeps its peak since the last reset, so that
/// each operation can report the memory it needed.
/// </summary>
internal sealed class MemoryMonitor : IDisposable
{
    private readonly Timer timer;
    private long peak;

    public MemoryMonitor(TimeSpan interval)
    {
        peak = Environment.WorkingSet;
        timer = new Timer(state => Sample(), null, interval, interval);
    }

    /// <summary>
    /// Starts a new period: the peak becomes the current working set.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref peak, Environment.WorkingSet);
    }

    /// <summary>
    /// Gets the peak working set since the last reset, the current working set included.
    /// </summary>
    public long GetPeak()
    {
        Sample();
        return Interlocked.Read(ref peak);
    }

    /// <summary>
    /// Stops the sampling and waits for a sample that is being taken.
    /// </summary>
    public void Dispose()
    {
        using ManualResetEvent disposed = new ManualResetEvent(false);
        if (timer.Dispose(disposed))
        {
            disposed.WaitOne();
        }
    }

    private void Sample()
    {
        long value = Environment.WorkingSet;
        long current = Interlocked.Read(ref peak);
        while (value > current)
        {
            long original = Interlocked.CompareExchange(ref peak, value, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }
}
