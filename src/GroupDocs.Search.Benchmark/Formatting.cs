using System;
using System.Globalization;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Formats the values of the report independently of the culture of the system.
/// </summary>
internal static class Formatting
{
    private static readonly char[] LineBreaks = ['\r', '\n'];
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a number with group separators: 1,234,567.
    /// </summary>
    public static string Count(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a number of items with the noun in the singular or the plural: 1 segment, 1,234 segments.
    /// </summary>
    public static string Count(long value, string singular, string plural)
    {
        return Count(value) + " " + (value == 1 ? singular : plural);
    }

    /// <summary>
    /// Formats a time in milliseconds for the CSV file: 1234.567.
    /// </summary>
    public static string Milliseconds(TimeSpan time)
    {
        return time.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a time in the unit that suits it: 12.345 ms, 12.345 s or 1:02:03.456.
    /// </summary>
    public static string Duration(TimeSpan? time)
    {
        if (time == null)
        {
            return string.Empty;
        }

        TimeSpan value = time.Value;
        if (value.TotalSeconds < 1)
        {
            return value.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture) + " ms";
        }

        if (value.TotalMinutes < 1)
        {
            return value.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " s";
        }

        return ((long)value.TotalHours).ToString(CultureInfo.InvariantCulture) +
            value.ToString(@"\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a size in bytes in the unit that suits it: 12.3 MB.
    /// </summary>
    public static string Size(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < SizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + SizeUnits[unit];
    }

    /// <summary>
    /// Formats the number of items per second.
    /// </summary>
    public static string Rate(long count, TimeSpan elapsed)
    {
        return elapsed.TotalSeconds > 0
            ? (count / elapsed.TotalSeconds).ToString("N1", CultureInfo.InvariantCulture)
            : "-";
    }

    /// <summary>
    /// Joins the lines of a text into one line and cuts it to the maximal length.
    /// </summary>
    public static string Shorten(string text, int maxLength)
    {
        string singleLine = string.Join(" | ", text.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= maxLength ? singleLine : string.Concat(singleLine.AsSpan(0, maxLength), "...");
    }
}
