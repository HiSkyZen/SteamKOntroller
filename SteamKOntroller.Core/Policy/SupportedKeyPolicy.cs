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
        IsHangulToggleSentinel(keyboardEvent) ||
        TryGetReinjectTarget(keyboardEvent, out _);

    public static bool IsHangulToggleSentinel(LowLevelKeyboardEvent keyboardEvent) =>
        keyboardEvent.ScanCode == HangulToggleSentinelScanCode &&
        (keyboardEvent.VirtualKey == VirtualKeys.VK_PACKET ||
         keyboardEvent.VirtualKey == HangulToggleSentinelScanCode);

    public static bool IsPacketAsciiLetter(LowLevelKeyboardEvent keyboardEvent) =>
        keyboardEvent.VirtualKey == VirtualKeys.VK_PACKET &&
        IsAsciiLetterCode(keyboardEvent.ScanCode);

    public static bool TryGetReinjectTarget(
        LowLevelKeyboardEvent keyboardEvent,
        out ReinjectKeyTarget target)
    {
        if (TryGetPacketAsciiLetter(keyboardEvent, out var packetVirtualKey, out var packetShifted) &&
            TryGetLetterScanCode(packetVirtualKey, out var packetScanCode))
        {
            target = new ReinjectKeyTarget(
                packetVirtualKey,
                packetScanCode,
                Extended: false,
                Shifted: packetShifted);
            return true;
        }

        if (VirtualKeys.IsAsciiLetter(keyboardEvent.VirtualKey))
        {
            var scanCode = keyboardEvent.ScanCode;
            if (scanCode == 0 && TryGetLetterScanCode(keyboardEvent.VirtualKey, out var mappedScanCode))
            {
                scanCode = mappedScanCode;
            }

            target = new ReinjectKeyTarget(
                keyboardEvent.VirtualKey,
                scanCode,
                keyboardEvent.IsExtended,
                Shifted: false);
            return true;
        }

        if (keyboardEvent.VirtualKey is VirtualKeys.VK_SPACE or VirtualKeys.VK_BACK or VirtualKeys.VK_RETURN)
        {
            target = new ReinjectKeyTarget(
                keyboardEvent.VirtualKey,
                keyboardEvent.ScanCode,
                keyboardEvent.IsExtended,
                Shifted: false);
            return true;
        }

        target = default;
        return false;
    }

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

    private static bool TryGetPacketAsciiLetter(
        LowLevelKeyboardEvent keyboardEvent,
        out ushort virtualKey,
        out bool shifted)
    {
        virtualKey = 0;
        shifted = false;

        if (keyboardEvent.VirtualKey != VirtualKeys.VK_PACKET)
        {
            return false;
        }

        if (keyboardEvent.ScanCode is >= (ushort)'A' and <= (ushort)'Z')
        {
            virtualKey = keyboardEvent.ScanCode;
            shifted = true;
            return true;
        }

        if (keyboardEvent.ScanCode is >= (ushort)'a' and <= (ushort)'z')
        {
            virtualKey = (ushort)(keyboardEvent.ScanCode - ('a' - 'A'));
            return true;
        }

        return false;
    }

    private static bool IsAsciiLetterCode(ushort code) =>
        code is >= (ushort)'A' and <= (ushort)'Z' or >= (ushort)'a' and <= (ushort)'z';

    private static bool TryGetLetterScanCode(ushort virtualKey, out ushort scanCode)
    {
        scanCode = virtualKey switch
        {
            (ushort)'Q' => 0x10,
            (ushort)'W' => 0x11,
            (ushort)'E' => 0x12,
            (ushort)'R' => 0x13,
            (ushort)'T' => 0x14,
            (ushort)'Y' => 0x15,
            (ushort)'U' => 0x16,
            (ushort)'I' => 0x17,
            (ushort)'O' => 0x18,
            (ushort)'P' => 0x19,
            (ushort)'A' => 0x1E,
            (ushort)'S' => 0x1F,
            (ushort)'D' => 0x20,
            (ushort)'F' => 0x21,
            (ushort)'G' => 0x22,
            (ushort)'H' => 0x23,
            (ushort)'J' => 0x24,
            (ushort)'K' => 0x25,
            (ushort)'L' => 0x26,
            (ushort)'Z' => 0x2C,
            (ushort)'X' => 0x2D,
            (ushort)'C' => 0x2E,
            (ushort)'V' => 0x2F,
            (ushort)'B' => 0x30,
            (ushort)'N' => 0x31,
            (ushort)'M' => 0x32,
            _ => 0
        };

        return scanCode != 0;
    }
}

public readonly record struct ReinjectKeyTarget(ushort VirtualKey, ushort ScanCode, bool Extended, bool Shifted);
