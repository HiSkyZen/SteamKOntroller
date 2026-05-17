namespace SteamKOntroller.Core.Reinject;

public static class InjectionMarker
{
    private const ulong Marker64 = 0x534B4F4E54524C01UL;
    private const uint Marker32 = 0x54524C01U;

    public static UIntPtr Value => UIntPtr.Size == 8 ? new UIntPtr(Marker64) : new UIntPtr(Marker32);
    public static string Hex => UIntPtr.Size == 8 ? $"0x{Marker64:X}" : $"0x{Marker32:X}";

    public static bool IsMarked(UIntPtr extraInfo) => extraInfo == Value;
}
