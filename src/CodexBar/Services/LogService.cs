using System.Diagnostics;

namespace CodexBar.Services;

/// <summary>
/// Simple logging service for debugging
/// </summary>
public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexBar",
        "debug.log"
    );

    private static readonly object _lock = new();

    static LogService()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch { }
    }

    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] {message}";

        Debug.WriteLine(logLine);

        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogPath, logLine + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void Log(string category, string message)
    {
        Log($"[{category}] {message}");
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var logMessage = ex != null ? $"ERROR: {message} - {ex.Message}\n{ex.StackTrace}" : $"ERROR: {message}";
        Log(logMessage);
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(LogPath))
            {
                File.Delete(LogPath);
            }
        }
        catch { }
    }
}
