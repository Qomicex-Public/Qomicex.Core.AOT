using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Services.Options;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(List<MinecraftOption>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
internal partial class OptionsJsonContext : JsonSerializerContext
{
}
