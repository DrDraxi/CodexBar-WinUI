using System.Diagnostics;

namespace CodexBar.Core.Debug;

/// <summary>
/// Simple debug logging utility
/// </summary>
public static class DebugLog
{
    /// <summary>
    /// Log a debug message with a category tag
    /// </summary>
    public static void Log(string category, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{category}] {message}");
    }
}
