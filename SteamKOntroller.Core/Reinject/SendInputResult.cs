namespace SteamKOntroller.Core.Reinject;

public sealed record SendInputResult(
    ushort VirtualKey,
    ushort ScanCode,
    uint ReturnCount,
    int LastError,
    string? ErrorMessage)
{
    public bool Success => ReturnCount == 2;
}
