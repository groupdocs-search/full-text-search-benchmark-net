using System.IO;
using System.Text;

namespace GroupDocs.Search.Benchmark;

/// <summary>
/// Writes the text to two writers: the console and the log file.
/// </summary>
internal sealed class TeeWriter : TextWriter
{
    private readonly TextWriter first;
    private readonly TextWriter second;

    public TeeWriter(TextWriter first, TextWriter second)
    {
        this.first = first;
        this.second = second;
    }

    public override Encoding Encoding => first.Encoding;

    public override void Write(char value)
    {
        first.Write(value);
        second.Write(value);
    }

    public override void Write(string? value)
    {
        first.Write(value);
        second.Write(value);
    }

    public override void WriteLine(string? value)
    {
        first.WriteLine(value);
        second.WriteLine(value);
    }

    public override void Flush()
    {
        first.Flush();
        second.Flush();
    }
}
