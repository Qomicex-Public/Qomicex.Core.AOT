namespace Qomicex.Core.AOT.Interfaces;

public interface IAuthProvider
{
    Task<AuthResult> AuthenticateAsync(AuthRequest request);

    Task<bool> ValidateAsync(string accessToken);

    Task InvalidateAsync(string accessToken);

    Task<DeviceCodeResult?> StartDeviceCodeAsync() => null;
    Task<PollTokenResult?> PollForTokenAsync(string deviceCode) => null;
    Task<AuthResult> CompleteLoginAsync(string accessToken, string refreshToken) =>
        Task.FromResult(new AuthResult { Success = false, ErrorMessage = "此认证方式不支持设备码登录" });
    Task<AuthResult> RefreshLoginAsync(string refreshToken) =>
        Task.FromResult(new AuthResult { Success = false, ErrorMessage = "此认证方式不支持令牌刷新" });
}

public sealed class AuthRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? AccessToken { get; set; }
    public string? ServerUrl { get; set; }
    public bool IsOffline { get; set; }
}

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string? Username { get; init; }
    public string? AccessToken { get; init; }
    public string? ClientToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? Uuid { get; init; }
    public string? UserType { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record DeviceCodeResult(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int Interval,
    int ExpiresIn
);

public sealed record PollTokenResult(
    string? AccessToken,
    string? RefreshToken,
    string? Error,
    bool IsCompleted,
    bool IsPending
);
