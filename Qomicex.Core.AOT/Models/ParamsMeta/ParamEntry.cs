using Qomicex.Core.AOT.Models.VersionMetadata;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.ParamsMeta
{
    public record ParamEntry(
        [property: JsonPropertyName("rules")] List<VersionMetadata.Rule>? Rules,
        [property: JsonPropertyName("value")] JsonElement Value);

    public record Config(
        [property: JsonPropertyName("arguments")] Arguments Arguments,
        [property: JsonPropertyName("inheritsFrom")] string InheritsFrom,
        [property: JsonPropertyName("mainClass")] string MainClass,
        [property: JsonPropertyName("minecraftArguments")] string MinecraftArguments,
        [property: JsonPropertyName("assetIndex")] string AssetIndex
    );
    public record Arguments(
        [property: JsonPropertyName("jvm")] List<JsonElement> Jvm,
        [property: JsonPropertyName("game")] List<JsonElement> Game
    );

}
