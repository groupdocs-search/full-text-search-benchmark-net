using System;
using System.Collections.Generic;
using System.Linq;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// A measured operation: one or several runs with their times, peak memory and the descriptions of their results.
/// </summary>
internal sealed class Measurement
{
    private readonly List<TimeSpan> times = new List<TimeSpan>();
    private readonly List<string> details = new List<string>();
    private readonly List<long?> peakWorkingSets = new List<long?>();

    public Measurement(string stage, string operation, string query)
    {
        Stage = stage;
        Operation = operation;
        Query = query;
    }

    /// <summary>
    /// Gets the stage of the benchmark: extraction, indexing, optimization or search.
    /// </summary>
    public string Stage { get; }

    /// <summary>
    /// Gets the name of the operation.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Gets the query of a search, or an empty string.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Gets the times of the runs.
    /// </summary>
    public IReadOnlyList<TimeSpan> Times => times;

    /// <summary>
    /// Gets the descriptions of the results of the runs.
    /// </summary>
    public IReadOnlyList<string> Details => details;

    /// <summary>
    /// Gets the peak working sets of the process during the runs, in bytes; null where it is not measured.
    /// </summary>
    public IReadOnlyList<long?> PeakWorkingSets => peakWorkingSets;

    /// <summary>
    /// Gets the description of the exception that has stopped the operation, or null.
    /// </summary>
    public string? Failure { get; private set; }

    /// <summary>
    /// Gets or sets the number of the errors reported by the library during the operation.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Adds a run of the operation.
    /// </summary>
    public void AddRun(TimeSpan time, string runDetails, long? peakWorkingSet)
    {
        times.Add(time);
        details.Add(runDetails);
        peakWorkingSets.Add(peakWorkingSet);
    }

    /// <summary>
    /// Marks the operation as stopped by an exception.
    /// </summary>
    public void Fail(Exception exception)
    {
        Failure = exception.GetType().Name + ": " + exception.Message;
    }

    /// <summary>
    /// Gets the minimal time of the repeated runs (all runs except the first one).
    /// </summary>
    public TimeSpan? GetRepeatedMinimum()
    {
        return times.Count > 1 ? times.Skip(1).Min() : null;
    }

    /// <summary>
    /// Gets the average time of the repeated runs (all runs except the first one).
    /// </summary>
    public TimeSpan? GetRepeatedAverage()
    {
        return times.Count > 1 ? TimeSpan.FromTicks((long)times.Skip(1).Average(time => time.Ticks)) : null;
    }

    /// <summary>
    /// Gets the peak working set of all runs, or null if it is not measured.
    /// </summary>
    public long? GetPeakWorkingSet()
    {
        return peakWorkingSets.Max();
    }

    /// <summary>
    /// Describes the result of the operation: the result of the first run, the failure and the errors.
    /// </summary>
    public string DescribeResult()
    {
        List<string> parts = new List<string>();
        if (details.Count > 0 && details[0].Length > 0)
        {
            parts.Add(details[0]);
        }

        if (Failure != null)
        {
            parts.Add("FAILED: " + Failure);
        }

        if (ErrorCount > 0)
        {
            parts.Add(Formatting.Count(ErrorCount, "error", "errors"));
        }

        return string.Join("; ", parts);
    }
}
