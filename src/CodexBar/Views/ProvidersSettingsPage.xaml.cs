using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CodexBar.Core.Models;
using CodexBar.Services;

namespace CodexBar.Views;

public sealed partial class ProvidersSettingsPage : Page
{
    private bool _isLoading = true;

    public ProvidersSettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        var settings = SettingsService.Instance.Settings;
        var enabled = settings.EnabledProviders;

        CodexToggle.IsOn = enabled.Contains(UsageProvider.Codex);
        ClaudeToggle.IsOn = enabled.Contains(UsageProvider.Claude);
        CopilotToggle.IsOn = enabled.Contains(UsageProvider.Copilot);
        CursorToggle.IsOn = enabled.Contains(UsageProvider.Cursor);
        GeminiToggle.IsOn = enabled.Contains(UsageProvider.Gemini);
        JetBrainsToggle.IsOn = enabled.Contains(UsageProvider.JetBrains);
        AugmentToggle.IsOn = enabled.Contains(UsageProvider.Augment);

        // Load manual cookies (masked)
        LoadCookieBox(CodexCookieBox, "Codex");
        LoadCookieBox(ClaudeCookieBox, "Claude");
        LoadCookieBox(CursorCookieBox, "Cursor");
        LoadCookieBox(AugmentCookieBox, "Augment");
    }

    private void LoadCookieBox(PasswordBox box, string provider)
    {
        if (SettingsService.Instance.Settings.ManualCookies.TryGetValue(provider, out var cookie))
        {
            if (!string.IsNullOrEmpty(cookie))
            {
                // Show placeholder to indicate a cookie is saved
                box.PlaceholderText = "Cookie saved (paste new to replace)";
            }
        }
    }

    private void ProviderToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender is ToggleSwitch toggle && toggle.Tag is string providerName)
        {
            if (Enum.TryParse<UsageProvider>(providerName, out var provider))
            {
                var enabled = SettingsService.Instance.Settings.EnabledProviders;

                if (toggle.IsOn)
                {
                    enabled.Add(provider);
                }
                else
                {
                    enabled.Remove(provider);
                }

                SettingsService.Instance.Save();
            }
        }
    }

    private void CookieBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender is PasswordBox box && box.Tag is string providerName)
        {
            var cookie = box.Password;
            if (!string.IsNullOrEmpty(cookie))
            {
                // Normalize the cookie (strip "Cookie:" prefix if present)
                cookie = NormalizeCookie(cookie);
                SettingsService.Instance.Settings.ManualCookies[providerName] = cookie;
                SettingsService.Instance.Save();
            }
        }
    }

    private string NormalizeCookie(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        raw = raw.Trim();

        // Remove common prefixes
        if (raw.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw.Substring(7).Trim();
        }
        else if (raw.StartsWith("-H 'Cookie:", StringComparison.OrdinalIgnoreCase) ||
                 raw.StartsWith("-H \"Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            // Extract from curl-style header
            var start = raw.IndexOf(':') + 1;
            raw = raw.Substring(start).Trim().Trim('\'', '"');
        }

        return raw;
    }

    private async void LoginOAuth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string providerName)
        {
            switch (providerName)
            {
                case "Codex":
                    await LaunchCodexLoginAsync();
                    break;
                case "Claude":
                    await LaunchClaudeLoginAsync();
                    break;
                case "Copilot":
                    await LaunchCopilotLoginAsync();
                    break;
            }
        }
    }

    private async Task LaunchCodexLoginAsync()
    {
        // Try to find codex binary
        var codexPath = FindExecutable("codex");
        if (codexPath == null)
        {
            await ShowMessageAsync("Codex CLI not found", "Please install the Codex CLI first: npm install -g @anthropic/codex");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = codexPath,
                Arguments = "login",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to launch Codex login: {ex.Message}");
        }
    }

    private async Task LaunchClaudeLoginAsync()
    {
        // Try to find claude binary
        var claudePath = FindExecutable("claude");
        if (claudePath == null)
        {
            await ShowMessageAsync("Claude CLI not found", "Please install the Claude CLI first: npm install -g @anthropic/claude-cli");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = claudePath,
                Arguments = "login",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to launch Claude login: {ex.Message}");
        }
    }

    private async Task LaunchCopilotLoginAsync()
    {
        // Open GitHub device flow in browser
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "https://github.com/login/device",
                UseShellExecute = true
            };
            Process.Start(startInfo);
            await ShowMessageAsync("GitHub Login", "Complete the device flow in your browser, then the app will detect your credentials.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Failed to open browser: {ex.Message}");
        }
    }

    private string? FindExecutable(string name)
    {
        // Check common locations
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "npm", $"{name}.cmd"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", $"{name}.cmd"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, name);
            if (File.Exists(fullPath)) return fullPath;
            if (File.Exists(fullPath + ".cmd")) return fullPath + ".cmd";
            if (File.Exists(fullPath + ".exe")) return fullPath + ".exe";
        }

        return null;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
