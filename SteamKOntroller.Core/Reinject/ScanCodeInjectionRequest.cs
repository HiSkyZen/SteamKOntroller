namespace SteamKOntroller.Core.Reinject;

public sealed record ScanCodeInjectionRequest(ushort VirtualKey, ushort ScanCode, bool Extended);
