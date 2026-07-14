using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(VersionManifestRoot))]
[JsonSerializable(typeof(CompleteVersionMetadata))]
public partial class CombinedJsonContext : JsonSerializerContext
{
}
