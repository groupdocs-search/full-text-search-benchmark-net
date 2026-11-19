using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Collects the measurements: prints each of them, writes them to the CSV file and prints their summary. Copies the
/// console output to the log file, writes the errors reported by the library to the error file and samples the
/// working set of the process for the peak memory of the operations.
/// </summary>
internal sealed class BenchmarkReport : IDisposable
{
    private const double BytesPerMegabyte = 1024 * 1024;

    private static readonly SearchValues<char> CsvSpecialCharacters = SearchValues.Create(",\"\r\n");
    private static readonly TimeSpan MemorySampleInterval = TimeSpan.FromMilliseconds(250);

    private readonly List<Measurement> measurements = new List<Measurement>();
    private readonly object errorLock = new object();
    private readonly TextWriter consoleOutput;
    private readonly StreamWriter logWriter;
    private readonly StreamWriter csvWriter;
    private readonly StreamWriter errorWriter;
    private readonly MemoryMonitor memoryMonitor;
    private int errorCount;

    public BenchmarkReport(string folder)
    {
        Directory.CreateDirectory(folder);
        string name = "benchmark-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string basePath = Path.Combine(folder, name);
        LogPath = basePath + ".log";
        CsvPath = basePath + ".csv";
        ErrorPath = basePath + ".errors.log";

        logWriter = CreateWriter(LogPath);
        csvWriter = CreateWriter(CsvPath);
        errorWriter = CreateWriter(ErrorPath);
        csvWriter.WriteLine("Stage,Operation,Query,Run,TimeMs,PeakWorkingSetMB,Errors,Details");

        consoleOutput = Console.Out;
        Console.SetOut(TextWriter.Synchronized(new TeeWriter(consoleOutput, logWriter)));
        memoryMonitor = new MemoryMonitor(MemorySampleInterval);
    }

    /// <summary>
    /// Gets the path of the log file, a copy of the console output.
    /// </summary>
    public string LogPath { get; }

    /// <summary>
    /// Gets the path of the CSV file with a row for each run of each operation.
    /// </summary>
    public string CsvPath { get; }

    /// <summary>
    /// Gets the path of the file with the errors reported by the library.
    /// </summary>
    public string ErrorPath { get; }

    /// <summary>
    /// Gets the number of the errors reported by the library since the start.
    /// </summary>
    public int ErrorCount => Volatile.Read(ref errorCount);

    /// <summary>
    /// Starts the measurement of the peak working set of an operation.
    /// </summary>
    public void ResetPeakWorkingSet()
    {
        memoryMonitor.Reset();
    }

    /// <summary>
    /// Gets the peak working set of the process since the last call of <see cref="ResetPeakWorkingSet"/>.
    /// </summary>
    public long GetPeakWorkingSet()
    {
        return memoryMonitor.GetPeak();
    }

    /// <summary>
    /// Writes an error reported by the library to the error file; can be called from any thread.
    /// </summary>
    public void LogError(string stage, string message)
    {
        string time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        lock (errorLock)
        {
            errorWriter.WriteLine(time + " [" + stage + "] " + message);
        }

        Interlocked.Increment(ref errorCount);
    }

    /// <summary>
    /// Adds a measurement of an operation with one run.
    /// </summary>
    public void Add(string stage, string operation, TimeSpan time, string details, int errors, long? peakWorkingSet)
    {
        Measurement measurement = new Measurement(stage, operation, string.Empty);
        measurement.AddRun(time, details, peakWorkingSet);
        measurement.ErrorCount = errors;
        Add(measurement);
    }

    /// <summary>
    /// Adds an operation that has failed before its first run.
    /// </summary>
    public void AddFailure(string stage, string operation, string query, Exception exception)
    {
        Measurement measurement = new Measurement(stage, operation, query);
        measurement.Fail(exception);
        Add(measurement);
    }

    /// <summary>
    /// Adds a measurement: prints it and writes its runs to the CSV file.
    /// </summary>
    public void Add(Measurement measurement)
    {
        measurements.Add(measurement);
        for (int run = 0; run < measurement.Times.Count; run++)
        {
            WriteCsvRow(
                measurement,
                (run + 1).ToString(CultureInfo.InvariantCulture),
                Formatting.Milliseconds(measurement.Times[run]),
                FormatMegabytes(measurement.PeakWorkingSets[run]),
                measurement.Details[run]);
        }

        if (measurement.Failure != null)
        {
            WriteCsvRow(measurement, string.Empty, string.Empty, string.Empty, "FAILED: " + measurement.Failure);
        }

        List<string> parts = new List<string>();
        if (measurement.Times.Count == 1)
        {
            parts.Add(Formatting.Duration(measurement.Times[0]));
        }
        else if (measurement.Times.Count > 1)
        {
            parts.Add(
                "first " + Formatting.Duration(measurement.Times[0]) + ", repeated min " +
                Formatting.Duration(measurement.GetRepeatedMinimum()) + ", avg " +
                Formatting.Duration(measurement.GetRepeatedAverage()));
        }

        long? peakWorkingSet = measurement.GetPeakWorkingSet();
        if (peakWorkingSet != null)
        {
            parts.Add("peak working set " + Formatting.Size(peakWorkingSet.Value));
        }

        string result = measurement.DescribeResult();
        if (result.Length > 0)
        {
            parts.Add(result);
        }

        string query = measurement.Query.Length > 0 ? " " + measurement.Query : string.Empty;
        Console.WriteLine(
            "[" + measurement.Stage + "] " + measurement.Operation + query + ": " + string.Join(" | ", parts));
    }

    /// <summary>
    /// Prints the table of all measurements and the resources of the process.
    /// </summary>
    public void PrintSummary()
    {
        const string StageHeader = "Stage";
        const string OperationHeader = "Operation";
        const int TimeWidth = 14;
        const int MemoryWidth = 11;
        int stageWidth = measurements.Select(item => item.Stage.Length).Append(StageHeader.Length).Max();
        int operationWidth = measurements.Select(item => item.Operation.Length).Append(OperationHeader.Length).Max();

        Console.WriteLine();
        Console.WriteLine("Summary");
        Console.WriteLine(
            StageHeader.PadRight(stageWidth) + "  " + OperationHeader.PadRight(operationWidth) + "  " +
            "Time (first)".PadLeft(TimeWidth) + "  " + "Repeated min".PadLeft(TimeWidth) + "  " +
            "Repeated avg".PadLeft(TimeWidth) + "  " + "Peak memory".PadLeft(MemoryWidth) + "  Result");
        foreach (Measurement measurement in measurements)
        {
            string first = measurement.Times.Count > 0 ? Formatting.Duration(measurement.Times[0]) : string.Empty;
            long? peakWorkingSet = measurement.GetPeakWorkingSet();
            string memory = peakWorkingSet != null ? Formatting.Size(peakWorkingSet.Value) : string.Empty;
            Console.WriteLine(
                measurement.Stage.PadRight(stageWidth) + "  " + measurement.Operation.PadRight(operationWidth) + "  " +
                first.PadLeft(TimeWidth) + "  " +
                Formatting.Duration(measurement.GetRepeatedMinimum()).PadLeft(TimeWidth) + "  " +
                Formatting.Duration(measurement.GetRepeatedAverage()).PadLeft(TimeWidth) + "  " +
                memory.PadLeft(MemoryWidth) + "  " + measurement.DescribeResult());
        }

        using Process process = Process.GetCurrentProcess();
        Console.WriteLine();
        Console.WriteLine("Peak working set of the run: " + Formatting.Size(process.PeakWorkingSet64));
        Console.WriteLine("Errors reported by the library: " + Formatting.Count(ErrorCount) + ", " + ErrorPath);
        Console.WriteLine("Measurements: " + CsvPath);
        Console.WriteLine("Log: " + LogPath);
    }

    /// <summary>
    /// Stops the sampling of the memory, restores the console output and closes the files of the report.
    /// </summary>
    public void Dispose()
    {
        memoryMonitor.Dispose();
        Console.SetOut(consoleOutput);
        logWriter.Dispose();
        csvWriter.Dispose();
        errorWriter.Dispose();
    }

    private static StreamWriter CreateWriter(string path)
    {
        StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.AutoFlush = true;
        return writer;
    }

    private static string FormatMegabytes(long? bytes)
    {
        return bytes != null
            ? (bytes.Value / BytesPerMegabyte).ToString("0", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static string EscapeCsv(string value)
    {
        if (value.AsSpan().IndexOfAny(CsvSpecialCharacters) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private void WriteCsvRow(Measurement measurement, string run, string time, string memory, string details)
    {
        csvWriter.WriteLine(string.Join(
            ",",
            EscapeCsv(measurement.Stage),
            EscapeCsv(measurement.Operation),
            EscapeCsv(measurement.Query),
            run,
            time,
            memory,
            measurement.ErrorCount.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(details)));
    }
}
