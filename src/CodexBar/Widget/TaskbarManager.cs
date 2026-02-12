using CodexBar.Core.Models;
using CodexBar.Services;

namespace CodexBar.Widget;

/// <summary>
/// Manages the lifecycle of the taskbar widget
/// </summary>
public sealed class TaskbarManager : IDisposable
{
    private CodexBarWidget? _widget;
    private bool _disposed;

    /// <summary>
    /// Fired when the widget is clicked
    /// </summary>
    public event EventHandler? WidgetClicked;

    /// <summary>
    /// Whether the widget is currently visible
    /// </summary>
    public bool IsWidgetVisible => _widget?.IsVisible ?? false;

    /// <summary>
    /// Show the taskbar widget
    /// </summary>
    public void ShowWidget()
    {
        if (_disposed) return;
        if (_widget != null && _widget.IsVisible) return;

        if (_widget == null)
        {
            _widget = new CodexBarWidget();
            _widget.Clicked += (s, e) => WidgetClicked?.Invoke(this, EventArgs.Empty);
        }

        _widget.Show();

        SettingsService.Instance.Settings.TaskbarWidgetVisible = true;
        SettingsService.Instance.Save();

        LogService.Log("TaskbarManager", "Widget shown");
    }

    /// <summary>
    /// Hide the taskbar widget
    /// </summary>
    public void HideWidget()
    {
        _widget?.Hide();

        SettingsService.Instance.Settings.TaskbarWidgetVisible = false;
        SettingsService.Instance.Save();

        LogService.Log("TaskbarManager", "Widget hidden");
    }

    /// <summary>
    /// Toggle widget visibility
    /// </summary>
    public void ToggleWidget()
    {
        if (IsWidgetVisible)
            HideWidget();
        else
            ShowWidget();
    }

    /// <summary>
    /// Update the widget with new usage data
    /// </summary>
    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        _widget?.UpdateUsage(snapshots);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _widget?.Dispose();
        _widget = null;

        LogService.Log("TaskbarManager", "Disposed");
    }
}
