using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Describes the hardware of the computer for the report, so that the results of different computers can be
/// compared.
/// </summary>
internal static class SystemInformation
{
    private const string UnknownProcessor = "unknown processor";

    /// <summary>
    /// Gets the name of the processor, or a placeholder if the system does not report it.
    /// </summary>
    public static string GetProcessorName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                object? value = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString",
                    null);
                return value is string name && name.Trim().Length > 0 ? name.Trim() : UnknownProcessor;
            }

            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    int separator = line.IndexOf(':', StringComparison.Ordinal);
                    if (line.StartsWith("model name", StringComparison.Ordinal) && separator >= 0)
                    {
                        return line.Substring(separator + 1).Trim();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // The name of the processor is optional information of the report.
        }

        return UnknownProcessor;
    }

    /// <summary>
    /// Gets the memory available to the process: the physical memory of the computer or the limit of the
    /// container.
    /// </summary>
    public static long GetAvailableMemory()
    {
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }
}
