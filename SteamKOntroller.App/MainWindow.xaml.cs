using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SteamKOntroller.App.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace SteamKOntroller.App;

public sealed partial class MainWindow : Window
{
    private const int BaseDpi = 96;
    private const double DefaultWindowWidth = 1080;
    private const double DefaultWindowHeight = 680;
    private const double MinimumWindowWidth = 720;
    private const double MinimumWindowHeight = 480;
    private const double WorkAreaUsage = 0.9;

    public MainWindow()
    {
        InitializeComponent();

        ConfigureBackdropFallback();
        ConfigureTitleBar();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        ConfigureWindowSize();
        AppWindow.Closing += OnAppWindowClosing;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void ConfigureBackdropFallback()
    {
        if (MicaController.IsSupported())
        {
            return;
        }

        if (Application.Current.Resources.TryGetValue("SolidBackgroundFillColorBaseBrush", out var value)
            && value is Brush fallbackBrush)
        {
            RootShell.Background = fallbackBrush;
        }
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        SetTitleBar(AppTitleBar);
    }

    private void ConfigureWindowSize()
    {
        var scale = GetWindowScale();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = ToPhysicalPixels(MinimumWindowWidth, scale);
            presenter.PreferredMinimumHeight = ToPhysicalPixels(MinimumWindowHeight, scale);
        }

        var clientSize = new SizeInt32(
            ToPhysicalPixels(DefaultWindowWidth, scale),
            ToPhysicalPixels(DefaultWindowHeight, scale));
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            clientSize.Width = Math.Min(clientSize.Width, ToAvailablePixels(displayArea.WorkArea.Width));
            clientSize.Height = Math.Min(clientSize.Height, ToAvailablePixels(displayArea.WorkArea.Height));
        }

        AppWindow.ResizeClient(clientSize);
        CenterWindow(displayArea);
    }

    private double GetWindowScale()
    {
        var scale = (Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 0;
        if (scale > 0)
        {
            return scale;
        }

        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(this));
        return dpi > 0 ? dpi / (double)BaseDpi : 1;
    }

    private void CenterWindow(DisplayArea? displayArea)
    {
        if (displayArea is null)
        {
            return;
        }

        var workArea = displayArea.WorkArea;
        AppWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - AppWindow.Size.Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - AppWindow.Size.Height) / 2)));
    }

    private static int ToPhysicalPixels(double effectivePixels, double scale)
    {
        return Math.Max(1, (int)Math.Round(effectivePixels * scale));
    }

    private static int ToAvailablePixels(int workAreaPixels)
    {
        return Math.Max(1, (int)Math.Round(workAreaPixels * WorkAreaUsage));
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var app = (App)Application.Current;
        if (app.IsExiting)
        {
            return;
        }

        args.Cancel = true;
        WindowVisibility.Hide(this);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
