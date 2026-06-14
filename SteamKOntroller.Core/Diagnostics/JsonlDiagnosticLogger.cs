using System.Text.Json;
using System.Threading.Channels;

namespace SteamKOntroller.Core.Diagnostics;

public sealed class JsonlDiagnosticLogger : IInputDiagnosticSink, IDisposable
{
    private readonly Channel<InputDiagnosticRecord> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false
    };

    public JsonlDiagnosticLogger(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        _channel = Channel.CreateBounded<InputDiagnosticRecord>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public string Path { get; }

    public bool IsSensitiveInputEnabled => true;

    public static string CreateDefaultPath()
    {
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamKOntroller",
            "App",
            "logs");
        return System.IO.Path.Combine(logDir, $"steamkontroller-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
    }

    public bool TryWrite(InputDiagnosticRecord record)
    {
        if (_channel.Writer.TryWrite(record))
        {
            return true;
        }

        if (record.ContainsSensitiveInput)
        {
            record.ClearSensitiveFields();
        }

        return false;
    }

    private async Task WriteLoopAsync()
    {
        await using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(record, _options));
                await writer.FlushAsync();
                if (record.ContainsSensitiveInput)
                {
                    record.ClearSensitiveFields();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cts.Dispose();
    }
}
