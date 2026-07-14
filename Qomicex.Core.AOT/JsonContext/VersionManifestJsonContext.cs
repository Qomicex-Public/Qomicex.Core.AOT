using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.VersionManifest;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(VersionManifestRoot))]
[JsonSerializable(typeof(LatestVersionInfo))]
[JsonSerializable(typeof(ManifestVersionInfo))]
public partial class VersionManifestJsonContext : JsonSerializerContext
{
}
