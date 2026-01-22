using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Content;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;
using CodexBar.Core.Models;
using CodexBar.Interop;
using CodexBar.Services;

using Color = Windows.UI.Color;

namespace CodexBar.Widget;

/// <summary>
/// Manages a widget window injected into the Windows taskbar using DesktopWindowXamlSource
/// </summary>
internal sealed class TaskbarWidget : IDisposable
{
    private const int DefaultWidgetWidth = 100;
    private const string WidgetClassName = "CodexBarWidget";

    /// <summary>
    /// Fired when the widget is clicked
    /// </summary>
    public event EventHandler? Clicked;

    private DesktopWindowXamlSource? _xamlSource;
    private AppWindow? _appWindow;
    private TaskbarWidgetContent? _content;
    private IntPtr _hwnd;
    private IntPtr _hwndShell;
    private IntPtr _hwndTrayNotify;
    private int _widgetWidth;
    private double _dpiScale = 1.0;
    private bool _isVisible;
    private bool _disposed;
    private bool _classRegistered;

    public bool IsVisible => _isVisible;

    /// <summary>
    /// Initialize and show the widget in the taskbar
    /// </summary>
    public bool Initialize()
    {
        if (_disposed) return false;

        try
        {
            // Find taskbar window handles
            _hwndShell = Native.FindTaskbar();
            if (_hwndShell == IntPtr.Zero)
            {
                LogService.Log("TaskbarWidget", "Failed to find taskbar");
                return false;
            }

            _hwndTrayNotify = Native.FindTrayNotifyWnd(_hwndShell);

            // Calculate DPI scale
            var dpi = Native.GetDpiForWindow(_hwndShell);
            _dpiScale = dpi / 96.0;
            _widgetWidth = (int)Math.Ceiling(_dpiScale * DefaultWidgetWidth);

            LogService.Log("TaskbarWidget", $"Shell: {_hwndShell:X}, TrayNotify: {_hwndTrayNotify:X}, DPI scale: {_dpiScale}");

            // Create the host window
            _hwnd = CreateHostWindow(_hwndShell);
            if (_hwnd == IntPtr.Zero)
            {
                LogService.Log("TaskbarWidget", "Failed to create host window");
                return false;
            }

            // Get AppWindow for the host
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.IsShownInSwitchers = false;

            // Get taskbar height for initial sizing
            Native.GetWindowRect(_hwndShell, out var taskbarRect);
            var height = taskbarRect.Height;

            // Resize the window
            _appWindow.ResizeClient(new SizeInt32(_widgetWidth, height));

            // Initialize XAML source
            _xamlSource = new DesktopWindowXamlSource();
            _xamlSource.Initialize(windowId);
            _xamlSource.SiteBridge.ResizePolicy = ContentSizePolicy.ResizeContentToParentWindow;

            // Create content
            _content = new TaskbarWidgetContent
            {
                Margin = new Thickness(4, 0, 4, 0),
                MaxHeight = height - 8
            };

            // Wire up click event
            _content.Clicked += (s, e) => Clicked?.Invoke(this, EventArgs.Empty);

            // Wrap in transparent grid
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Children = { _content }
            };

            _xamlSource.Content = rootGrid;

            // Inject into taskbar
            if (!InjectIntoTaskbar())
            {
                LogService.Log("TaskbarWidget", "Failed to inject into taskbar");
                Dispose();
                return false;
            }

            // Position the widget
            UpdatePosition();

            _isVisible = true;
            LogService.Log("TaskbarWidget", "Widget initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("TaskbarWidget initialization failed", ex);
            return false;
        }
    }

    private IntPtr CreateHostWindow(IntPtr parent)
    {
        RegisterWindowClass();

        var hwnd = Native.CreateWindowExW(
            dwExStyle: Native.WS_EX_LAYERED,
            lpClassName: WidgetClassName,
            lpWindowName: "CodexBarWidgetHost",
            dwStyle: Native.WS_POPUP,
            x: 0, y: 0,
            nWidth: 0, nHeight: 0,
            hWndParent: parent,
            hMenu: IntPtr.Zero,
            hInstance: IntPtr.Zero,
            lpParam: IntPtr.Zero);

        LogService.Log("TaskbarWidget", $"Created host window: {hwnd:X}");
        return hwnd;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate? _wndProcDelegate; // prevent GC

    private void RegisterWindowClass()
    {
        if (_classRegistered) return;

        // Keep delegate alive to prevent GC
        _wndProcDelegate = WndProc;

        var wndClass = new Native.WNDCLASS
        {
            lpszClassName = WidgetClassName,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = Native.GetModuleHandleW(null)
        };

        var atom = Native.RegisterClassW(ref wndClass);
        _classRegistered = atom != 0;

        LogService.Log("TaskbarWidget", $"Registered window class: {_classRegistered}, atom: {atom}");
    }

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private bool InjectIntoTaskbar()
    {
        LogService.Log("TaskbarWidget", "Attempting to inject widget into taskbar");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            LogService.Log("TaskbarWidget", $"Injection attempt #{attempt}");

            var result = Native.SetParent(_hwnd, _hwndShell);
            if (result != IntPtr.Zero)
            {
                LogService.Log("TaskbarWidget", "Widget injected successfully");
                return true;
            }

            Thread.Sleep(500);
        }

        return false;
    }

    /// <summary>
    /// Update widget position in the taskbar
    /// </summary>
    public void UpdatePosition()
    {
        if (_appWindow == null || _hwndShell == IntPtr.Zero) return;

        // Re-find tray notify in case icons were added/removed
        _hwndTrayNotify = Native.FindTrayNotifyWnd(_hwndShell);

        Native.GetWindowRect(_hwndShell, out var taskbarRect);

        int x, y;

        // Position left of the system tray
        if (_hwndTrayNotify != IntPtr.Zero)
        {
            Native.GetWindowRect(_hwndTrayNotify, out var trayRect);
            // Convert to taskbar-relative coordinates
            x = trayRect.Left - taskbarRect.Left - _widgetWidth - 4;
        }
        else
        {
            x = taskbarRect.Width - _widgetWidth - 100;
        }

        y = 0;
        x = Math.Max(0, x);

        var height = taskbarRect.Height;

        _appWindow.MoveAndResize(new RectInt32(x, y, _widgetWidth, height));
        LogService.Log("TaskbarWidget", $"Positioned at ({x}, {y}), size ({_widgetWidth}x{height})");
    }

    /// <summary>
    /// Update usage display
    /// </summary>
    public void UpdateUsage(Dictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        if (_content == null || !_isVisible) return;

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
        if (_disposed || _hwnd == IntPtr.Zero) return;

        Native.ShowWindow(_hwnd, 5); // SW_SHOW
        _isVisible = true;
        LogService.Log("TaskbarWidget", "Widget shown");
    }

    /// <summary>
    /// Hide the widget
    /// </summary>
    public void Hide()
    {
        if (_hwnd == IntPtr.Zero) return;

        Native.ShowWindow(_hwnd, 0); // SW_HIDE
        _isVisible = false;
        LogService.Log("TaskbarWidget", "Widget hidden");
    }

    /// <summary>
    /// Re-inject widget after explorer restart
    /// </summary>
    public void Reinject()
    {
        if (_disposed) return;

        LogService.Log("TaskbarWidget", "Re-injecting widget after explorer restart");

        // Find new taskbar handles
        _hwndShell = Native.FindTaskbar();
        if (_hwndShell == IntPtr.Zero)
        {
            LogService.Log("TaskbarWidget", "Taskbar not found during reinject");
            return;
        }

        _hwndTrayNotify = Native.FindTrayNotifyWnd(_hwndShell);

        if (_hwnd != IntPtr.Zero)
        {
            Native.SetParent(_hwnd, _hwndShell);
            UpdatePosition();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isVisible = false;

        if (_xamlSource != null)
        {
            try
            {
                _xamlSource.Dispose();
            }
            catch { }
            _xamlSource = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                Native.SetParent(_hwnd, IntPtr.Zero);
                Native.DestroyWindow(_hwnd);
            }
            catch { }
            _hwnd = IntPtr.Zero;
        }

        _content = null;
        _appWindow = null;

        LogService.Log("TaskbarWidget", "Widget disposed");
    }
}
