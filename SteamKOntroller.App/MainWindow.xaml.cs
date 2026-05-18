using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SteamKOntroller.App.Services;
using Windows.Graphics;

namespace SteamKOntroller.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1040, 720));
        AppWindow.Closing += OnAppWindowClosing;

        RootFrame.Navigate(typeof(MainPage));
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
}
