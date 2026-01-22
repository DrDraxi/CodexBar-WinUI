using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using CodexBar.Services;

namespace CodexBar.Views;

public sealed partial class GeneralSettingsPage : Page
{
    private const string AppName = "CodexBar";
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private bool _isLoading = true;

    public GeneralSettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        var settings = SettingsService.Instance.Settings;

        // Set refresh frequency
        var refreshMinutes = settings.RefreshIntervalMinutes;
        for (int i = 0; i < RefreshFrequencyCombo.Items.Count; i++)
        {
            if (RefreshFrequencyCombo.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == refreshMinutes.ToString())
            {
                RefreshFrequencyCombo.SelectedIndex = i;
                break;
            }
        }

        // Check actual registry state for start with Windows
        StartWithWindowsToggle.IsOn = IsStartupEnabled();

        // Set taskbar widget toggle state
        TaskbarWidgetToggle.IsOn = SettingsService.Instance.Settings.TaskbarWidgetVisible;
    }

    private void RefreshFrequencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (RefreshFrequencyCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            SettingsService.Instance.Settings.RefreshIntervalMinutes = minutes;
            SettingsService.Instance.Save();
        }
    }

    private void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var enabled = StartWithWindowsToggle.IsOn;
        SetStartupEnabled(enabled);
        SettingsService.Instance.Settings.StartWithWindows = enabled;
        SettingsService.Instance.Save();
    }

    private void TaskbarWidgetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        var enabled = TaskbarWidgetToggle.IsOn;

        if (Application.Current is App app)
        {
            if (enabled)
            {
                app.ShowTaskbarWidget();
            }
            else
            {
                app.HideTaskbarWidget();
            }
        }
    }

    private static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                // Get the executable path
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }
}
