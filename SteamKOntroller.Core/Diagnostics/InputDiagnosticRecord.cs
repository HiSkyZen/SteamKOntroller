using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.Core.Diagnostics;

public sealed class InputDiagnosticRecord
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string Stage { get; init; } = string.Empty;
    public int? Vk { get; init; }
    public int? ScanCode { get; init; }
    public string? Direction { get; init; }
    public string? Flags { get; init; }
    public bool? Injected { get; init; }
    public bool? LowerIntegrityInjected { get; init; }
    public string? ExtraInfo { get; init; }
    public int? SteamCandidateScore { get; init; }
    public string? Action { get; init; }
    public string? Reason { get; init; }
    public string? ForegroundProcess { get; init; }
    public int? SendInputReturnCount { get; init; }
    public int? LastError { get; init; }

    public static InputDiagnosticRecord FromDecision(
        LowLevelKeyboardEvent keyboardEvent,
        CandidateScore score,
        BridgeDecision decision) => new()
        {
            Timestamp = keyboardEvent.Timestamp,
            Stage = decision.Action is BridgeAction.SuppressAndReinject or BridgeAction.SuppressOnly ? "suppress" : "classify",
            Vk = keyboardEvent.VirtualKey,
            ScanCode = keyboardEvent.ScanCode,
            Direction = keyboardEvent.Direction,
            Flags = keyboardEvent.FlagsHex,
            Injected = keyboardEvent.IsInjected,
            LowerIntegrityInjected = keyboardEvent.IsLowerIntegrityInjected,
            ExtraInfo = keyboardEvent.ExtraInfoHex,
            SteamCandidateScore = score.Score,
            Action = decision.Action.ToString(),
            Reason = string.IsNullOrWhiteSpace(decision.Reason) ? score.ReasonText : decision.Reason
        };

    public static InputDiagnosticRecord Reinjected(SendInputResult result) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Stage = "reinject",
        Vk = result.VirtualKey,
        ScanCode = result.ScanCode,
        Direction = "tap",
        ExtraInfo = InjectionMarker.Hex,
        Action = "SendInput",
        SendInputReturnCount = unchecked((int)result.ReturnCount),
        LastError = result.LastError
    };

    public static InputDiagnosticRecord SendInputFailure(SendInputResult result) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Stage = "error",
        Vk = result.VirtualKey,
        ScanCode = result.ScanCode,
        Direction = "tap",
        ExtraInfo = InjectionMarker.Hex,
        Action = "SendInputFailed",
        Reason = result.ErrorMessage,
        SendInputReturnCount = unchecked((int)result.ReturnCount),
        LastError = result.LastError,
        ForegroundProcess = ForegroundWindowReader.TryGetForegroundProcessName()
    };

    public static InputDiagnosticRecord Error(string stage, string reason, LowLevelKeyboardEvent keyboardEvent) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Stage = stage,
        Vk = keyboardEvent.VirtualKey,
        ScanCode = keyboardEvent.ScanCode,
        Direction = keyboardEvent.Direction,
        Flags = keyboardEvent.FlagsHex,
        Injected = keyboardEvent.IsInjected,
        LowerIntegrityInjected = keyboardEvent.IsLowerIntegrityInjected,
        ExtraInfo = keyboardEvent.ExtraInfoHex,
        Action = "error",
        Reason = reason
    };

    public static InputDiagnosticRecord Status(string stage, string action) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Stage = stage,
        Action = action
    };
}
