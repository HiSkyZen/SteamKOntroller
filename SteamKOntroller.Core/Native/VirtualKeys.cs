namespace SteamKOntroller.Core.Native;

public static class VirtualKeys
{
    public const int VK_BACK = 0x08;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_HANGUL = 0x15;
    public const int VK_SPACE = 0x20;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_F24 = 0x87;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;

    public static bool IsAsciiLetter(ushort virtualKey) => virtualKey is >= (ushort)'A' and <= (ushort)'Z';

    public static string NameOf(ushort virtualKey) => virtualKey switch
    {
        VK_BACK => "Backspace",
        VK_RETURN => "Enter",
        VK_HANGUL => "Hangul",
        VK_SPACE => "Space",
        VK_F24 => "F24",
        >= (ushort)'A' and <= (ushort)'Z' => ((char)virtualKey).ToString(),
        _ => $"VK 0x{virtualKey:X2}"
    };
}
