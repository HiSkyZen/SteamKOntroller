namespace SteamKOntroller.Core.Diagnostics;

public sealed class EventDiagnosticSink : IInputDiagnosticSink
{
    public event EventHandler<InputDiagnosticRecord>? RecordWritten;

    public bool IsSensitiveInputEnabled => false;

    public bool TryWrite(InputDiagnosticRecord record)
    {
        RecordWritten?.Invoke(this, record);
        return true;
    }
}
