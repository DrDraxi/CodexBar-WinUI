using CodexBar.Core.Models;
using CodexBar.Interop;
using CodexBar.Services;

namespace CodexBar.Widget;

/// <summary>
/// Manages the lifecycle of the taskbar widget
/// </summary>
public sealed class TaskbarManager : IDisposable
{
    private TaskbarWidget? _widget;
    private TaskbarStructureWatcher? _watcher;
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

        if (_widget != null && _widget.IsVisible)
        {
            // Already showing
            return;
        }

        if (_widget == null)
        {
            // Create and initialize widget
            _widget = new TaskbarWidget();
            if (!_widget.Initialize())
            {
                LogService.Log("TaskbarManager", "Failed to initialize widget");
                _widget.Dispose();
                _widget = null;
                return;
            }

            // Wire up click event
            _widget.Clicked += (s, e) => WidgetClicked?.Invoke(this, EventArgs.Empty);

            // Start watching for taskbar changes
            _watcher = new TaskbarStructureWatcher();
            _watcher.TaskbarChanged += OnTaskbarChanged;
            _watcher.Start();

            // Explicitly show after initialization
            _widget.Show();
        }
        else
        {
            _widget.Show();
        }

        // Save setting
        SettingsService.Instance.Settings.TaskbarWidgetVisible = true;
        SettingsService.Instance.Save();

        LogService.Log("TaskbarManager", "Widget shown");
    }

    /// <summary>
    /// Hide the taskbar widget
    /// </summary>
    public void HideWidget()
    {
        if (_widget != null)
        {
            _widget.Hide();
        }

        // Save setting
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
        {
            HideWidget();
        }
        else
        {
            ShowWidget();
        }
    }

    /// <summary>
    /// Update the widget with new usage data
    /// </summary>
    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        _widget?.UpdateUsage(snapshots);
    }

    private void OnTaskbarChanged(object? sender, TaskbarChangedEventArgs e)
    {
        if (_widget == null) return;

        switch (e.Reason)
        {
            case TaskbarChangeReason.ExplorerRestart:
                // Re-inject widget after explorer restart
                LogService.Log("TaskbarManager", "Handling explorer restart");
                _widget.Reinject();
                break;

            case TaskbarChangeReason.BoundsChanged:
            case TaskbarChangeReason.DpiChanged:
            case TaskbarChangeReason.TrayIconsChanged:
                // Reposition widget
                LogService.Log("TaskbarManager", $"Repositioning widget due to {e.Reason}");
                _widget.UpdatePosition();
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_watcher != null)
        {
            _watcher.TaskbarChanged -= OnTaskbarChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _widget?.Dispose();
        _widget = null;

        LogService.Log("TaskbarManager", "Disposed");
    }
}
