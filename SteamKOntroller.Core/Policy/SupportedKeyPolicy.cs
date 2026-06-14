using SteamKOntroller.Core.Capture;
using SteamKOntroller.Core.Native;

namespace SteamKOntroller.Core.Policy;

public static class SupportedKeyPolicy
{
    public const ushort HangulToggleSentinelScanCode = 0x12D0;

    public static bool IsSupported(ushort virtualKey) =>
        VirtualKeys.IsAsciiLetter(virtualKey) ||
        virtualKey is VirtualKeys.VK_SPACE or VirtualKeys.VK_BACK or VirtualKeys.VK_RETURN;

    public static bool IsSupported(LowLevelKeyboardEvent keyboardEvent) =>
        IsSupported(keyboardEvent.VirtualKey) ||
        IsHangulToggleSentinel(keyboardEvent);

    public static bool IsHangulToggleSentinel(LowLevelKeyboardEvent keyboardEvent) =>
        keyboardEvent.VirtualKey == VirtualKeys.VK_PACKET &&
        keyboardEvent.ScanCode == HangulToggleSentinelScanCode;

    public static bool ShouldMockShiftForHangulJamo(ushort virtualKey) =>
        virtualKey is
            (ushort)'Q' or
            (ushort)'W' or
            (ushort)'E' or
            (ushort)'R' or
            (ushort)'T' or
            (ushort)'O' or
            (ushort)'P';

    public static bool IsToggleHotkey(ushort virtualKey, ModifierKeyState modifiers) =>
        virtualKey == 'H' && modifiers.Control && modifiers.Alt && !modifiers.Windows;
}
