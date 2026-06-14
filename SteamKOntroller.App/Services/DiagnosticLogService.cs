using System.IO.Compression;
using System.Text.Json;
using System.Threading.Channels;
using SteamKOntroller.Core.Diagnostics;

namespace SteamKOntroller.App.Services;

internal sealed class DiagnosticLogService : IInputDiagnosticSink, IDisposable
{
    private const long MaxLogFileBytes = 1_048_576;
    private readonly Channel<DiagnosticLogWorkItem> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false
    };
    private int _enabled;
    private int _retentionDays;
    private string? _currentPath;
    private bool _disposed;

    public DiagnosticLogService(bool enabled, int retentionDays)
    {
        DirectoryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SteamKOntroller",
            "App",
            "logs");
        Directory.CreateDirectory(DirectoryPath);

        _enabled = enabled ? 1 : 0;
        _retentionDays = AppSettingsStore.ClampRetentionDays(retentionDays);
        _channel = Channel.CreateBounded<DiagnosticLogWorkItem>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriteLoopAsync);
        QueueMaintenance();
    }

    public string DirectoryPath { get; }
    public bool IsEnabled => Volatile.Read(ref _enabled) == 1;
    public bool IsSensitiveInputEnabled => IsEnabled;
    public int RetentionDays => Volatile.Read(ref _retentionDays);
    public string? CurrentPath => Volatile.Read(ref _currentPath);

    public bool TryWrite(InputDiagnosticRecord record)
    {
        if (!IsEnabled)
        {
            if (record.ContainsSensitiveInput)
            {
                record.ClearSensitiveFields();
            }

            return false;
        }

        if (_channel.Writer.TryWrite(DiagnosticLogWorkItem.ForRecord(record)))
        {
            return true;
        }

        if (record.ContainsSensitiveInput)
        {
            record.ClearSensitiveFields();
        }

        return false;
    }

    public void SetEnabled(bool enabled)
    {
        var value = enabled ? 1 : 0;
        var previous = Interlocked.Exchange(ref _enabled, value);
        if (previous == value)
        {
            return;
        }

        if (!enabled)
        {
            ClearPendingRecords();
        }

        _channel.Writer.TryWrite(enabled
            ? DiagnosticLogWorkItem.Maintenance()
            : DiagnosticLogWorkItem.CloseCurrent());
    }

    public void SetRetentionDays(int retentionDays)
    {
        Interlocked.Exchange(ref _retentionDays, AppSettingsStore.ClampRetentionDays(retentionDays));
        QueueMaintenance();
    }

    public void OpenDirectory()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DirectoryPath,
            UseShellExecute = true
        });
    }

    private void QueueMaintenance()
    {
        _channel.Writer.TryWrite(DiagnosticLogWorkItem.Maintenance());
    }

    private async Task WriteLoopAsync()
    {
        FileStream? stream = null;
        StreamWriter? writer = null;
        string? activePath = null;

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                switch (item.Kind)
                {
                    case DiagnosticLogWorkItemKind.Record:
                        if (!IsEnabled || item.Record is null)
                        {
                            item.ClearSensitiveReference();
                            break;
                        }

                        EnsureWriter(ref stream, ref writer, ref activePath);
                        await writer!.WriteLineAsync(JsonSerializer.Serialize(item.Record, _options));
                        await writer.FlushAsync();
                        item.ClearSensitiveReference();
                        if (stream!.Length >= MaxLogFileBytes)
                        {
                            CloseWriter(ref stream, ref writer, ref activePath, compress: true);
                        }

                        break;

                    case DiagnosticLogWorkItemKind.CloseCurrent:
                        CloseWriter(ref stream, ref writer, ref activePath, compress: true);
                        break;

                    case DiagnosticLogWorkItemKind.Maintenance:
                        CompressCompletedLogs(activePath);
                        PruneExpiredLogs();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            CloseWriter(ref stream, ref writer, ref activePath, compress: true);
        }
    }

    private void EnsureWriter(ref FileStream? stream, ref StreamWriter? writer, ref string? activePath)
    {
        if (writer is not null)
        {
            return;
        }

        activePath = CreateLogPath();
        Volatile.Write(ref _currentPath, activePath);
        stream = new FileStream(activePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        writer = new StreamWriter(stream);
    }

    private void ClearPendingRecords()
    {
        while (_channel.Reader.TryRead(out var item))
        {
            item.ClearSensitiveReference();
        }
    }

    private void CloseWriter(
        ref FileStream? stream,
        ref StreamWriter? writer,
        ref string? activePath,
        bool compress)
    {
        writer?.Dispose();
        stream?.Dispose();
        writer = null;
        stream = null;

        if (activePath is not null)
        {
            if (compress)
            {
                CompressLog(activePath);
            }

            activePath = null;
            Volatile.Write(ref _currentPath, null);
        }
    }

    private string CreateLogPath()
    {
        return System.IO.Path.Combine(
            DirectoryPath,
            $"steamkontroller-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.jsonl");
    }

    private void CompressCompletedLogs(string? activePath)
    {
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.jsonl"))
        {
            if (string.Equals(path, activePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CompressLog(path);
        }
    }

    private static void CompressLog(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var sourceInfo = new FileInfo(path);
        if (sourceInfo.Length == 0)
        {
            sourceInfo.Delete();
            return;
        }

        var gzipPath = path + ".gz";
        if (!File.Exists(gzipPath))
        {
            using (var source = File.OpenRead(path))
            using (var destination = File.Create(gzipPath))
            using (var gzip = new GZipStream(destination, CompressionLevel.SmallestSize))
            {
                source.CopyTo(gzip);
            }
        }

        File.Delete(path);
    }

    private void PruneExpiredLogs()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "steamkontroller-*.jsonl*"))
        {
            var lastWriteTime = File.GetLastWriteTimeUtc(path);
            if (lastWriteTime < cutoff)
            {
                File.Delete(path);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        if (!_writerTask.Wait(TimeSpan.FromSeconds(2)))
        {
            _cts.Cancel();
            try
            {
                _writerTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
            }
        }

        _cts.Dispose();
    }

    private enum DiagnosticLogWorkItemKind
    {
        Record,
        CloseCurrent,
        Maintenance
    }

    private sealed class DiagnosticLogWorkItem
    {
        private DiagnosticLogWorkItem(DiagnosticLogWorkItemKind kind, InputDiagnosticRecord? record = null)
        {
            Kind = kind;
            Record = record;
        }

        public DiagnosticLogWorkItemKind Kind { get; }
        public InputDiagnosticRecord? Record { get; private set; }

        public static DiagnosticLogWorkItem ForRecord(InputDiagnosticRecord record) => new(
            DiagnosticLogWorkItemKind.Record,
            record);

        public static DiagnosticLogWorkItem CloseCurrent() => new(DiagnosticLogWorkItemKind.CloseCurrent);
        public static DiagnosticLogWorkItem Maintenance() => new(DiagnosticLogWorkItemKind.Maintenance);

        public void ClearSensitiveReference()
        {
            if (Record?.ContainsSensitiveInput == true)
            {
                Record.ClearSensitiveFields();
            }

            Record = null;
        }
    }
}
