using System.Text.Json;
using System.Text.Json.Serialization;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.JsonConverters;

public class VersionArgumentsConverter : JsonConverter<VersionArguments>
{
    private static readonly VersionMetadataJsonContext Ctx = VersionMetadataJsonContext.Default;

    public override VersionArguments? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (!root.TryGetProperty("game", out var gameEl))
            return null;

        var game = ParseItems(gameEl);
        var jvm = root.TryGetProperty("jvm", out var jvmEl)
            ? ParseItems(jvmEl)
            : [];

        return new VersionArgumentsNew(game, jvm);
    }

    public override void Write(Utf8JsonWriter writer, VersionArguments value, JsonSerializerOptions options)
    {
        if (value is not VersionArgumentsNew args)
            throw new NotSupportedException();

        writer.WriteStartObject();
        writer.WritePropertyName("game");
        WriteItems(writer, args.Game);
        writer.WritePropertyName("jvm");
        WriteItems(writer, args.Jvm);
        writer.WriteEndObject();
    }

    private static List<ArgumentItem> ParseItems(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];

        var items = new List<ArgumentItem>();
        foreach (var item in element.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    items.Add(new ArgumentString(item.GetString() ?? ""));
                    break;
                case JsonValueKind.Object:
                {
                    var value = item.TryGetProperty("value", out var vEl)
                        ? vEl.ValueKind switch
                        {
                            JsonValueKind.String => [vEl.GetString() ?? ""],
                            JsonValueKind.Array => vEl.EnumerateArray().Select(e => e.GetString() ?? "").ToList(),
                            _ => []
                        }
                        : [];
                    var rules = item.TryGetProperty("rules", out var rEl)
                        ? DeserializeRules(rEl)
                        : [];
                    items.Add(new ArgumentObject(value, rules));
                    break;
                }
            }
        }
        return items;
    }

    private static List<Rule> DeserializeRules(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];

        var rules = new List<Rule>();
        foreach (var r in element.EnumerateArray())
        {
            var rule = JsonSerializer.Deserialize(r.GetRawText(), Ctx.Rule);
            if (rule != null) rules.Add(rule);
        }
        return rules;
    }

    private static void WriteItems(Utf8JsonWriter writer, List<ArgumentItem> items)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            switch (item)
            {
                case ArgumentString s:
                    writer.WriteStringValue(s.Value);
                    break;
                case ArgumentObject o:
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("value");
                    if (o.Value.Count == 1)
                        writer.WriteStringValue(o.Value[0]);
                    else
                    {
                        writer.WriteStartArray();
                        foreach (var v in o.Value)
                            writer.WriteStringValue(v);
                        writer.WriteEndArray();
                    }
                    if (o.Rules.Count > 0)
                    {
                        writer.WritePropertyName("rules");
                        writer.WriteStartArray();
                        foreach (var rule in o.Rules)
                            JsonSerializer.Serialize(writer, rule, Ctx.Rule);
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                    break;
                }
            }
        }
        writer.WriteEndArray();
    }
}
