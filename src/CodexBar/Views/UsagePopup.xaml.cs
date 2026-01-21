using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;

using Color = Windows.UI.Color;
using Colors = Microsoft.UI.Colors;

namespace CodexBar.Views;

public sealed partial class UsagePopup : Window
{
    // Cache of provider cards and their progress bars for smooth updates
    private readonly Dictionary<UsageProvider, (Border Card, List<ProgressBar> Bars)> _providerCards = new();

    public UsagePopup()
    {
        this.InitializeComponent();
        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Set compact size for popup
        appWindow.Resize(new Windows.Graphics.SizeInt32(340, 600));
        appWindow.Title = "CodexBar";

        // Make title bar transparent/blend with content
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        // Position near system tray (bottom-right)
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var x = workArea.X + workArea.Width - 360;
        var y = workArea.Y + workArea.Height - 620;
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    public void SetLoading()
    {
        if (ProviderCards.Children.Count == 0)
        {
            StatusText.Text = "Fetching...";
            var loadingText = new TextBlock
            {
                Text = "Loading usage data...",
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            ProviderCards.Children.Add(loadingText);
        }
        else
        {
            StatusText.Text = "Refreshing...";
        }
    }

    public void SetError(string error)
    {
        StatusText.Text = "Error";
        ProviderCards.Children.Clear();

        var errorText = new TextBlock
        {
            Text = error,
            Foreground = new SolidColorBrush(Colors.Red),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 20, 0, 0)
        };
        ProviderCards.Children.Add(errorText);
    }

    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        var validCount = snapshots.Values.Count(s => s.IsValid);
        StatusText.Text = $"{validCount} provider{(validCount != 1 ? "s" : "")} active";

        // Remove cards for providers no longer in snapshots
        var toRemove = _providerCards.Keys.Except(snapshots.Keys).ToList();
        foreach (var provider in toRemove)
        {
            if (_providerCards.TryGetValue(provider, out var cached))
            {
                ProviderCards.Children.Remove(cached.Card);
                _providerCards.Remove(provider);
            }
        }

        foreach (var (provider, snapshot) in snapshots.OrderBy(kv => kv.Key.ToString()))
        {
            if (_providerCards.TryGetValue(provider, out var cached))
            {
                // Update existing progress bars - they will animate smoothly
                UpdateProviderCard(cached.Card, cached.Bars, snapshot);
            }
            else
            {
                // Create new card
                var bars = new List<ProgressBar>();
                var card = CreateProviderCard(provider, snapshot, bars);
                _providerCards[provider] = (card, bars);
                ProviderCards.Children.Add(card);
            }
        }
    }

    private static void UpdateProviderCard(Border card, List<ProgressBar> bars, UsageSnapshot snapshot)
    {
        if (snapshot.Error != null)
        {
            card.Opacity = 0.6;
            foreach (var bar in bars) bar.Value = 0;
            return;
        }

        card.Opacity = 1.0;

        int barIndex = 0;
        if (snapshot.Primary != null && barIndex < bars.Count)
        {
            bars[barIndex++].Value = snapshot.Primary.UsedPercent;
        }
        if (snapshot.Secondary != null && barIndex < bars.Count)
        {
            bars[barIndex++].Value = snapshot.Secondary.UsedPercent;
        }
        if (snapshot.ProviderCost != null && barIndex < bars.Count)
        {
            var cost = snapshot.ProviderCost;
            var percent = cost.Limit > 0 ? (cost.Used / cost.Limit) * 100 : 0;
            if (cost.CurrencyCode == "Credits") percent = 100;
            bars[barIndex].Value = percent;
        }
    }

    private static Border CreateProviderCard(UsageProvider provider, UsageSnapshot snapshot, List<ProgressBar> bars)
    {
        var info = ProviderRegistry.GetProviderInfo(provider);
        var hasError = !string.IsNullOrEmpty(snapshot.Error);

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Opacity = hasError ? 0.6 : 1.0
        };

        var content = new StackPanel { Spacing = 6 };

        // Header row: provider name + status
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var colorBrush = ParseColor(info.Color);
        var badge = new Border
        {
            Background = colorBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        badge.Child = new TextBlock
        {
            Text = info.Name,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White)
        };
        header.Children.Add(badge);

        var status = new TextBlock
        {
            Text = hasError ? snapshot.Error : (snapshot.Identity?.Plan ?? "Connected"),
            FontSize = 12,
            Foreground = new SolidColorBrush(hasError ? Colors.OrangeRed : Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 150
        };
        header.Children.Add(status);
        content.Children.Add(header);

        if (!hasError && snapshot.Primary != null)
        {
            // Primary usage bar
            var (primaryRow, primaryBar) = CreateUsageRow(
                snapshot.Secondary != null ? "Session" : "Usage",
                snapshot.Primary.UsedPercent,
                colorBrush
            );
            content.Children.Add(primaryRow);
            bars.Add(primaryBar);

            // Secondary usage bar (if available)
            if (snapshot.Secondary != null)
            {
                var (secondaryRow, secondaryBar) = CreateUsageRow("Weekly", snapshot.Secondary.UsedPercent, colorBrush);
                content.Children.Add(secondaryRow);
                bars.Add(secondaryBar);
            }

            // On-demand / pay-per-use cost (if available)
            if (snapshot.ProviderCost != null)
            {
                var cost = snapshot.ProviderCost;
                var (costRow, costBar) = CreateCostRow(cost.Period ?? "On-Demand", cost.Used, cost.Limit, cost.CurrencyCode);
                content.Children.Add(costRow);
                bars.Add(costBar);
            }

            // Reset time
            if (snapshot.Primary.ResetsAt != null)
            {
                var resetText = new TextBlock
                {
                    Text = $"Resets in {snapshot.Primary.ResetTimeDisplay}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.Gray)
                };
                content.Children.Add(resetText);
            }
        }

        card.Child = content;
        return card;
    }

    private static (StackPanel Row, ProgressBar Bar) CreateUsageRow(string label, double percent, SolidColorBrush color)
    {
        var row = new StackPanel { Spacing = 4 };

        var labelRow = new Grid();
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Gray)
        };
        Grid.SetColumn(labelText, 0);
        labelRow.Children.Add(labelText);

        var percentText = new TextBlock
        {
            Text = $"{percent:F0}%",
            FontSize = 12
        };
        Grid.SetColumn(percentText, 1);
        labelRow.Children.Add(percentText);

        row.Children.Add(labelRow);

        var progressBar = new ProgressBar
        {
            Value = percent,
            Maximum = 100,
            Height = 4,
            Foreground = color
        };
        row.Children.Add(progressBar);

        return (row, progressBar);
    }

    private static (StackPanel Row, ProgressBar Bar) CreateCostRow(string label, double used, double limit, string currency)
    {
        var row = new StackPanel { Spacing = 4 };

        var labelRow = new Grid();
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Gray)
        };
        Grid.SetColumn(labelText, 0);
        labelRow.Children.Add(labelText);

        // Format based on currency type
        string displayText;
        double percent;
        SolidColorBrush barColor;

        if (currency == "Credits")
        {
            // For credits, show remaining balance (limit = balance remaining)
            displayText = limit == 0 ? "Unlimited" : $"{limit:F0} remaining";
            percent = 100; // Full bar for credits
            barColor = new SolidColorBrush(Colors.MediumPurple);
        }
        else if (currency == "USD")
        {
            displayText = $"${used:F2} / ${limit:F0}";
            percent = limit > 0 ? (used / limit) * 100 : 0;
            barColor = new SolidColorBrush(Colors.Orange);
        }
        else
        {
            displayText = $"{used:F0} / {limit:F0}";
            percent = limit > 0 ? (used / limit) * 100 : 0;
            barColor = new SolidColorBrush(Colors.Orange);
        }

        var costText = new TextBlock
        {
            Text = displayText,
            FontSize = 12,
            Foreground = barColor
        };
        Grid.SetColumn(costText, 1);
        labelRow.Children.Add(costText);

        row.Children.Add(labelRow);

        var progressBar = new ProgressBar
        {
            Value = percent,
            Maximum = 100,
            Height = 4,
            Foreground = barColor
        };
        row.Children.Add(progressBar);

        return (row, progressBar);
    }

    private static SolidColorBrush ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.RefreshUsage();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowSettings();
        }
    }

    // Legacy methods for compatibility
    public void SetClaudeLoading() => SetLoading();
    public void SetClaudeError(string error) => SetError(error);
    public void UpdateClaudeUsage(double sessionPercent, double weeklyPercent, string resetTime, string status)
    {
        var snapshot = new UsageSnapshot
        {
            Provider = UsageProvider.Claude,
            Primary = new RateWindow { UsedPercent = sessionPercent },
            Secondary = new RateWindow { UsedPercent = weeklyPercent },
            Identity = new ProviderIdentity { Plan = status }
        };
        UpdateUsage(new Dictionary<UsageProvider, UsageSnapshot> { [UsageProvider.Claude] = snapshot });
    }
}
