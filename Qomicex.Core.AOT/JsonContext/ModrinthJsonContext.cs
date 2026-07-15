using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.Expansion.Modrinth;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchResultInfo))]
[JsonSerializable(typeof(ProjectInfo))]
[JsonSerializable(typeof(GalleryItem))]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(VersionFileInfo))]
[JsonSerializable(typeof(FileHashes))]
[JsonSerializable(typeof(DependenciesInfo))]
[JsonSerializable(typeof(ProjectVersionInfo))]
[JsonSerializable(typeof(ModrinthVersionInfo))]
[JsonSerializable(typeof(ModrinthFile))]
[JsonSerializable(typeof(ModrinthVersionResponse))]
[JsonSerializable(typeof(Dictionary<string, ModrinthVersionInfo>))]
[JsonSerializable(typeof(ModrinthTag))]
[JsonSerializable(typeof(List<ModrinthTag>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ModLoaderType))]
internal partial class ModrinthJsonContext : JsonSerializerContext
{
}
