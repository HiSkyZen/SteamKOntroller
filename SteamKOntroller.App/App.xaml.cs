using Microsoft.UI.Xaml;
using SteamKOntroller.App.Services;

namespace SteamKOntroller.App;

public partial class App : Application
{
    private Window? _window;
    private TrayIconService? _tray;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    internal BridgeRuntime Runtime { get; private set; } = null!;
    internal bool IsExiting { get; private set; }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Runtime = BridgeRuntime.Create();
        _window = new MainWindow();
        _tray = new TrayIconService(_window, Runtime);
        _tray.ShowRequested += (_, _) => ShowMainWindow();
        _tray.ToggleRequested += (_, _) => Runtime.Bridge.ToggleEnabled();
        _tray.OpenLogsRequested += (_, _) => Runtime.OpenLogFolder();
        _tray.ExitRequested += (_, _) => ExitApplication();
        Runtime.State.EnabledChanged += (_, enabled) => _tray.UpdateEnabled(enabled);

        _tray.UpdateEnabled(Runtime.Bridge.Enabled);
        Runtime.Start();
        _window.Activate();
        if (!ShouldShowWindow(args.Arguments))
        {
            WindowVisibility.Hide(_window);
        }
    }

    internal void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        WindowVisibility.Show(_window);
    }

    internal void ExitApplication()
    {
        if (IsExiting)
        {
            return;
        }

        IsExiting = true;
        _tray?.Dispose();
        Runtime.Dispose();
        _window?.Close();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Runtime?.ReportError("unhandled", e.Message);
    }

    private static bool ShouldShowWindow(string arguments)
    {
        return arguments.Contains("--show", StringComparison.OrdinalIgnoreCase)
            || arguments.Contains("--window", StringComparison.OrdinalIgnoreCase);
    }
}
