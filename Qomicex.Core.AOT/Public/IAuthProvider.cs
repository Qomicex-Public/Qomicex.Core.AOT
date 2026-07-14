namespace Qomicex.Core.AOT.Interfaces;

public interface IAuthProvider
{
    Task<AuthResult> AuthenticateAsync(AuthRequest request);

    Task<bool> ValidateAsync(string accessToken);

    Task InvalidateAsync(string accessToken);
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
