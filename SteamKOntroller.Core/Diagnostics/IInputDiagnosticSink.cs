namespace SteamKOntroller.Core.Diagnostics;

public interface IInputDiagnosticSink
{
    bool TryWrite(InputDiagnosticRecord record);
}
