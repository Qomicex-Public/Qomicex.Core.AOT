using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Utils;

public static class JsonHelper
{
    private static readonly CombinedJsonContext CombinedContext = CombinedJsonContext.Default;

    public static string Serialize(VersionManifestRoot obj)
    {
        return JsonSerializer.Serialize(obj, CombinedContext.VersionManifestRoot);
    }

    public static VersionManifestRoot? DeserializeVersionManifest(string json)
    {
        return JsonSerializer.Deserialize(json, CombinedContext.VersionManifestRoot);
    }

    public static string Serialize(CompleteVersionMetadata obj)
    {
        return JsonSerializer.Serialize(obj, CombinedContext.CompleteVersionMetadata);
    }

    public static CompleteVersionMetadata? DeserializeVersionMetadata(string json)
    {
        return JsonSerializer.Deserialize(json, CombinedContext.CompleteVersionMetadata);
    }

    public static T? ToObject<T>(this JsonNode node, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.Deserialize(node.ToJsonString(), typeInfo);
    }
}
