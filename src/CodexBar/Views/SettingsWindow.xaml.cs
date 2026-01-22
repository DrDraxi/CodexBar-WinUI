using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;

namespace CodexBar.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        this.InitializeComponent();

        // Set window size and title
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
        appWindow.Title = "CodexBar Settings";

        // Disable minimize and maximize buttons, set min size
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(true, true);
        }

        // Make title bar transparent/blend with content
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        // Select first item by default
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "general":
                    ContentFrame.Navigate(typeof(GeneralSettingsPage));
                    break;
                case "providers":
                    ContentFrame.Navigate(typeof(ProvidersSettingsPage));
                    break;
                case "about":
                    ContentFrame.Navigate(typeof(AboutSettingsPage));
                    break;
            }
        }
    }
}
