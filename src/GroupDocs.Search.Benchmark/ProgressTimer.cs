using System;
using System.Diagnostics;
using System.Threading;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Prints a progress message at regular intervals while a long operation runs.
/// </summary>
internal sealed class ProgressTimer : IDisposable
{
    private readonly Timer timer;

    public ProgressTimer(TimeSpan interval, Func<string> describe)
    {
        long start = Stopwatch.GetTimestamp();
        TimerCallback print = state =>
            Console.WriteLine("    " + Formatting.Duration(Stopwatch.GetElapsedTime(start)) + ": " + describe());
        timer = new Timer(print, null, interval, interval);
    }

    /// <summary>
    /// Stops the messages and waits for a message that is being printed, so that no message follows the result of
    /// the operation.
    /// </summary>
    public void Dispose()
    {
        using ManualResetEvent disposed = new ManualResetEvent(false);
        if (timer.Dispose(disposed))
        {
            disposed.WaitOne();
        }
    }
}
