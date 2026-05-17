using SteamKOntroller.Core.Diagnostics;
using SteamKOntroller.Core.Native;

namespace SteamKOntroller.App;

public sealed class DiagnosticRecordView
{
    public DiagnosticRecordView(InputDiagnosticRecord record)
    {
        Time = record.Timestamp.ToString("HH:mm:ss.fff");
        Stage = record.Stage;
        Action = record.Action ?? "-";
        Key = record.Vk is null
            ? "-"
            : $"{VirtualKeys.NameOf((ushort)record.Vk.Value)} / 0x{record.ScanCode.GetValueOrDefault():X2}";
        Direction = record.Direction ?? "-";
        Score = record.SteamCandidateScore?.ToString() ?? "-";
        Detail = record.Reason ?? record.ForegroundProcess ?? record.ExtraInfo ?? "-";
    }

    public string Time { get; }
    public string Stage { get; }
    public string Action { get; }
    public string Key { get; }
    public string Direction { get; }
    public string Score { get; }
    public string Detail { get; }
}
