using System.Text.Json;
using System.Text.Json.Serialization;
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
        UsageProvider.Augment,
        UsageProvider.Kiro,
        UsageProvider.Amp,
        UsageProvider.Factory,
        UsageProvider.Zai,
        UsageProvider.Kimi,
        UsageProvider.KimiK2,
        UsageProvider.MiniMax,
        UsageProvider.VertexAI,
        UsageProvider.OpenCode,
        UsageProvider.Antigravity
    ];

    /// <summary>
    /// Manual cookie storage per provider
    /// </summary>
    public Dictionary<string, string> ManualCookies { get; set; } = new();

    /// <summary>
    /// Per-provider bar visibility for popup window (default: all visible)
    /// </summary>
    public Dictionary<string, BarVisibilitySettings> PopupBarVisibility { get; set; } = new();

    /// <summary>
    /// Per-provider bar visibility for taskbar widget (default: Primary+Secondary+Cost only)
    /// </summary>
    public Dictionary<string, BarVisibilitySettings> WidgetBarVisibility { get; set; } = new();

    /// <summary>
    /// Whether the taskbar widget is visible
    /// </summary>
    public bool TaskbarWidgetVisible { get; set; } = true;

    /// <summary>
    /// Custom display order for providers (list of provider name strings).
    /// Providers not in the list fall back to enum order at the end.
    /// </summary>
    public List<string>? ProviderOrder { get; set; }

    public BarVisibilitySettings GetPopupVisibility(UsageProvider provider) =>
        PopupBarVisibility.GetValueOrDefault(provider.ToString(), new BarVisibilitySettings());

    public BarVisibilitySettings GetWidgetVisibility(UsageProvider provider) =>
        WidgetBarVisibility.GetValueOrDefault(provider.ToString(),
            new BarVisibilitySettings { Tertiary = false, Quaternary = false });

    /// <summary>
    /// Returns the entries of the dictionary ordered by saved ProviderOrder,
    /// with any unlisted providers appended in enum order.
    /// </summary>
    public IEnumerable<KeyValuePair<UsageProvider, T>> GetOrderedProviders<T>(Dictionary<UsageProvider, T> dict)
    {
        if (ProviderOrder == null || ProviderOrder.Count == 0)
            return dict.OrderBy(kv => kv.Key);

        var orderLookup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < ProviderOrder.Count; i++)
            orderLookup[ProviderOrder[i]] = i;

        return dict.OrderBy(kv =>
        {
            var name = kv.Key.ToString();
            return orderLookup.TryGetValue(name, out var idx) ? idx : int.MaxValue;
        }).ThenBy(kv => kv.Key);
    }
}

/// <summary>
/// Per-bar visibility toggles for a provider
/// </summary>
public class BarVisibilitySettings
{
    public bool Primary { get; set; } = true;
    public bool Secondary { get; set; } = true;
    public bool Tertiary { get; set; } = true;
    public bool Quaternary { get; set; } = true;
    public bool Cost { get; set; } = true;
}
