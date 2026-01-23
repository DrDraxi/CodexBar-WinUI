using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Services;

using Color = Windows.UI.Color;

namespace CodexBar.Widget;

/// <summary>
/// Compact XAML content for the taskbar widget showing provider usage bars
/// </summary>
public sealed partial class TaskbarWidgetContent : UserControl
{
    private const int BarWidth = 4;
    private const int BarSpacing = 2;
    private const int ProviderSpacing = 6;

    // Cache of provider panels
    private readonly Dictionary<UsageProvider, ProviderPanel> _providerPanels = new();

    /// <summary>
    /// Fired when the widget is clicked
    /// </summary>
    public event EventHandler? Clicked;

    public TaskbarWidgetContent()
    {
        this.InitializeComponent();

        // Handle click on the entire widget
        this.PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Update the widget with latest usage data
    /// </summary>
    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        // Remove panels for providers no longer in snapshots
        var toRemove = _providerPanels.Keys.Except(snapshots.Keys).ToList();
        foreach (var provider in toRemove)
        {
            if (_providerPanels.TryGetValue(provider, out var panel))
            {
                BarsPanel.Children.Remove(panel.OuterContainer);
                _providerPanels.Remove(provider);
            }
        }

        // Update or create panels for each provider
        foreach (var (provider, snapshot) in snapshots.OrderBy(kv => kv.Key.ToString()))
        {
            if (!snapshot.IsValid) continue;

            if (_providerPanels.TryGetValue(provider, out var existing))
            {
                // Update existing panel
                UpdateProviderPanel(existing, snapshot);
            }
            else
            {
                // Create new panel
                var panel = CreateProviderPanel(provider, snapshot);
                _providerPanels[provider] = panel;

                // Insert in sorted order
                var insertIndex = 0;
                foreach (var child in BarsPanel.Children)
                {
                    if (child is Border border && border.Tag is UsageProvider existingProvider)
                    {
                        if (string.Compare(provider.ToString(), existingProvider.ToString()) < 0)
                            break;
                    }
                    insertIndex++;
                }
                BarsPanel.Children.Insert(insertIndex, panel.OuterContainer);
            }
        }
    }

    private ProviderPanel CreateProviderPanel(UsageProvider provider, UsageSnapshot snapshot)
    {
        var info = ProviderRegistry.GetProviderInfo(provider);
        var color = ParseColor(info.Color);

        // Inner container for bars
        var barsContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = BarSpacing,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(4, 4, 4, 4)
        };

        // Outer container with hover background
        var outerContainer = new Border
        {
            Tag = provider,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Colors.Transparent),
            CornerRadius = new CornerRadius(4),
            Child = barsContainer
        };

        // Add hover effects
        outerContainer.PointerEntered += (s, e) =>
        {
            outerContainer.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        };
        outerContainer.PointerExited += (s, e) =>
        {
            outerContainer.Background = new SolidColorBrush(Colors.Transparent);
        };

        var panel = new ProviderPanel
        {
            OuterContainer = outerContainer,
            BarsContainer = barsContainer,
            Provider = provider,
            Color = color,
            Bars = new List<(Rectangle Back, Rectangle Fill, string Label)>()
        };

        // Create bars for primary, secondary, and cost
        if (snapshot.Primary != null)
        {
            var (back, fill) = CreateBar(color);
            barsContainer.Children.Add(CreateBarContainer(back, fill));
            panel.Bars.Add((back, fill, snapshot.Primary.Label ?? "Usage"));
        }

        if (snapshot.Secondary != null)
        {
            var (back, fill) = CreateBar(color);
            barsContainer.Children.Add(CreateBarContainer(back, fill));
            panel.Bars.Add((back, fill, snapshot.Secondary.Label ?? "Weekly"));
        }

        if (snapshot.ProviderCost != null)
        {
            var costColor = new SolidColorBrush(Colors.Orange);
            var (back, fill) = CreateBar(costColor);
            barsContainer.Children.Add(CreateBarContainer(back, fill));
            panel.Bars.Add((back, fill, snapshot.ProviderCost.Period ?? "Cost"));
        }

        UpdateProviderPanel(panel, snapshot);

        return panel;
    }

    private (Rectangle Back, Rectangle Fill) CreateBar(SolidColorBrush color)
    {
        var back = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            RadiusX = 2,
            RadiusY = 2,
            Width = BarWidth,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var fill = new Rectangle
        {
            Fill = color,
            RadiusX = 2,
            RadiusY = 2,
            Width = BarWidth,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        return (back, fill);
    }

    private Grid CreateBarContainer(Rectangle back, Rectangle fill)
    {
        var grid = new Grid
        {
            Width = BarWidth,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Children.Add(back);
        grid.Children.Add(fill);

        // Handle size changes to update fill height
        grid.SizeChanged += (s, e) =>
        {
            if (fill.Tag is double percent && e.NewSize.Height > 0)
            {
                fill.Height = (e.NewSize.Height * percent) / 100.0;
            }
        };

        return grid;
    }

    private void UpdateProviderPanel(ProviderPanel panel, UsageSnapshot snapshot)
    {
        var info = ProviderRegistry.GetProviderInfo(panel.Provider);
        var tooltipLines = new List<string> { info.Name };

        int barIndex = 0;

        // Update primary bar
        if (snapshot.Primary != null && barIndex < panel.Bars.Count)
        {
            var (back, fill, label) = panel.Bars[barIndex];
            var percent = snapshot.Primary.UsedPercent;
            UpdateBarFill(fill, percent);

            var resetText = snapshot.Primary.ResetsAt != null ? $" (resets {snapshot.Primary.ResetTimeDisplay})" : "";
            tooltipLines.Add($"{label}: {percent:F0}%{resetText}");
            barIndex++;
        }

        // Update secondary bar
        if (snapshot.Secondary != null && barIndex < panel.Bars.Count)
        {
            var (back, fill, label) = panel.Bars[barIndex];
            var percent = snapshot.Secondary.UsedPercent;
            UpdateBarFill(fill, percent);

            var resetText = snapshot.Secondary.ResetsAt != null ? $" (resets {snapshot.Secondary.ResetTimeDisplay})" : "";
            tooltipLines.Add($"{label}: {percent:F0}%{resetText}");
            barIndex++;
        }

        // Update cost bar
        if (snapshot.ProviderCost != null && barIndex < panel.Bars.Count)
        {
            var (back, fill, label) = panel.Bars[barIndex];
            var cost = snapshot.ProviderCost;
            double percent;
            string costText;

            if (cost.CurrencyCode == "Credits")
            {
                percent = 100;
                costText = cost.Limit == 0 ? "Unlimited" : $"{cost.Limit:F0} remaining";
            }
            else if (cost.CurrencyCode == "USD")
            {
                percent = cost.Limit > 0 ? (cost.Used / cost.Limit) * 100 : 0;
                costText = $"${cost.Used:F2} / ${cost.Limit:F0}";
            }
            else
            {
                percent = cost.Limit > 0 ? (cost.Used / cost.Limit) * 100 : 0;
                costText = $"{cost.Used:F0} / {cost.Limit:F0}";
            }

            UpdateBarFill(fill, percent);
            tooltipLines.Add($"{label}: {costText}");
        }

        // Set tooltip on the container
        var tooltip = string.Join("\n", tooltipLines);
        ToolTipService.SetToolTip(panel.OuterContainer, tooltip);
    }

    private void UpdateBarFill(Rectangle fill, double percent)
    {
        fill.Tag = percent;

        // Update height if parent is available
        if (fill.Parent is Grid container && container.ActualHeight > 0)
        {
            fill.Height = (container.ActualHeight * percent) / 100.0;
        }
    }

    private static SolidColorBrush ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    /// <summary>
    /// Clear all panels
    /// </summary>
    public void Clear()
    {
        BarsPanel.Children.Clear();
        _providerPanels.Clear();
    }

    /// <summary>
    /// Get the desired width based on content
    /// </summary>
    public double GetDesiredWidth()
    {
        int totalBars = _providerPanels.Values.Sum(p => p.Bars.Count);
        int providerCount = _providerPanels.Count;
        return (totalBars * BarWidth) +
               ((totalBars - providerCount) * BarSpacing) +
               ((providerCount - 1) * ProviderSpacing) + 16;
    }

    private class ProviderPanel
    {
        public Border OuterContainer { get; set; } = null!;
        public StackPanel BarsContainer { get; set; } = null!;
        public UsageProvider Provider { get; set; }
        public SolidColorBrush Color { get; set; } = null!;
        public List<(Rectangle Back, Rectangle Fill, string Label)> Bars { get; set; } = null!;
    }
}
