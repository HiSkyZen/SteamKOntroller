namespace SteamKOntroller.InputProbe.Native;

public static class WindowMessageNames
{
    public const int WM_INPUT = 0x00FF;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_CHAR = 0x0102;
    public const int WM_DEADCHAR = 0x0103;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_SYSCHAR = 0x0106;
    public const int WM_UNICHAR = 0x0109;
    public const int WM_IME_STARTCOMPOSITION = 0x010D;
    public const int WM_IME_ENDCOMPOSITION = 0x010E;
    public const int WM_IME_COMPOSITION = 0x010F;
    public const int WM_IME_CHAR = 0x0286;

    public static string NameOf(int msg) => msg switch
    {
        WM_INPUT => nameof(WM_INPUT),
        WM_KEYDOWN => nameof(WM_KEYDOWN),
        WM_KEYUP => nameof(WM_KEYUP),
        WM_CHAR => nameof(WM_CHAR),
        WM_DEADCHAR => nameof(WM_DEADCHAR),
        WM_SYSKEYDOWN => nameof(WM_SYSKEYDOWN),
        WM_SYSKEYUP => nameof(WM_SYSKEYUP),
        WM_SYSCHAR => nameof(WM_SYSCHAR),
        WM_UNICHAR => nameof(WM_UNICHAR),
        WM_IME_STARTCOMPOSITION => nameof(WM_IME_STARTCOMPOSITION),
        WM_IME_ENDCOMPOSITION => nameof(WM_IME_ENDCOMPOSITION),
        WM_IME_COMPOSITION => nameof(WM_IME_COMPOSITION),
        WM_IME_CHAR => nameof(WM_IME_CHAR),
        _ => $"0x{msg:X4}"
    };

    public static bool IsInteresting(int msg) => msg is
        WM_INPUT or
        WM_KEYDOWN or WM_KEYUP or WM_CHAR or WM_DEADCHAR or
        WM_SYSKEYDOWN or WM_SYSKEYUP or WM_SYSCHAR or WM_UNICHAR or
        WM_IME_STARTCOMPOSITION or WM_IME_COMPOSITION or WM_IME_ENDCOMPOSITION or WM_IME_CHAR;
}
