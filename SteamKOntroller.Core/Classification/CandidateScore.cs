namespace SteamKOntroller.Core.Classification;

public sealed record CandidateScore(int Score, string[] Reasons, int Threshold)
{
    public bool IsCandidate => Score >= Threshold;
    public string ReasonText => Reasons.Length == 0 ? "none" : string.Join(",", Reasons);
}
