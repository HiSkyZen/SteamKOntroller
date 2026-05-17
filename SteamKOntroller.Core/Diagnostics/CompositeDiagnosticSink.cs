namespace SteamKOntroller.Core.Diagnostics;

public sealed class CompositeDiagnosticSink : IInputDiagnosticSink
{
    private readonly IReadOnlyList<IInputDiagnosticSink> _sinks;

    public CompositeDiagnosticSink(params IInputDiagnosticSink[] sinks)
    {
        _sinks = sinks;
    }

    public bool TryWrite(InputDiagnosticRecord record)
    {
        var wrote = false;
        foreach (var sink in _sinks)
        {
            wrote |= sink.TryWrite(record);
        }

        return wrote;
    }
}
