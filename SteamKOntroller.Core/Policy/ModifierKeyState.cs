namespace SteamKOntroller.Core.Policy;

public sealed record ModifierKeyState(bool Control, bool Alt, bool Windows, bool Shift)
{
    public bool HasShortcutModifier => Control || Alt || Windows;
}
