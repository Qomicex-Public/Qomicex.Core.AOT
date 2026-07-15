using System.Text.Json.Serialization;
using Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;

namespace Qomicex.Core.AOT.JsonContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(ModpackInfo))]
[JsonSerializable(typeof(TagInfo))]
[JsonSerializable(typeof(VersionInfo))]
[JsonSerializable(typeof(SpecsInfo))]
[JsonSerializable(typeof(TargetInfo))]
[JsonSerializable(typeof(AuthorInfo))]
[JsonSerializable(typeof(LinkInfo))]
[JsonSerializable(typeof(ArtInfo))]
[JsonSerializable(typeof(MetaInfo))]
[JsonSerializable(typeof(RatingInfo))]
[JsonSerializable(typeof(VersionDetail))]
[JsonSerializable(typeof(FtbFileInfo))]
[JsonSerializable(typeof(ChangelogResult))]
[JsonSerializable(typeof(CacheData))]
[JsonSerializable(typeof(List<ModpackInfo>))]
internal partial class FTBJsonContext : JsonSerializerContext
{
}
