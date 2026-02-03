using Microsoft.UI.Content;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using CodexBar.Core.Models;
using CodexBar.Services;
using TaskbarWidget;

namespace CodexBar.Widget;

/// <summary>
/// Manages a widget window injected into the Windows taskbar using DesktopWindowXamlSource
/// </summary>
internal sealed class TaskbarWidget : IDisposable
{
    private const int DefaultWidgetWidth = 40;
    private const string WidgetClassName = "CodexBarWidget";

    /// <summary>
    /// Fired when the widget is clicked
    /// </summary>
    public event EventHandler? Clicked;

    private TaskbarInjectionHelper? _injectionHelper;
    private DesktopWindowXamlSource? _xamlSource;
    private TaskbarWidgetContent? _content;
    private bool _disposed;

    public bool IsVisible => _injectionHelper?.IsVisible ?? false;

    /// <summary>
    /// Initialize and show the widget in the taskbar
    /// </summary>
    public bool Initialize()
    {
        if (_disposed) return false;

        try
        {
            // Create and initialize the injection helper
            // Defer injection until after XAML content is set up
            _injectionHelper = new TaskbarInjectionHelper(
                new TaskbarInjectionConfig
                {
                    ClassName = WidgetClassName,
                    WindowTitle = "CodexBarWidgetHost",
                    WidthDip = DefaultWidgetWidth,
                    DeferInjection = true
                },
                log: msg => LogService.Log("TaskbarWidget", msg));

            var result = _injectionHelper.Initialize();
            if (!result.Success)
            {
                LogService.Log("TaskbarWidget", $"Injection failed: {result.Error}");
                _injectionHelper.Dispose();
                _injectionHelper = null;
                return false;
            }

            // Initialize XAML source
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(result.WindowHandle);
            _xamlSource = new DesktopWindowXamlSource();
            _xamlSource.Initialize(windowId);
            _xamlSource.SiteBridge.ResizePolicy = ContentSizePolicy.ResizeContentToParentWindow;

            // Create content
            _content = new TaskbarWidgetContent
            {
                Margin = new Thickness(4, 0, 4, 0),
                MaxHeight = result.Height - 8
            };

            // Wire up click event
            _content.Clicked += (s, e) => Clicked?.Invoke(this, EventArgs.Empty);

            // Wire up width change event for dynamic resizing
            _content.WidthChanged += (s, width) => _injectionHelper?.Resize(width);

            // Wrap in transparent grid
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Children = { _content }
            };

            _xamlSource.Content = rootGrid;

            // Now inject into taskbar (after XAML is set up)
            if (!_injectionHelper.Inject())
            {
                LogService.Log("TaskbarWidget", "Failed to inject into taskbar");
                _xamlSource.Dispose();
                _xamlSource = null;
                _injectionHelper.Dispose();
                _injectionHelper = null;
                return false;
            }

            LogService.Log("TaskbarWidget", "Widget initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("TaskbarWidget initialization failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Update widget position in the taskbar
    /// </summary>
    public void UpdatePosition()
    {
        _injectionHelper?.UpdatePosition();
    }

    /// <summary>
    /// Update usage display
    /// </summary>
    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        if (_content == null || !IsVisible) return;

        try
        {
            _content.UpdateUsage(snapshots);

            // Reposition on each update in case taskbar icons changed
            UpdatePosition();
        }
        catch (Exception ex)
        {
            LogService.LogError("TaskbarWidget UpdateUsage failed", ex);
        }
    }

    /// <summary>
    /// Show the widget
    /// </summary>
    public void Show()
    {
        _injectionHelper?.Show();
    }

    /// <summary>
    /// Hide the widget
    /// </summary>
    public void Hide()
    {
        _injectionHelper?.Hide();
    }

    /// <summary>
    /// Re-inject widget after explorer restart
    /// </summary>
    public void Reinject()
    {
        _injectionHelper?.Reinject();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_xamlSource != null)
        {
            try
            {
                _xamlSource.Dispose();
            }
            catch { }
            _xamlSource = null;
        }

        _injectionHelper?.Dispose();
        _injectionHelper = null;
        _content = null;

        LogService.Log("TaskbarWidget", "Widget disposed");
    }
}
