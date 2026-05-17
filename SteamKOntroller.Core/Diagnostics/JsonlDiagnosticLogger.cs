using System.Text.Encodings.Web;
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
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public JsonlDiagnosticLogger(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        _channel = Channel.CreateBounded<InputDiagnosticRecord>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public string Path { get; }

    public static string CreateDefaultPath()
    {
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamKOntroller",
            "App",
            "logs");
        return System.IO.Path.Combine(logDir, $"steamkontroller-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
    }

    public bool TryWrite(InputDiagnosticRecord record) => _channel.Writer.TryWrite(record);

    private async Task WriteLoopAsync()
    {
        await using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        await using var writer = new StreamWriter(stream);

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(record, _options));
                await writer.FlushAsync();
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
