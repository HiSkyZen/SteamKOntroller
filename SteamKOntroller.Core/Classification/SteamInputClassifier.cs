using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Policy;
using SteamKOntroller.Core.Reinject;

namespace SteamKOntroller.Core.Classification;

public sealed class SteamInputClassifier
{
    public int Threshold { get; init; } = 6;

    public CandidateScore Score(LowLevelKeyboardEvent keyboardEvent)
    {
        var score = 0;
        var reasons = new List<string>(4);

        if (InjectionMarker.IsMarked(keyboardEvent.ExtraInfo))
        {
            reasons.Add("own_extra_info");
            return new CandidateScore(0, reasons.ToArray(), Threshold);
        }

        if (keyboardEvent.IsInjected)
        {
            score += 2;
            reasons.Add("injected");
        }

        if (keyboardEvent.IsLowerIntegrityInjected)
        {
            score += 3;
            reasons.Add("lower_il_injected");
        }

        if (keyboardEvent.IsInjected && SupportedKeyPolicy.IsPacketAsciiLetter(keyboardEvent))
        {
            score += 3;
            reasons.Add("packet_ascii_key");
        }

        if (SupportedKeyPolicy.IsSupported(keyboardEvent))
        {
            score += 1;
            reasons.Add("supported_key");
        }

        return new CandidateScore(score, reasons.ToArray(), Threshold);
    }
}
