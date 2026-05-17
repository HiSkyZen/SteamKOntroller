using SteamKOntroller.Core.Native;

namespace SteamKOntroller.Core.Policy;

public static class SupportedKeyPolicy
{
    public static bool IsSupported(ushort virtualKey) =>
        VirtualKeys.IsAsciiLetter(virtualKey) ||
        virtualKey is VirtualKeys.VK_SPACE or VirtualKeys.VK_BACK or VirtualKeys.VK_RETURN;

    public static bool IsToggleHotkey(ushort virtualKey, ModifierKeyState modifiers) =>
        virtualKey == 'H' && modifiers.Control && modifiers.Alt && !modifiers.Windows;
}
