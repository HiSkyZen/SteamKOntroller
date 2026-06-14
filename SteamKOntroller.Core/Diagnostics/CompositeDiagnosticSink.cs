namespace SteamKOntroller.Core.Diagnostics;

public sealed class CompositeDiagnosticSink : IInputDiagnosticSink
{
    private readonly IReadOnlyList<IInputDiagnosticSink> _sinks;

    public CompositeDiagnosticSink(params IInputDiagnosticSink[] sinks)
    {
        _sinks = sinks;
    }

    public bool IsSensitiveInputEnabled => _sinks.Any(sink => sink.IsSensitiveInputEnabled);

    public bool TryWrite(InputDiagnosticRecord record)
    {
        var wrote = false;
        foreach (var sink in _sinks)
        {
            var sinkRecord = record.ContainsSensitiveInput && !sink.IsSensitiveInputEnabled
                ? record.CopyWithoutSensitiveInput()
                : record.Copy();
            wrote |= sink.TryWrite(sinkRecord);
        }

        if (record.ContainsSensitiveInput)
        {
            record.ClearSensitiveFields();
        }

        return wrote;
    }
}
