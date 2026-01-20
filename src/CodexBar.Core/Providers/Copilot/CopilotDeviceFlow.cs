using System.Text.Json;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// GitHub Device Flow OAuth for Copilot
/// </summary>
public class CopilotDeviceFlow
{
    private static readonly HttpClient HttpClient = new();

    // VS Code's GitHub OAuth Client ID (public)
    private const string ClientId = "Iv1.b507a08c87ecfe98";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string TokenUrl = "https://github.com/login/oauth/access_token";

    /// <summary>
    /// Start the device flow - returns code for user to enter at GitHub
    /// </summary>
    public static async Task<DeviceCodeResponse?> RequestDeviceCodeAsync()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = "read:user"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUrl)
        {
            Content = content
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DeviceCodeResponse>(json);
    }

    /// <summary>
    /// Poll for access token after user authorizes
    /// </summary>
    public static async Task<TokenResponse?> PollForTokenAsync(string deviceCode, int interval, CancellationToken cancellationToken = default)
    {
        var pollInterval = TimeSpan.FromSeconds(interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollInterval, cancellationToken);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = content
            };
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var response = await HttpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

            if (tokenResponse?.AccessToken != null)
            {
                return tokenResponse;
            }

            // Handle polling states
            switch (tokenResponse?.Error)
            {
                case "authorization_pending":
                    // User hasn't authorized yet, keep polling
                    continue;
                case "slow_down":
                    // Add 5 seconds to interval
                    pollInterval = pollInterval.Add(TimeSpan.FromSeconds(5));
                    continue;
                case "expired_token":
                case "access_denied":
                    // User denied or token expired
                    return tokenResponse;
                default:
                    // Unknown error
                    return tokenResponse;
            }
        }

        return null;
    }
}
