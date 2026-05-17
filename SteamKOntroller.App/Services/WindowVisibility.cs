using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace SteamKOntroller.App.Services;

internal static class WindowVisibility
{
    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;

    public static void Hide(Window window)
    {
        ShowWindow(WindowNative.GetWindowHandle(window), SW_HIDE);
    }

    public static void Show(Window window)
    {
        ShowWindow(WindowNative.GetWindowHandle(window), SW_SHOWNORMAL);
        window.Activate();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
