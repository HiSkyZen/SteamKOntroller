using System.Text.Encodings.Web;
using System.Text.Json;

namespace SteamKOntroller.InputProbe.Logging;

public sealed class JsonlLogger : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Path { get; }

    public JsonlLogger(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public void Write(InputEventRecord record)
    {
        lock (_sync)
        {
            _writer.WriteLine(JsonSerializer.Serialize(record, _options));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }
}
