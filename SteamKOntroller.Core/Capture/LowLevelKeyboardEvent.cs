using SteamKOntroller.Core.Native;

namespace SteamKOntroller.Core.Capture;

public readonly record struct LowLevelKeyboardEvent(
    DateTimeOffset Timestamp,
    int Message,
    ushort VirtualKey,
    ushort ScanCode,
    LowLevelKeyboardFlags Flags,
    UIntPtr ExtraInfo)
{
    public bool IsKeyDown => (Message is WindowMessages.WM_KEYDOWN or WindowMessages.WM_SYSKEYDOWN) && !IsKeyUp;
    public bool IsKeyUp => (Flags & LowLevelKeyboardFlags.Up) != 0 ||
                           Message is WindowMessages.WM_KEYUP or WindowMessages.WM_SYSKEYUP;
    public bool IsInjected => (Flags & LowLevelKeyboardFlags.Injected) != 0;
    public bool IsLowerIntegrityInjected => (Flags & LowLevelKeyboardFlags.LowerIntegrityInjected) != 0;
    public bool IsExtended => (Flags & LowLevelKeyboardFlags.Extended) != 0;
    public string Direction => IsKeyUp ? "up" : "down";
    public string FlagsHex => $"0x{(uint)Flags:X8}";
    public string ExtraInfoHex => $"0x{ExtraInfo.ToUInt64():X}";
}
