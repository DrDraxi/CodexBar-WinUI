using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CodexBar.Core.Models;
using CodexBar.Services;

namespace CodexBar.Views;

public sealed partial class ProvidersSettingsPage : Page
{
    private bool _isLoading = true;
    private readonly ObservableCollection<object> _providerItems = new();

    // Per-provider bar labels: (slot key, display name)
    // slot key must match BarVisibilitySettings property names
    private static readonly Dictionary<UsageProvider, (string Slot, string Label)[]> ProviderBarLabels = new()
    {
        [UsageProvider.Codex] = [("Primary", "Session"), ("Secondary", "Weekly"), ("Cost", "Cost")],
        [UsageProvider.Claude] = [("Primary", "5h"), ("Secondary", "7d"), ("Tertiary", "Sonnet 7d"), ("Quaternary", "Opus 7d"), ("Cost", "Extra Usage")],
        [UsageProvider.Copilot] = [("Primary", "Usage"), ("Cost", "Cost")],
        [UsageProvider.Cursor] = [("Primary", "Auto"), ("Secondary", "Named"), ("Cost", "On-Demand")],
        [UsageProvider.Gemini] = [("Primary", "Usage"), ("Secondary", "Weekly")],
        [UsageProvider.JetBrains] = [("Primary", "Usage")],
        [UsageProvider.Augment] = [("Primary", "Usage"), ("Cost", "Credits")],
        [UsageProvider.Kiro] = [("Primary", "Credits"), ("Secondary", "Bonus")],
        [UsageProvider.Amp] = [("Primary", "Daily")],
        [UsageProvider.Factory] = [("Primary", "Standard"), ("Secondary", "Premium")],
        [UsageProvider.Zai] = [("Primary", "Tokens"), ("Secondary", "MCP")],
        [UsageProvider.Kimi] = [("Primary", "Weekly"), ("Secondary", "5h Rate")],
        [UsageProvider.KimiK2] = [("Primary", "Credits")],
        [UsageProvider.MiniMax] = [("Primary", "Plan")],
        [UsageProvider.VertexAI] = [("Primary", "Quota")],
        [UsageProvider.OpenCode] = [("Primary", "5h"), ("Secondary", "Weekly")],
        [UsageProvider.Antigravity] = [("Primary", "Monthly")],
    };

    public ProvidersSettingsPage()
    {
        this.InitializeComponent();

        // Move XAML items into ObservableCollection so CanReorderItems works properly
        var xamlItems = ProvidersListView.Items.Cast<object>().ToList();
        ProvidersListView.Items.Clear();
        foreach (var item in xamlItems)
            _providerItems.Add(item);
        ProvidersListView.ItemsSource = _providerItems;

        ApplySavedOrder();
        WrapExpandersWithGrips();
        LoadSettings();
        InjectBarVisibilitySections();
        _isLoading = false;

        // Listen for any collection changes (drag reorder or button moves) to persist order
        _providerItems.CollectionChanged += (_, _) =>
        {
            if (!_isLoading) SaveCurrentOrder();
        };
    }

    private void ApplySavedOrder()
    {
        var order = SettingsService.Instance.Settings.ProviderOrder;
        if (order == null || order.Count == 0) return;

        var orderLookup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < order.Count; i++)
            orderLookup[order[i]] = i;

        var sorted = _providerItems
            .Select(item =>
            {
                var name = GetProviderNameFromItem(item);
                int key = name != null && orderLookup.TryGetValue(name, out var idx) ? idx : int.MaxValue;
                return (item, key);
            })
            .OrderBy(x => x.key)
            .Select(x => x.item)
            .ToList();

        _providerItems.Clear();
        foreach (var item in sorted)
            _providerItems.Add(item);
    }

    private void WrapExpandersWithGrips()
    {
        var expanders = _providerItems.OfType<Expander>().ToList();
        _providerItems.Clear();

        foreach (var expander in expanders)
        {
            var wrapper = new Grid();
            wrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Grip area outside the Expander — pointer events here propagate
            // to the ListViewItem, enabling drag-reorder on collapsed items.
            var grip = new FontIcon
            {
                Glyph = "\uE76F",
                FontSize = 14,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 8, 0)
            };
            Grid.SetColumn(grip, 0);
            Grid.SetColumn(expander, 1);

            wrapper.Children.Add(grip);
            wrapper.Children.Add(expander);

            _providerItems.Add(wrapper);
        }
    }

    private void SaveCurrentOrder()
    {
        var order = new List<string>();
        foreach (var item in _providerItems)
        {
            var name = GetProviderNameFromItem(item);
            if (name != null)
                order.Add(name);
        }

        SettingsService.Instance.Settings.ProviderOrder = order;
        SettingsService.Instance.Save();
    }

    private static Expander? GetExpanderFromItem(object? item)
    {
        if (item is Expander exp) return exp;
        if (item is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Expander e) return e;
            }
        }
        return null;
    }

    private static string? GetProviderNameFromItem(object? item)
    {
        var expander = GetExpanderFromItem(item);
        if (expander?.Header is not Grid headerGrid) return null;
        foreach (var child in headerGrid.Children)
        {
            if (child is ToggleSwitch toggle && toggle.Tag is string tag)
                return tag;
        }
        return null;
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
        KiroToggle.IsOn = enabled.Contains(UsageProvider.Kiro);
        AmpToggle.IsOn = enabled.Contains(UsageProvider.Amp);
        FactoryToggle.IsOn = enabled.Contains(UsageProvider.Factory);
        ZaiToggle.IsOn = enabled.Contains(UsageProvider.Zai);
        KimiToggle.IsOn = enabled.Contains(UsageProvider.Kimi);
        KimiK2Toggle.IsOn = enabled.Contains(UsageProvider.KimiK2);
        MiniMaxToggle.IsOn = enabled.Contains(UsageProvider.MiniMax);
        VertexAIToggle.IsOn = enabled.Contains(UsageProvider.VertexAI);
        OpenCodeToggle.IsOn = enabled.Contains(UsageProvider.OpenCode);
        AntigravityToggle.IsOn = enabled.Contains(UsageProvider.Antigravity);

        // Load manual cookies/tokens (masked)
        LoadCookieBox(CodexCookieBox, "Codex");
        LoadCookieBox(ClaudeCookieBox, "Claude");
        LoadCookieBox(CursorCookieBox, "Cursor");
        LoadCookieBox(AugmentCookieBox, "Augment");
        LoadCookieBox(AmpCookieBox, "Amp");
        LoadCookieBox(FactoryCookieBox, "Factory");
        LoadCookieBox(ZaiCookieBox, "Zai");
        LoadCookieBox(KimiCookieBox, "Kimi");
        LoadCookieBox(KimiK2CookieBox, "KimiK2");
        LoadCookieBox(MiniMaxCookieBox, "MiniMax");
        LoadCookieBox(OpenCodeCookieBox, "OpenCode");
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

    private void InjectBarVisibilitySections()
    {
        foreach (var child in _providerItems)
        {
            var expander = GetExpanderFromItem(child);
            if (expander == null) continue;

            var providerName = GetProviderNameFromItem(child);
            if (providerName == null || !Enum.TryParse<UsageProvider>(providerName, out var provider))
                continue;

            // Get the content StackPanel
            if (expander.Content is not StackPanel contentPanel)
                continue;

            // Build bar visibility section
            var visSection = CreateBarVisibilitySection(provider);
            contentPanel.Children.Add(visSection);
        }
    }

    private StackPanel CreateBarVisibilitySection(UsageProvider provider)
    {
        var settings = SettingsService.Instance.Settings;
        var popupVis = settings.GetPopupVisibility(provider);
        var widgetVis = settings.GetWidgetVisibility(provider);
        var providerName = provider.ToString();

        var section = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };

        section.Children.Add(new TextBlock
        {
            Text = "Bar Visibility",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        // Header row
        var headerRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

        var colLabel = new TextBlock { Text = "Bar", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        var popupLabel = new TextBlock { Text = "Popup", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        var widgetLabel = new TextBlock { Text = "Widget", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        Grid.SetColumn(colLabel, 0);
        Grid.SetColumn(popupLabel, 1);
        Grid.SetColumn(widgetLabel, 2);
        headerRow.Children.Add(colLabel);
        headerRow.Children.Add(popupLabel);
        headerRow.Children.Add(widgetLabel);
        section.Children.Add(headerRow);

        // One row per bar slot for this provider
        var bars = ProviderBarLabels.GetValueOrDefault(provider,
            [("Primary", "Primary"), ("Secondary", "Secondary"), ("Cost", "Cost")]);

        foreach (var (slot, displayName) in bars)
        {
            bool popupChecked = slot switch
            {
                "Primary" => popupVis.Primary,
                "Secondary" => popupVis.Secondary,
                "Tertiary" => popupVis.Tertiary,
                "Quaternary" => popupVis.Quaternary,
                "Cost" => popupVis.Cost,
                _ => true
            };
            bool widgetChecked = slot switch
            {
                "Primary" => widgetVis.Primary,
                "Secondary" => widgetVis.Secondary,
                "Tertiary" => widgetVis.Tertiary,
                "Quaternary" => widgetVis.Quaternary,
                "Cost" => widgetVis.Cost,
                _ => true
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var label = new TextBlock { Text = displayName, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var popupCb = new CheckBox
            {
                IsChecked = popupChecked,
                Tag = $"{providerName}|Popup|{slot}",
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            popupCb.Checked += BarVisibility_Changed;
            popupCb.Unchecked += BarVisibility_Changed;
            Grid.SetColumn(popupCb, 1);
            row.Children.Add(popupCb);

            var widgetCb = new CheckBox
            {
                IsChecked = widgetChecked,
                Tag = $"{providerName}|Widget|{slot}",
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            widgetCb.Checked += BarVisibility_Changed;
            widgetCb.Unchecked += BarVisibility_Changed;
            Grid.SetColumn(widgetCb, 2);
            row.Children.Add(widgetCb);

            section.Children.Add(row);
        }

        return section;
    }

    private void BarVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is not CheckBox cb || cb.Tag is not string tag) return;

        var parts = tag.Split('|');
        if (parts.Length != 3) return;

        var providerName = parts[0];
        var target = parts[1]; // "Popup" or "Widget"
        var slot = parts[2];
        var isChecked = cb.IsChecked == true;

        var settings = SettingsService.Instance.Settings;
        var dict = target == "Popup" ? settings.PopupBarVisibility : settings.WidgetBarVisibility;

        if (!dict.TryGetValue(providerName, out var vis))
        {
            // Create with appropriate defaults
            vis = target == "Popup"
                ? new BarVisibilitySettings()
                : new BarVisibilitySettings { Tertiary = false, Quaternary = false };
            dict[providerName] = vis;
        }

        switch (slot)
        {
            case "Primary": vis.Primary = isChecked; break;
            case "Secondary": vis.Secondary = isChecked; break;
            case "Tertiary": vis.Tertiary = isChecked; break;
            case "Quaternary": vis.Quaternary = isChecked; break;
            case "Cost": vis.Cost = isChecked; break;
        }

        SettingsService.Instance.Save();
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
