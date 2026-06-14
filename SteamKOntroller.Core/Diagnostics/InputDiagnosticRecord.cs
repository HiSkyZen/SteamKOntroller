using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.Core.Diagnostics;

public sealed class InputDiagnosticRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Stage { get; set; } = string.Empty;
    public int? Vk { get; set; }
    public int? ScanCode { get; set; }
    public string? Direction { get; set; }
    public string? Flags { get; set; }
    public bool? Injected { get; set; }
    public bool? LowerIntegrityInjected { get; set; }
    public string? ExtraInfo { get; set; }
    public int? SteamCandidateScore { get; set; }
    public string? Action { get; set; }
    public string? Reason { get; set; }
    public string? ForegroundProcess { get; set; }
    public int? SendInputReturnCount { get; set; }
    public int? LastError { get; set; }
    public bool ContainsSensitiveInput { get; set; }

    public static InputDiagnosticRecord FromDecision(
        LowLevelKeyboardEvent keyboardEvent,
        CandidateScore score,
        BridgeDecision decision) => new()
        {
            Timestamp = keyboardEvent.Timestamp,
            Stage = decision.Action is BridgeAction.SuppressAndReinject or BridgeAction.SuppressAndSendHangulToggle or BridgeAction.SuppressOnly ? "suppress" : "classify",
            Vk = keyboardEvent.VirtualKey,
            ScanCode = keyboardEvent.ScanCode,
            Direction = keyboardEvent.Direction,
            Flags = keyboardEvent.FlagsHex,
            Injected = keyboardEvent.IsInjected,
            LowerIntegrityInjected = keyboardEvent.IsLowerIntegrityInjected,
            ExtraInfo = keyboardEvent.ExtraInfoHex,
            SteamCandidateScore = score.Score,
            Action = decision.Action.ToString(),
            Reason = string.IsNullOrWhiteSpace(decision.Reason) ? score.ReasonText : decision.Reason,
            ContainsSensitiveInput = true
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
        LastError = result.LastError,
        ContainsSensitiveInput = true
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
        ForegroundProcess = ForegroundWindowReader.TryGetForegroundProcessName(),
        ContainsSensitiveInput = true
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
        Reason = reason,
        ContainsSensitiveInput = true
    };

    public static InputDiagnosticRecord Status(string stage, string action) => new()
    {
        Timestamp = DateTimeOffset.Now,
        Stage = stage,
        Action = action
    };

    public InputDiagnosticRecord Copy() => new()
    {
        Timestamp = Timestamp,
        Stage = Stage,
        Vk = Vk,
        ScanCode = ScanCode,
        Direction = Direction,
        Flags = Flags,
        Injected = Injected,
        LowerIntegrityInjected = LowerIntegrityInjected,
        ExtraInfo = ExtraInfo,
        SteamCandidateScore = SteamCandidateScore,
        Action = Action,
        Reason = Reason,
        ForegroundProcess = ForegroundProcess,
        SendInputReturnCount = SendInputReturnCount,
        LastError = LastError,
        ContainsSensitiveInput = ContainsSensitiveInput
    };

    public InputDiagnosticRecord CopyWithoutSensitiveInput()
    {
        var copy = Copy();
        copy.ClearSensitiveFields();
        return copy;
    }

    public void ClearSensitiveFields()
    {
        Vk = null;
        ScanCode = null;
        Direction = null;
        Flags = null;
        Injected = null;
        LowerIntegrityInjected = null;
        ExtraInfo = null;
        SteamCandidateScore = null;
        ForegroundProcess = null;
        ContainsSensitiveInput = false;
    }
}
