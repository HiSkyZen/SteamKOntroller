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

        if (!SupportedKeyPolicy.IsSupported(keyboardEvent.VirtualKey))
        {
            return BridgeDecision.PassThrough("unsupported_key");
        }

        if (modifiers.HasShortcutModifier)
        {
            return BridgeDecision.PassThrough("shortcut_modifier");
        }

        if (!score.IsCandidate)
        {
            return BridgeDecision.PassThrough("not_steam_candidate");
        }

        if (SupportedKeyPolicy.IsHangulToggleSentinel(keyboardEvent.VirtualKey))
        {
            return keyboardEvent.IsKeyUp
                ? new BridgeDecision(BridgeAction.SuppressOnly, "hangul_toggle_key_up")
                : new BridgeDecision(BridgeAction.SuppressAndSendHangulToggle, "hangul_toggle_key_down");
        }

        return keyboardEvent.IsKeyUp
            ? new BridgeDecision(BridgeAction.SuppressOnly, "candidate_key_up")
            : new BridgeDecision(
                BridgeAction.SuppressAndReinject,
                "candidate_key_down",
                MockShift: modifiers.Shift && VirtualKeys.IsAsciiLetter(keyboardEvent.VirtualKey));
    }
}
