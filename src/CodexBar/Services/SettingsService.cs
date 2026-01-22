using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Services;

/// <summary>
/// Manages application settings persistence
/// </summary>
public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexBar",
        "settings.json"
    );

    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private AppSettings _settings = new();

    private SettingsService()
    {
        Load();
    }

    public AppSettings Settings => _settings;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}

/// <summary>
/// Application settings
/// </summary>
public class AppSettings
{
    public int RefreshIntervalMinutes { get; set; } = 1;
    public bool StartWithWindows { get; set; } = false;
    public bool FirstLaunchComplete { get; set; } = false;
    public HashSet<UsageProvider> EnabledProviders { get; set; } =
    [
        UsageProvider.Codex,
        UsageProvider.Claude,
        UsageProvider.Copilot,
        UsageProvider.Cursor,
        UsageProvider.Gemini,
        UsageProvider.JetBrains,
        UsageProvider.Augment
    ];

    /// <summary>
    /// Manual cookie storage per provider
    /// </summary>
    public Dictionary<string, string> ManualCookies { get; set; } = new();

    /// <summary>
    /// Whether the taskbar widget is visible
    /// </summary>
    public bool TaskbarWidgetVisible { get; set; } = false;
}
