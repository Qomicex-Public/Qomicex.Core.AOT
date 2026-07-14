using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Auth;

internal sealed record YggdrasilAgent(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] int Version
);

internal sealed record YggdrasilAuthenticateRequest(
    [property: JsonPropertyName("agent")] YggdrasilAgent Agent,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("clientToken")] string ClientToken,
    [property: JsonPropertyName("requestUser")] bool RequestUser
);

internal sealed record YggdrasilAuthenticateResponse(
    [property: JsonPropertyName("accessToken")] string? AccessToken,
    [property: JsonPropertyName("clientToken")] string? ClientToken,
    [property: JsonPropertyName("availableProfiles")] List<YggdrasilProfile>? AvailableProfiles,
    [property: JsonPropertyName("selectedProfile")] YggdrasilProfile? SelectedProfile,
    [property: JsonPropertyName("user")] YggdrasilUser? User
);

internal sealed record YggdrasilRefreshRequest(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("clientToken")] string ClientToken,
    [property: JsonPropertyName("requestUser")] bool RequestUser,
    [property: JsonPropertyName("selectedProfile")] YggdrasilProfile? SelectedProfile
);

internal sealed record YggdrasilRefreshResponse(
    [property: JsonPropertyName("accessToken")] string? AccessToken,
    [property: JsonPropertyName("clientToken")] string? ClientToken,
    [property: JsonPropertyName("selectedProfile")] YggdrasilProfile? SelectedProfile,
    [property: JsonPropertyName("user")] YggdrasilUser? User
);

internal sealed record YggdrasilValidateRequest(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("clientToken")] string ClientToken
);

internal sealed record YggdrasilInvalidateRequest(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("clientToken")] string ClientToken
);

internal sealed record YggdrasilProfile(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("properties")] List<YggdrasilProperty>? Properties
);

internal sealed record YggdrasilUser(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("properties")] List<YggdrasilProperty>? Properties
);

internal sealed record YggdrasilProperty(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("value")] string? Value
);

internal sealed record YggdrasilError(
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("cause")] string? Cause
);
