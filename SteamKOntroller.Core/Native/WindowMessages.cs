namespace SteamKOntroller.Core.Native;

public static class WindowMessages
{
    public const int WM_QUIT = 0x0012;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    public static string NameOf(int message) => message switch
    {
        WM_KEYDOWN => nameof(WM_KEYDOWN),
        WM_KEYUP => nameof(WM_KEYUP),
        WM_SYSKEYDOWN => nameof(WM_SYSKEYDOWN),
        WM_SYSKEYUP => nameof(WM_SYSKEYUP),
        WM_QUIT => nameof(WM_QUIT),
        _ => $"0x{message:X4}"
    };
}
