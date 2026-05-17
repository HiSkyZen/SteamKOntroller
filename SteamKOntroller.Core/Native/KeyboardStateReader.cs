using System.Runtime.InteropServices;
using SteamKOntroller.Core.Policy;

namespace SteamKOntroller.Core.Native;

public static class KeyboardStateReader
{
    public static ModifierKeyState ReadModifiers() => new(
        IsDown(VirtualKeys.VK_CONTROL) || IsDown(VirtualKeys.VK_LCONTROL) || IsDown(VirtualKeys.VK_RCONTROL),
        IsDown(VirtualKeys.VK_MENU) || IsDown(VirtualKeys.VK_LMENU) || IsDown(VirtualKeys.VK_RMENU),
        IsDown(VirtualKeys.VK_LWIN) || IsDown(VirtualKeys.VK_RWIN),
        IsDown(VirtualKeys.VK_SHIFT) || IsDown(VirtualKeys.VK_LSHIFT) || IsDown(VirtualKeys.VK_RSHIFT));

    private static bool IsDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
