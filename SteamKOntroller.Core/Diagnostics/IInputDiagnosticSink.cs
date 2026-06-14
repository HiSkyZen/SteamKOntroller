namespace SteamKOntroller.Core.Diagnostics;

public interface IInputDiagnosticSink
{
    bool IsSensitiveInputEnabled { get; }

    bool TryWrite(InputDiagnosticRecord record);
}
