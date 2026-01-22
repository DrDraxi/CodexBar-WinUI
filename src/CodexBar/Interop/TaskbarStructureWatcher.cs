using Microsoft.UI.Dispatching;
using CodexBar.Services;

namespace CodexBar.Interop;

/// <summary>
/// Monitors taskbar changes (resize, DPI, explorer restart)
/// </summary>
internal sealed class TaskbarStructureWatcher : IDisposable
{
    private readonly DispatcherQueueTimer _pollTimer;
    private Native.RECT _lastTaskbarRect;
    private IntPtr _lastTaskbarHwnd;
    private bool _disposed;

    /// <summary>
    /// Fires when taskbar bounds change or explorer restarts
    /// </summary>
    public event EventHandler<TaskbarChangedEventArgs>? TaskbarChanged;

    public TaskbarStructureWatcher()
    {
        _pollTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _pollTimer.Interval = TimeSpan.FromMilliseconds(500);
        _pollTimer.Tick += OnPollTick;
    }

    /// <summary>
    /// Start monitoring taskbar changes
    /// </summary>
    public void Start()
    {
        if (_disposed) return;

        // Capture initial state
        var taskbar = Native.FindTaskbar();
        if (taskbar != IntPtr.Zero)
        {
            _lastTaskbarHwnd = taskbar;
            Native.GetWindowRect(taskbar, out _lastTaskbarRect);
        }

        _pollTimer.Start();
        LogService.Log("TaskbarWatcher", "Started monitoring");
    }

    /// <summary>
    /// Stop monitoring taskbar changes
    /// </summary>
    public void Stop()
    {
        _pollTimer.Stop();
        LogService.Log("TaskbarWatcher", "Stopped monitoring");
    }

    private void OnPollTick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed) return;

        var taskbar = Native.FindTaskbar();

        // Check for explorer restart (taskbar handle changed)
        if (taskbar != _lastTaskbarHwnd)
        {
            LogService.Log("TaskbarWatcher", "Taskbar handle changed (explorer restart?)");
            _lastTaskbarHwnd = taskbar;

            if (taskbar != IntPtr.Zero)
            {
                Native.GetWindowRect(taskbar, out _lastTaskbarRect);
                TaskbarChanged?.Invoke(this, new TaskbarChangedEventArgs(TaskbarChangeReason.ExplorerRestart, _lastTaskbarRect));
            }
            return;
        }

        if (taskbar == IntPtr.Zero) return;

        // Check for bounds change
        Native.GetWindowRect(taskbar, out var currentRect);

        if (!RectsEqual(currentRect, _lastTaskbarRect))
        {
            LogService.Log("TaskbarWatcher", $"Taskbar bounds changed: {_lastTaskbarRect.Width}x{_lastTaskbarRect.Height} -> {currentRect.Width}x{currentRect.Height}");
            _lastTaskbarRect = currentRect;
            TaskbarChanged?.Invoke(this, new TaskbarChangedEventArgs(TaskbarChangeReason.BoundsChanged, currentRect));
        }
    }

    private static bool RectsEqual(Native.RECT a, Native.RECT b)
    {
        return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Stop();
        TaskbarChanged = null;
    }
}

/// <summary>
/// Event args for taskbar change events
/// </summary>
internal class TaskbarChangedEventArgs : EventArgs
{
    public TaskbarChangeReason Reason { get; }
    public Native.RECT NewBounds { get; }

    public TaskbarChangedEventArgs(TaskbarChangeReason reason, Native.RECT newBounds)
    {
        Reason = reason;
        NewBounds = newBounds;
    }
}

/// <summary>
/// Reason for taskbar change
/// </summary>
internal enum TaskbarChangeReason
{
    BoundsChanged,
    ExplorerRestart,
    DpiChanged
}
