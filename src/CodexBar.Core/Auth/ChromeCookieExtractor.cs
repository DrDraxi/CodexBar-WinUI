using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodexBar.Core.Auth;

/// <summary>
/// Extracts cookies from Chrome browser on Windows
/// </summary>
public static class ChromeCookieExtractor
{
    /// <summary>
    /// Get a cookie value for a specific domain and name from Chrome
    /// </summary>
    public static async Task<string?> GetCookieAsync(string domain, string cookieName)
    {
        var cookiesPath = GetChromeCookiesPath();
        if (cookiesPath == null || !File.Exists(cookiesPath))
        {
            return null;
        }

        var encryptionKey = await GetEncryptionKeyAsync();
        if (encryptionKey == null)
        {
            return null;
        }

        // Try multiple strategies to read the locked database
        return await TryReadCookieWithStrategies(cookiesPath, domain, cookieName, encryptionKey);
    }

    private static async Task<string?> TryReadCookieWithStrategies(string cookiesPath, string domain, string cookieName, byte[] encryptionKey)
    {
        // Strategy 1: Try SQLite URI with immutable mode (reads locked files directly)
        var result = await TryReadWithImmutableMode(cookiesPath, domain, cookieName, encryptionKey);
        if (result != null) return result;

        // Strategy 2: Copy file with shared access and retry
        result = await TryReadWithFileCopy(cookiesPath, domain, cookieName, encryptionKey);
        if (result != null) return result;

        return null;
    }

    private static async Task<string?> TryReadWithImmutableMode(string cookiesPath, string domain, string cookieName, byte[] encryptionKey)
    {
        try
        {
            // Use file: URI with immutable=1 to bypass locking
            var escapedPath = cookiesPath.Replace("\\", "/");
            var connectionString = $"Data Source=file:{escapedPath}?immutable=1";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            return await QueryCookie(connection, domain, cookieName, encryptionKey);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryReadWithFileCopy(string cookiesPath, string domain, string cookieName, byte[] encryptionKey)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"codexbar_cookies_{Guid.NewGuid()}.db");
        try
        {
            // Try copying with retries and different methods
            var copied = await TryCopyFileWithRetries(cookiesPath, tempPath);
            if (!copied) return null;

            var connectionString = $"Data Source={tempPath};Mode=ReadOnly;Pooling=False";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            return await QueryCookie(connection, domain, cookieName, encryptionKey);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static async Task<bool> TryCopyFileWithRetries(string sourcePath, string destPath)
    {
        // Try up to 3 times with different approaches
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using var dest = new FileStream(
                    destPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                await source.CopyToAsync(dest);
                return true;
            }
            catch
            {
                if (attempt < 2)
                {
                    await Task.Delay(100 * (attempt + 1));
                }
            }
        }
        return false;
    }

    private static async Task<string?> QueryCookie(SqliteConnection connection, string domain, string cookieName, byte[] encryptionKey)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT encrypted_value, value
            FROM cookies
            WHERE host_key LIKE @domain AND name = @name
            ORDER BY last_access_utc DESC
            LIMIT 1";
        command.Parameters.AddWithValue("@domain", $"%{domain}%");
        command.Parameters.AddWithValue("@name", cookieName);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            // Try unencrypted value first (older Chrome versions)
            var plainValue = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (!string.IsNullOrEmpty(plainValue))
            {
                return plainValue;
            }

            // Decrypt the value
            var encryptedValue = reader.IsDBNull(0) ? null : (byte[])reader["encrypted_value"];
            if (encryptedValue != null && encryptedValue.Length > 0)
            {
                return DecryptCookieValue(encryptedValue, encryptionKey);
            }
        }

        return null;
    }

    private static string? GetChromeCookiesPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Try different Chrome profile locations
        string[] possiblePaths =
        [
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Network", "Cookies"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cookies"),
            // Edge (Chromium-based)
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Network", "Cookies"),
        ];

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static async Task<byte[]?> GetEncryptionKeyAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] localStatePaths =
        [
            Path.Combine(localAppData, "Google", "Chrome", "User Data", "Local State"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Local State"),
        ];

        foreach (var localStatePath in localStatePaths)
        {
            if (!File.Exists(localStatePath)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(localStatePath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("os_crypt", out var osCrypt) &&
                    osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyElement))
                {
                    var encryptedKeyB64 = encryptedKeyElement.GetString();
                    if (encryptedKeyB64 != null)
                    {
                        var encryptedKey = Convert.FromBase64String(encryptedKeyB64);
                        // Remove "DPAPI" prefix (5 bytes)
                        if (encryptedKey.Length > 5 && Encoding.ASCII.GetString(encryptedKey, 0, 5) == "DPAPI")
                        {
                            var keyBytes = new byte[encryptedKey.Length - 5];
                            Array.Copy(encryptedKey, 5, keyBytes, 0, keyBytes.Length);
                            return ProtectedData.Unprotect(keyBytes, null, DataProtectionScope.CurrentUser);
                        }
                    }
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return null;
    }

    private static string? DecryptCookieValue(byte[] encryptedValue, byte[] key)
    {
        try
        {
            // Check for v10/v11 encryption (AES-GCM)
            if (encryptedValue.Length > 3 &&
                encryptedValue[0] == 'v' &&
                encryptedValue[1] == '1' &&
                (encryptedValue[2] == '0' || encryptedValue[2] == '1'))
            {
                // v10/v11: 3-byte prefix + 12-byte nonce + ciphertext + 16-byte tag
                var nonce = new byte[12];
                Array.Copy(encryptedValue, 3, nonce, 0, 12);

                var ciphertextWithTag = new byte[encryptedValue.Length - 15];
                Array.Copy(encryptedValue, 15, ciphertextWithTag, 0, ciphertextWithTag.Length);

                // AES-GCM tag is at the end
                var tagLength = 16;
                var ciphertext = new byte[ciphertextWithTag.Length - tagLength];
                var tag = new byte[tagLength];
                Array.Copy(ciphertextWithTag, 0, ciphertext, 0, ciphertext.Length);
                Array.Copy(ciphertextWithTag, ciphertext.Length, tag, 0, tagLength);

                var plaintext = new byte[ciphertext.Length];
                using var aesGcm = new AesGcm(key, tagLength);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
            else
            {
                // Older DPAPI encryption
                var decrypted = ProtectedData.Unprotect(encryptedValue, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
        }
        catch
        {
            return null;
        }
    }
}
