using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

namespace CodexBar;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set up window properties
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Set window size
        appWindow.Resize(new Windows.Graphics.SizeInt32(400, 300));

        // Hide on startup - we're a tray app
        appWindow.Hide();
    }

    public void ShowWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Show();
    }
}
