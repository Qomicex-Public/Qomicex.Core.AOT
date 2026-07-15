using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Utils;

namespace Qomicex.Core.AOT.Services;

public sealed class DefaultAuthProvider : IAuthProvider
{
    public Task<AuthResult> AuthenticateAsync(AuthRequest request)
    {
        var uuid = OfflineUuidHelper.GenerateUuid(request.Username ?? "Player");
        return Task.FromResult(new AuthResult
        {
            Success = true,
            Username = request.Username ?? "Player",
            AccessToken = Guid.NewGuid().ToString(),
            ClientToken = Guid.NewGuid().ToString(),
            Uuid = uuid,
            UserType = "legacy"
        });
    }

    public Task<bool> ValidateAsync(string accessToken)
    {
        return Task.FromResult(true);
    }

    public Task InvalidateAsync(string accessToken)
    {
        return Task.CompletedTask;
    }
}
