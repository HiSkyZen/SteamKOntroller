using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Classification;
using SteamKOntroller.Core.Native;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.Core.Policy;

public sealed class SuppressionPolicy
{
    public BridgeDecision Decide(
        LowLevelKeyboardEvent keyboardEvent,
        ModifierKeyState modifiers,
        bool enabled,
        CandidateScore score)
    {
        if (InjectionMarker.IsMarked(keyboardEvent.ExtraInfo))
        {
            return new BridgeDecision(BridgeAction.LoopGuarded, "own_extra_info");
        }

        if (keyboardEvent.IsKeyDown && SupportedKeyPolicy.IsToggleHotkey(keyboardEvent.VirtualKey, modifiers))
        {
            return new BridgeDecision(BridgeAction.Toggle, "ctrl_alt_h");
        }

        if (!enabled)
        {
            return BridgeDecision.PassThrough("disabled");
        }

        if (!SupportedKeyPolicy.IsSupported(keyboardEvent))
        {
            return BridgeDecision.PassThrough("unsupported_key");
        }

        if (!score.IsCandidate)
        {
            return BridgeDecision.PassThrough("not_steam_candidate");
        }

        if (SupportedKeyPolicy.IsHangulToggleSentinel(keyboardEvent))
        {
            return keyboardEvent.IsKeyUp
                ? new BridgeDecision(BridgeAction.SuppressOnly, "packet_hangul_toggle_key_up")
                : new BridgeDecision(BridgeAction.SuppressAndSendHangulToggle, "packet_hangul_toggle_key_down");
        }

        if (modifiers.HasShortcutModifier)
        {
            return BridgeDecision.PassThrough("shortcut_modifier");
        }

        return keyboardEvent.IsKeyUp
            ? new BridgeDecision(BridgeAction.SuppressOnly, "candidate_key_up")
            : new BridgeDecision(
                BridgeAction.SuppressAndReinject,
                "candidate_key_down",
                MockShift: modifiers.Shift && SupportedKeyPolicy.ShouldMockShiftForHangulJamo(keyboardEvent.VirtualKey));
    }
}
