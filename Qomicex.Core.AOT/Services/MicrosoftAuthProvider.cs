using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Interfaces;

namespace Qomicex.Core.AOT.Services;

internal sealed class MicrosoftAuthProvider : IAuthProvider
{
    private readonly HttpClient _http;
    private readonly string _clientId;

    public MicrosoftAuthProvider(HttpClient http, string clientId)
    {
        _http = http;
        _clientId = clientId;
    }

    public async Task<AuthResult> AuthenticateAsync(AuthRequest request)
    {
        if (string.IsNullOrEmpty(request.AccessToken))
            return new AuthResult { Success = false, ErrorMessage = "需要 access_token（来自设备码流程）" };

        return await CompleteLoginAsync(request.AccessToken, request.AccessToken);
    }

    public async Task<bool> ValidateAsync(string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task InvalidateAsync(string accessToken)
    {
        return Task.CompletedTask;
    }

    public async Task<JsonObject?> StartDeviceCodeAsync()
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "scope", "offline_access XboxLive.signin XboxLive.offline_access" }
        };
        var resp = await _http.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
            new FormUrlEncodedContent(form));
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return null;

        return JsonNode.Parse(body)?.AsObject();
    }

    public async Task<JsonObject?> PollForTokenAsync(string deviceCode)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" },
            { "device_code", deviceCode }
        };
        var resp = await _http.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
            new FormUrlEncodedContent(form));
        var body = await resp.Content.ReadAsStringAsync();
        return JsonNode.Parse(body)?.AsObject();
    }

    public async Task<AuthResult> CompleteLoginAsync(string accessToken, string refreshToken)
    {
        try
        {
            var (xboxToken, uhs) = await AuthenticateXboxLiveAsync(accessToken);
            var xstsToken = await AuthenticateXstsAsync(xboxToken);
            var mcToken = await AuthenticateMinecraftAsync(xstsToken, uhs);
            var profile = await GetMinecraftProfileAsync(mcToken);

            return new AuthResult
            {
                Success = true,
                Username = profile?.name,
                Uuid = profile?.id,
                AccessToken = mcToken,
                RefreshToken = refreshToken,
                UserType = "msa",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
            };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AuthResult> RefreshLoginAsync(string oldRefreshToken)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", _clientId },
            { "grant_type", "refresh_token" },
            { "scope", "XboxLive.signin offline_access" },
            { "refresh_token", oldRefreshToken }
        };
        var resp = await _http.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
            new FormUrlEncodedContent(form));
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return new AuthResult { Success = false, ErrorMessage = "令牌刷新失败" };

        var data = JsonNode.Parse(body)?.AsObject();
        if (data == null)
            return new AuthResult { Success = false, ErrorMessage = "无法解析刷新响应" };

        var newAccessToken = data["access_token"]?.ToString();
        var newRefreshToken = data["refresh_token"]?.ToString();

        if (string.IsNullOrEmpty(newAccessToken))
            return new AuthResult { Success = false, ErrorMessage = "刷新响应缺少 access_token" };

        return await CompleteLoginAsync(newAccessToken, newRefreshToken ?? oldRefreshToken);
    }

    private async Task<(string token, string uhs)> AuthenticateXboxLiveAsync(string accessToken)
    {
        var payload = new JsonObject
        {
            ["Properties"] = new JsonObject
            {
                ["AuthMethod"] = "RPS",
                ["SiteName"] = "user.auth.xboxlive.com",
                ["RpsTicket"] = $"d={accessToken}"
            },
            ["RelyingParty"] = "http://auth.xboxlive.com",
            ["TokenType"] = "JWT"
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate")
        {
            Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        var obj = JsonNode.Parse(body)?.AsObject()
            ?? throw new Exception("无法解析 Xbox Live 响应");

        var token = obj["Token"]?.ToString() ?? throw new Exception("Xbox Live 响应缺少 Token");
        var uhs = obj["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString()
            ?? throw new Exception("Xbox Live 响应缺少 uhs");

        return (token, uhs);
    }

    private async Task<string> AuthenticateXstsAsync(string xboxToken)
    {
        var payload = new JsonObject
        {
            ["Properties"] = new JsonObject
            {
                ["SandboxId"] = "RETAIL",
                ["UserTokens"] = new JsonArray(xboxToken)
            },
            ["RelyingParty"] = "rp://api.minecraftservices.com/",
            ["TokenType"] = "JWT"
        };

        var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", content);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        var obj = JsonNode.Parse(body)?.AsObject()
            ?? throw new Exception("无法解析 XSTS 响应");

        return obj["Token"]?.ToString() ?? throw new Exception("XSTS 响应缺少 Token");
    }

    private async Task<string> AuthenticateMinecraftAsync(string xstsToken, string uhs)
    {
        var payload = new JsonObject
        {
            ["identityToken"] = $"XBL3.0 x={uhs};{xstsToken}"
        };

        var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(
            "https://api.minecraftservices.com/authentication/login_with_xbox", content);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync();
        var obj = JsonNode.Parse(body)?.AsObject()
            ?? throw new Exception("无法解析 Minecraft 认证响应");

        return obj["access_token"]?.ToString()
            ?? throw new Exception("Minecraft 认证响应缺少 access_token");
    }

    private async Task<(string id, string name)?> GetMinecraftProfileAsync(string mcToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcToken);
        using var resp = await _http.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
            return null;

        var body = await resp.Content.ReadAsStringAsync();
        var obj = JsonNode.Parse(body)?.AsObject();

        if (obj == null)
            return null;

        var id = obj["id"]?.ToString();
        var name = obj["name"]?.ToString();

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            return null;

        return (id, name);
    }
}
