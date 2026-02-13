using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Services;
using TaskbarWidget;
using TaskbarWidget.Rendering;

namespace CodexBar.Widget;

/// <summary>
/// Taskbar widget that displays provider usage as vertical bars using the Widget API.
/// </summary>
internal sealed class CodexBarWidget : IDisposable
{
    private const int BarWidthDip = 4;
    private const int BarSpacingDip = 2;
    private const int ProviderSpacingDip = 6;
    private const int BarHeightDip = 26;
    private const int ProviderPaddingDip = 7;

    private static readonly Color GrayBg = Color.FromArgb(80, 128, 128, 128);
    private static readonly Color OrangeColor = Color.FromRgb(255, 165, 0);

    private TaskbarWidget.Widget? _widget;
    private Dictionary<UsageProvider, UsageSnapshot> _snapshots = new();
    private bool _disposed;

    public event EventHandler? Clicked;

    public bool IsVisible => _widget != null;

    public void Show()
    {
        if (_disposed || _widget != null) return;

        _widget = new TaskbarWidget.Widget("CodexBar", render: Render);
        _widget.Show();
    }

    public void Hide()
    {
        _widget?.Dispose();
        _widget = null;
    }

    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        _snapshots = snapshots;
        _widget?.Invalidate();
    }

    private void Render(RenderContext ctx)
    {
        var snapshots = _snapshots;
        if (snapshots.Count == 0) return;

        ctx.Horizontal(ProviderSpacingDip, h =>
        {
            foreach (var (provider, snapshot) in SettingsService.Instance.Settings.GetOrderedProviders(snapshots))
            {
                if (!snapshot.IsValid) continue;

                var info = ProviderRegistry.GetProviderInfo(provider);
                var providerColor = ParseColor(info.Color);
                var vis = SettingsService.Instance.Settings.GetWidgetVisibility(provider);

                var bars = GetVisibleBars(snapshot, vis, providerColor);
                if (bars.Count == 0) continue;

                int canvasWidth = ProviderPaddingDip * 2 +
                                 bars.Count * BarWidthDip +
                                 (bars.Count - 1) * BarSpacingDip;

                h.Panel(panel =>
                {
                    // Transparent HoverBackground signals per-panel hover targeting
                    panel.HoverBackground(Color.Transparent);
                    panel.OnClick(() => Clicked?.Invoke(this, EventArgs.Empty));
                    panel.Tooltip(BuildTooltip(info, snapshot, vis));

                    panel.Canvas(canvasWidth, BarHeightDip, canvas =>
                    {
                        int x = ProviderPaddingDip;
                        foreach (var (percent, color) in bars)
                        {
                            canvas.DrawFilledRoundedRect(x, 0, BarWidthDip, BarHeightDip, 2, GrayBg);

                            int fillH = (int)(BarHeightDip * Math.Min(percent, 100) / 100);
                            if (fillH > 0)
                                canvas.DrawFilledRoundedRect(x, BarHeightDip - fillH, BarWidthDip, fillH, 2, color);

                            x += BarWidthDip + BarSpacingDip;
                        }
                    });
                });
            }
        });
    }

    private static List<(double Percent, Color Color)> GetVisibleBars(
        UsageSnapshot snapshot, BarVisibilitySettings vis, Color providerColor)
    {
        var bars = new List<(double, Color)>();

        if (snapshot.Primary != null && vis.Primary)
            bars.Add((snapshot.Primary.UsedPercent, providerColor));
        if (snapshot.Secondary != null && vis.Secondary)
            bars.Add((snapshot.Secondary.UsedPercent, providerColor));
        if (snapshot.Tertiary != null && vis.Tertiary)
            bars.Add((snapshot.Tertiary.UsedPercent, providerColor));
        if (snapshot.Quaternary != null && vis.Quaternary)
            bars.Add((snapshot.Quaternary.UsedPercent, providerColor));

        if (snapshot.ProviderCost != null && vis.Cost)
        {
            var cost = snapshot.ProviderCost;
            double percent = cost.CurrencyCode == "Credits" ? 100 :
                cost.Limit > 0 ? (cost.Used / cost.Limit) * 100 : 0;
            bars.Add((percent, OrangeColor));
        }

        return bars;
    }

    private static string BuildTooltip(ProviderInfo info, UsageSnapshot snapshot, BarVisibilitySettings vis)
    {
        var lines = new List<string> { info.Name };

        void AddBar(RateWindow? window, bool visible, string defaultLabel)
        {
            if (window == null || !visible) return;
            var resetText = window.ResetsAt != null ? $" (resets {window.ResetTimeDisplay})" : "";
            lines.Add($"{window.Label ?? defaultLabel}: {window.UsedPercent:F0}%{resetText}");
        }

        AddBar(snapshot.Primary, vis.Primary, "Usage");
        AddBar(snapshot.Secondary, vis.Secondary, "Weekly");
        AddBar(snapshot.Tertiary, vis.Tertiary, "Tertiary");
        AddBar(snapshot.Quaternary, vis.Quaternary, "Quaternary");

        if (snapshot.ProviderCost != null && vis.Cost)
        {
            var cost = snapshot.ProviderCost;
            string costText = cost.CurrencyCode switch
            {
                "Credits" => cost.Limit == 0 ? "Unlimited" : $"{cost.Limit:F0} remaining",
                "USD" => $"${cost.Used:F2} / ${cost.Limit:F0}",
                _ => $"{cost.Used:F0} / {cost.Limit:F0}"
            };
            lines.Add($"{cost.Period ?? "Cost"}: {costText}");
        }

        return string.Join("\n", lines);
    }

    private static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return Color.FromRgb(r, g, b);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _widget?.Dispose();
        _widget = null;
    }
}
