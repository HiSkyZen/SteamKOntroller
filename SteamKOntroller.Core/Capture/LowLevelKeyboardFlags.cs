namespace SteamKOntroller.Core.Capture;

[Flags]
public enum LowLevelKeyboardFlags : uint
{
    None = 0x00,
    Extended = 0x01,
    LowerIntegrityInjected = 0x02,
    Injected = 0x10,
    AltDown = 0x20,
    Up = 0x80
}
