using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.Expansion.CurseForge;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(CurseForgeInfo))]
[JsonSerializable(typeof(CurseForgeFileInfo))]
[JsonSerializable(typeof(CurseForgeFilesMeta))]
[JsonSerializable(typeof(CurseForgeDependenciesMeta))]
[JsonSerializable(typeof(FingerprintsFilesMeta))]
[JsonSerializable(typeof(CategoryMeta))]
[JsonSerializable(typeof(AuthorMeta))]
[JsonSerializable(typeof(ScreenshotsMeta))]
[JsonSerializable(typeof(Dictionary<long, FingerprintsFilesMeta>))]
[JsonSerializable(typeof(List<CurseForgeDependenciesMeta>))]
internal partial class CurseForgeJsonContext : JsonSerializerContext
{
}
