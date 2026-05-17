using System.Diagnostics;
using SteamKOntroller.Core;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Diagnostics;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.App.Services;

internal sealed class BridgeRuntime : IDisposable
{
    private readonly JsonlDiagnosticLogger _logger;
    private readonly EventDiagnosticSink _events;
    private bool _disposed;

    private BridgeRuntime(
        AppState state,
        BridgeCounters counters,
        JsonlDiagnosticLogger logger,
        EventDiagnosticSink events,
        SteamInputBridge bridge)
    {
        State = state;
        Counters = counters;
        _logger = logger;
        _events = events;
        Bridge = bridge;
    }

    public AppState State { get; }
    public BridgeCounters Counters { get; }
    public SteamInputBridge Bridge { get; }
    public string LogPath => _logger.Path;
    public string? LastStartupError { get; private set; }

    public event EventHandler<InputDiagnosticRecord>? RecordWritten
    {
        add => _events.RecordWritten += value;
        remove => _events.RecordWritten -= value;
    }

    public static BridgeRuntime Create()
    {
        var state = new AppState();
        var counters = new BridgeCounters();
        var logger = new JsonlDiagnosticLogger(JsonlDiagnosticLogger.CreateDefaultPath());
        var events = new EventDiagnosticSink();
        var diagnostics = new CompositeDiagnosticSink(logger, events);
        var bridge = new SteamInputBridge(
            state,
            new SteamInputClassifier(),
            new SuppressionPolicy(),
            new ScancodeReinjector(),
            diagnostics,
            counters);

        return new BridgeRuntime(state, counters, logger, events, bridge);
    }

    public void Start()
    {
        try
        {
            Bridge.Start();
            LastStartupError = null;
        }
        catch (Exception ex)
        {
            LastStartupError = ex.Message;
            ReportError("hook", ex.Message);
        }
    }

    public void ReportError(string stage, string message)
    {
        _events.TryWrite(new InputDiagnosticRecord
        {
            Timestamp = DateTimeOffset.Now,
            Stage = "error",
            Action = stage,
            Reason = message
        });
    }

    public void OpenLogFolder()
    {
        var logFolder = Path.GetDirectoryName(LogPath);
        if (string.IsNullOrWhiteSpace(logFolder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = logFolder,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Bridge.Dispose();
        _logger.Dispose();
    }
}
