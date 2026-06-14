using SteamKOntroller.Core;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Diagnostics;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.App.Services;

internal sealed class BridgeRuntime : IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly DiagnosticLogService _diagnosticLogs;
    private readonly EventDiagnosticSink _events;
    private bool _disposed;

    private BridgeRuntime(
        AppSettingsStore settingsStore,
        AppState state,
        BridgeCounters counters,
        DiagnosticLogService diagnosticLogs,
        EventDiagnosticSink events,
        SteamInputBridge bridge)
    {
        _settingsStore = settingsStore;
        State = state;
        Counters = counters;
        _diagnosticLogs = diagnosticLogs;
        _events = events;
        Bridge = bridge;
    }

    public AppSettings Settings => _settingsStore.Settings;
    public AppState State { get; }
    public BridgeCounters Counters { get; }
    public SteamInputBridge Bridge { get; }
    public string LogDirectory => _diagnosticLogs.DirectoryPath;
    public string? CurrentLogPath => _diagnosticLogs.CurrentPath;
    public string? LastStartupError { get; private set; }

    public event EventHandler? SettingsChanged;

    public event EventHandler<InputDiagnosticRecord>? RecordWritten
    {
        add => _events.RecordWritten += value;
        remove => _events.RecordWritten -= value;
    }

    public static BridgeRuntime Create()
    {
        var settingsStore = AppSettingsStore.LoadDefault();
        var state = new AppState();
        var counters = new BridgeCounters();
        var logger = new DiagnosticLogService(
            settingsStore.Settings.DiagnosticLoggingEnabled,
            settingsStore.Settings.LogRetentionDays);
        var events = new EventDiagnosticSink();
        var diagnostics = new CompositeDiagnosticSink(logger, events);
        var bridge = new SteamInputBridge(
            state,
            new SteamInputClassifier(),
            new SuppressionPolicy(),
            new ScancodeReinjector(),
            diagnostics,
            counters);

        return new BridgeRuntime(settingsStore, state, counters, logger, events, bridge);
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
        _diagnosticLogs.OpenDirectory();
    }

    public void SetDiagnosticLoggingEnabled(bool enabled)
    {
        if (Settings.DiagnosticLoggingEnabled == enabled)
        {
            return;
        }

        if (!enabled)
        {
            _diagnosticLogs.SetEnabled(false);
        }

        Settings.DiagnosticLoggingEnabled = enabled;
        _settingsStore.Save();

        if (enabled)
        {
            _diagnosticLogs.SetEnabled(true);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLogRetentionDays(int days)
    {
        var clampedDays = AppSettingsStore.ClampRetentionDays(days);
        if (Settings.LogRetentionDays == clampedDays)
        {
            return;
        }

        Settings.LogRetentionDays = clampedDays;
        _settingsStore.Save();
        _diagnosticLogs.SetRetentionDays(clampedDays);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Bridge.Dispose();
        _diagnosticLogs.Dispose();
    }
}
