using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using CommunityToolkit.Mvvm.Input;
using CodexBar.Views;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Auth;
using CodexBar.Services;

namespace CodexBar;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private UsagePopup? _usagePopup;
    private DispatcherQueueTimer? _refreshTimer;
    private Dictionary<UsageProvider, UsageSnapshot> _cachedSnapshots = new();

    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Check if another instance is already running
    /// </summary>
    public static bool IsAlreadyRunning()
    {
        _singleInstanceMutex = new Mutex(true, "CodexBar-SingleInstance-Mutex", out bool createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            return true;
        }
        return false;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LogService.Clear();
        LogService.Log("App", "OnLaunched started");

        // Check for single instance
        if (IsAlreadyRunning())
        {
            LogService.Log("App", "Another instance is already running, exiting");
            Exit();
            return;
        }

        try
        {
            // Create hidden main window (required for WinUI lifecycle)
            _mainWindow = new MainWindow();
            LogService.Log("App", "MainWindow created");

            // Initialize manual cookie store from settings
            ManualCookieStore.SetCookies(SettingsService.Instance.Settings.ManualCookies);
            LogService.Log("App", "ManualCookieStore initialized");

            // Initialize tray icon
            InitializeTrayIcon();
            LogService.Log("App", "TrayIcon initialized");

            // Run first-launch detection if needed
            if (!SettingsService.Instance.Settings.FirstLaunchComplete)
            {
                LogService.Log("App", "Running first-launch detection");
                await RunFirstLaunchDetectionAsync();
            }

            // Initial fetch in background
            LogService.Log("App", "Starting initial refresh");
            _ = RefreshUsageAsync();

            // Start periodic refresh timer
            StartRefreshTimer();
            LogService.Log("App", "Refresh timer started");
        }
        catch (Exception ex)
        {
            LogService.LogError("OnLaunched failed", ex);
        }
    }

    private void StartRefreshTimer()
    {
        var intervalMinutes = SettingsService.Instance.Settings.RefreshIntervalMinutes;
        if (intervalMinutes <= 0) intervalMinutes = 1;

        _refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);
        _refreshTimer.Tick += (s, e) => _ = RefreshUsageAsync();
        _refreshTimer.Start();

        LogService.Log("App", $"Refresh timer set to {intervalMinutes} minute(s)");
    }

    private async Task RunFirstLaunchDetectionAsync()
    {
        var settings = SettingsService.Instance.Settings;

        // Run detection on background thread to avoid UI freeze
        var availableProviders = await Task.Run(async () =>
        {
            var detected = new HashSet<UsageProvider>();

            // Test each provider's availability
            foreach (var provider in ProviderRegistry.AllProviders)
            {
                try
                {
                    var fetcher = ProviderRegistry.GetFetcher(provider);
                    if (fetcher != null)
                    {
                        var isAvailable = await fetcher.IsAvailableAsync();
                        LogService.Log("Detection", $"{provider}: {(isAvailable ? "Available" : "Not available")}");
                        if (isAvailable)
                        {
                            detected.Add(provider);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError($"Detection failed for {provider}", ex);
                }
            }

            return detected;
        });

        // If we found some providers, disable the ones that aren't available
        if (availableProviders.Count > 0)
        {
            settings.EnabledProviders = availableProviders;
            LogService.Log("Detection", $"Enabled providers: {string.Join(", ", availableProviders)}");
        }
        else
        {
            LogService.Log("Detection", "No providers detected, keeping defaults");
        }

        settings.FirstLaunchComplete = true;
        SettingsService.Instance.Save();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            var contextMenu = new MenuFlyout();

            var showItem = new MenuFlyoutItem { Text = "Show" };
            showItem.Click += (s, e) => ShowUsagePopup();
            contextMenu.Items.Add(showItem);

            var refreshItem = new MenuFlyoutItem { Text = "Refresh" };
            refreshItem.Click += (s, e) => RefreshUsage();
            contextMenu.Items.Add(refreshItem);

            var settingsItem = new MenuFlyoutItem { Text = "Settings" };
            settingsItem.Click += (s, e) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new MenuFlyoutSeparator());

            var exitItem = new MenuFlyoutItem { Text = "Quit" };
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            // Check for ico file first, then fall back to png
            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");

            LogService.Log("TrayIcon", $"ICO path: {icoPath}, exists: {File.Exists(icoPath)}");
            LogService.Log("TrayIcon", $"PNG path: {pngPath}, exists: {File.Exists(pngPath)}");

            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "CodexBar - AI Usage Tracker",
                ContextMenuMode = ContextMenuMode.SecondWindow,
                ContextFlyout = contextMenu
            };

            // Try to set icon - H.NotifyIcon needs System.Drawing.Icon for the tray
            if (File.Exists(pngPath))
            {
                LogService.Log("TrayIcon", "Converting PNG to Icon");
                try
                {
                    using var bitmap = new System.Drawing.Bitmap(pngPath);
                    // Resize to standard icon size (32x32 or 16x16)
                    using var resized = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(32, 32));
                    var iconHandle = resized.GetHicon();
                    _trayIcon.Icon = System.Drawing.Icon.FromHandle(iconHandle);
                    LogService.Log("TrayIcon", "Icon set successfully from PNG");
                }
                catch (Exception ex)
                {
                    LogService.LogError("Failed to convert PNG to icon", ex);
                    // Try IconSource as fallback
                    try
                    {
                        _trayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(pngPath));
                        LogService.Log("TrayIcon", "Fallback: Using IconSource");
                    }
                    catch (Exception ex2)
                    {
                        LogService.LogError("Fallback IconSource also failed", ex2);
                    }
                }
            }
            else
            {
                LogService.Log("TrayIcon", "WARNING: No icon file found!");
            }

            // Create the tray icon
            _trayIcon.ForceCreate();
            LogService.Log("TrayIcon", "ForceCreate called");

            // Handle double-click to show usage popup
            _trayIcon.DoubleClickCommand = new RelayCommand(ShowUsagePopup);
            LogService.Log("TrayIcon", "DoubleClickCommand set");
        }
        catch (Exception ex)
        {
            LogService.LogError("InitializeTrayIcon failed", ex);
        }
    }

    public void ShowUsagePopup()
    {
        if (_usagePopup == null)
        {
            _usagePopup = new UsagePopup();
            _usagePopup.Closed += (s, e) => _usagePopup = null;
        }

        // Show cached data immediately if available
        if (_cachedSnapshots.Count > 0)
        {
            _usagePopup.UpdateUsage(_cachedSnapshots);
        }

        _usagePopup.Activate();
    }

    public void ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
    }

    public void RefreshUsage() => _ = RefreshUsageAsync();

    public async Task RefreshUsageAsync()
    {
        LogService.Log("Refresh", "Starting refresh");

        if (_cachedSnapshots.Count == 0)
        {
            _usagePopup?.SetLoading();
        }

        try
        {
            var enabledProviders = SettingsService.Instance.Settings.EnabledProviders;
            LogService.Log("Refresh", $"Fetching for providers: {string.Join(", ", enabledProviders)}");

            // Run fetching on background thread to avoid UI freeze
            var snapshots = await Task.Run(async () =>
                await ProviderRegistry.FetchAllAsync(enabledProviders));

            foreach (var kvp in snapshots)
            {
                if (kvp.Value.IsValid)
                {
                    LogService.Log("Refresh", $"{kvp.Key}: Primary={kvp.Value.Primary?.UsedPercent:F1}%");
                }
                else
                {
                    LogService.Log("Refresh", $"{kvp.Key}: Error={kvp.Value.Error}");
                }
            }

            // Cache the snapshots for instant display when popup opens
            _cachedSnapshots = snapshots;

            _usagePopup?.UpdateUsage(snapshots);
            LogService.Log("Refresh", "Refresh completed");
        }
        catch (Exception ex)
        {
            LogService.LogError("Refresh failed", ex);
            _usagePopup?.SetError($"Error: {ex.Message}");
        }
    }

    public void ExitApplication()
    {
        _refreshTimer?.Stop();
        _trayIcon?.Dispose();
        _usagePopup?.Close();
        _settingsWindow?.Close();
        _mainWindow?.Close();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Exit();
    }
}
