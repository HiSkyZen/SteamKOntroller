namespace SteamKOntroller.App.Services;

internal sealed class AppSettings
{
    public bool DiagnosticLoggingEnabled { get; set; }
    public int LogRetentionDays { get; set; } = 14;
}
