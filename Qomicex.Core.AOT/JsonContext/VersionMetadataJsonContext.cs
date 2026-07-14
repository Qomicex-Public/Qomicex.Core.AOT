using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(CompleteVersionMetadata))]
[JsonSerializable(typeof(Library))]
[JsonSerializable(typeof(LibraryDownloads))]
[JsonSerializable(typeof(Artifact))]
[JsonSerializable(typeof(LibraryExtract))]
[JsonSerializable(typeof(AssetIndex))]
[JsonSerializable(typeof(VersionDownloads))]
[JsonSerializable(typeof(VersionArguments))]
[JsonSerializable(typeof(VersionArgumentsNew))]
[JsonSerializable(typeof(VersionArgumentsOld))]
[JsonSerializable(typeof(ArgumentItem))]
[JsonSerializable(typeof(ArgumentString))]
[JsonSerializable(typeof(ArgumentObject))]
[JsonSerializable(typeof(Rule))]
[JsonSerializable(typeof(OsRequirement))]
[JsonSerializable(typeof(JavaVersion))]
public partial class VersionMetadataJsonContext : JsonSerializerContext
{
}
