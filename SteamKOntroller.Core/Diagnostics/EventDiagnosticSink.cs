namespace SteamKOntroller.Core.Diagnostics;

public sealed class EventDiagnosticSink : IInputDiagnosticSink
{
    private const int MaxRecentRecords = 300;
    private readonly object _sync = new();
    private readonly Queue<InputDiagnosticRecord> _recentRecords = new(MaxRecentRecords);
    private readonly Func<bool> _isSensitiveInputEnabled;

    public event EventHandler<InputDiagnosticRecord>? RecordWritten;

    public EventDiagnosticSink()
        : this(static () => false)
    {
    }

    public EventDiagnosticSink(Func<bool> isSensitiveInputEnabled)
    {
        _isSensitiveInputEnabled = isSensitiveInputEnabled;
    }

    public bool IsSensitiveInputEnabled => _isSensitiveInputEnabled();

    public IReadOnlyList<InputDiagnosticRecord> Snapshot()
    {
        lock (_sync)
        {
            return _recentRecords.Select(record => record.Copy()).ToArray();
        }
    }

    public bool TryWrite(InputDiagnosticRecord record)
    {
        var storedRecord = record.Copy();
        lock (_sync)
        {
            _recentRecords.Enqueue(storedRecord);
            while (_recentRecords.Count > MaxRecentRecords)
            {
                _recentRecords.Dequeue().ClearSensitiveFields();
            }
        }

        RecordWritten?.Invoke(this, storedRecord.Copy());
        return true;
    }

    public void ClearSensitiveFields()
    {
        lock (_sync)
        {
            foreach (var record in _recentRecords)
            {
                record.ClearSensitiveFields();
            }
        }
    }
}
