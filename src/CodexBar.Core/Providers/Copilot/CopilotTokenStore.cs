using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// Stores Copilot access token securely using DPAPI
/// </summary>
public static class CopilotTokenStore
{
    private static readonly string TokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexBar",
        "copilot_token.dat"
    );

    public static async Task<string?> LoadTokenAsync()
    {
        if (!File.Exists(TokenPath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(TokenPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public static async Task SaveTokenAsync(string token)
    {
        var dir = Path.GetDirectoryName(TokenPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(TokenPath, encrypted);
    }

    public static void DeleteToken()
    {
        if (File.Exists(TokenPath))
        {
            File.Delete(TokenPath);
        }
    }
}
