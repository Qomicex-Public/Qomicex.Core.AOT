using System.Net.Http.Json;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Auth;

namespace Qomicex.Core.AOT.Services;

internal sealed class YggdrasilAuthProvider : IAuthProvider
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly AuthJsonContext Ctx = AuthJsonContext.Default;

    public YggdrasilAuthProvider(HttpClient http, string serverUrl)
    {
        _http = http;
        _baseUrl = serverUrl.TrimEnd('/') + "/";
    }

    public async Task<AuthResult> AuthenticateAsync(AuthRequest request)
    {
        var req = new YggdrasilAuthenticateRequest(
            Agent: new("Minecraft", 1),
            Username: request.Username ?? "",
            Password: request.Password ?? "",
            ClientToken: Guid.NewGuid().ToString("N"),
            RequestUser: true
        );

        var response = await _http.PostAsJsonAsync(
            $"{_baseUrl}authserver/authenticate",
            req,
            Ctx.YggdrasilAuthenticateRequest);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadFromJsonAsync(Ctx.YggdrasilError);
            return new AuthResult
            {
                Success = false,
                ErrorMessage = err?.ErrorMessage ?? $"认证失败: {response.StatusCode}"
            };
        }

        var authResp = await response.Content.ReadFromJsonAsync(Ctx.YggdrasilAuthenticateResponse);
        if (authResp?.AccessToken == null)
        {
            return new AuthResult { Success = false, ErrorMessage = "无法解析认证响应" };
        }

        var profile = authResp.SelectedProfile ?? authResp.AvailableProfiles?.FirstOrDefault();
        var userType = authResp.User?.Properties
            ?.FirstOrDefault(p => p.Name == "userType")?.Value;

        return new AuthResult
        {
            Success = true,
            Username = profile?.Name,
            AccessToken = authResp.AccessToken,
            ClientToken = authResp.ClientToken,
            Uuid = profile?.Id,
            UserType = userType,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(6)
        };
    }

    public async Task<bool> ValidateAsync(string accessToken)
    {
        var req = new YggdrasilValidateRequest(
            AccessToken: accessToken,
            ClientToken: ""
        );

        var response = await _http.PostAsJsonAsync(
            $"{_baseUrl}authserver/validate",
            req,
            Ctx.YggdrasilValidateRequest);

        return response.StatusCode == System.Net.HttpStatusCode.NoContent;
    }

    public async Task InvalidateAsync(string accessToken)
    {
        var req = new YggdrasilInvalidateRequest(
            AccessToken: accessToken,
            ClientToken: ""
        );

        await _http.PostAsJsonAsync(
            $"{_baseUrl}authserver/invalidate",
            req,
            Ctx.YggdrasilInvalidateRequest);
    }
}
