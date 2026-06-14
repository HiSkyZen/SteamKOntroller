namespace SteamKOntroller.Core.Reinject;

public readonly record struct ScanCodeInjectionRequest(ushort VirtualKey, ushort ScanCode, bool Extended, bool MockShift = false);
