using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.Auth;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(YggdrasilAuthenticateRequest))]
[JsonSerializable(typeof(YggdrasilAuthenticateResponse))]
[JsonSerializable(typeof(YggdrasilRefreshRequest))]
[JsonSerializable(typeof(YggdrasilRefreshResponse))]
[JsonSerializable(typeof(YggdrasilValidateRequest))]
[JsonSerializable(typeof(YggdrasilInvalidateRequest))]
[JsonSerializable(typeof(YggdrasilProfile))]
[JsonSerializable(typeof(YggdrasilUser))]
[JsonSerializable(typeof(YggdrasilProperty))]
[JsonSerializable(typeof(YggdrasilError))]
[JsonSerializable(typeof(YggdrasilAgent))]
internal partial class AuthJsonContext : JsonSerializerContext
{
}
