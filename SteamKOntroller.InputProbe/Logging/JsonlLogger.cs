using System.Text.Json;

namespace SteamKOntroller.InputProbe.Logging;

public sealed class JsonlLogger : IDisposable
{
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private bool _disposed;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false
    };

    public string Path { get; }

    public JsonlLogger(string path)
    {
        Path = path;
    }

    public void Write(InputEventRecord record)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureWriter();
            _writer!.WriteLine(JsonSerializer.Serialize(record, _options));
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
        {
            return;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
