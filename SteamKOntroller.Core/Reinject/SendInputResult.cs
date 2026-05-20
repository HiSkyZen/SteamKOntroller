namespace SteamKOntroller.Core.Reinject;

public sealed record SendInputResult(
    ushort VirtualKey,
    ushort ScanCode,
    uint ReturnCount,
    int LastError,
    string? ErrorMessage,
    uint ExpectedCount = 2)
{
    public bool Success => ReturnCount == ExpectedCount;
}
