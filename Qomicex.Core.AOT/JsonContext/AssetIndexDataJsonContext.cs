using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.JsonContext;

public record AssetIndexData(
    [property: JsonPropertyName("objects")] Dictionary<string, AssetObject> Objects
);

public record AssetObject(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("size")] long Size
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(AssetIndexData))]
[JsonSerializable(typeof(AssetObject))]
[JsonSerializable(typeof(Dictionary<string, AssetObject>))]
public partial class AssetIndexDataJsonContext : JsonSerializerContext
{
}
