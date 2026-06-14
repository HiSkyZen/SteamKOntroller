namespace SteamKOntroller.Core.Policy;

public sealed record BridgeDecision(
    BridgeAction Action,
    string Reason,
    bool MockShift = false,
    ushort? ReinjectVirtualKey = null,
    ushort? ReinjectScanCode = null,
    bool? ReinjectExtended = null)
{
    public static BridgeDecision PassThrough(string reason) => new(BridgeAction.PassThrough, reason);
}

public enum BridgeAction
{
    PassThrough,
    Toggle,
    LoopGuarded,
    SuppressOnly,
    SuppressAndSendHangulToggle,
    SuppressAndReinject
}
