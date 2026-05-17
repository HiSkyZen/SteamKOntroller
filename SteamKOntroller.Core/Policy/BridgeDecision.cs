namespace SteamKOntroller.Core.Policy;

public sealed record BridgeDecision(BridgeAction Action, string Reason)
{
    public static BridgeDecision PassThrough(string reason) => new(BridgeAction.PassThrough, reason);
}

public enum BridgeAction
{
    PassThrough,
    Toggle,
    LoopGuarded,
    SuppressOnly,
    SuppressAndReinject
}
