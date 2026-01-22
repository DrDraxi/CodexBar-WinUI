namespace CodexBar.Core.Logging;

/// <summary>
/// Simple debug logging for core library
/// </summary>
public static class DebugLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexBar",
        "debug.log"
    );

    private static readonly object _lock = new();

    public static void Log(string category, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{category}] {message}";

        System.Diagnostics.Debug.WriteLine(logLine);

        try
        {
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.AppendAllText(LogPath, logLine + Environment.NewLine);
            }
        }
        catch { }
    }
}
