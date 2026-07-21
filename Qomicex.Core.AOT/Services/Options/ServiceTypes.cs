using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Services.Options;

#region 游戏版本定义

public readonly record struct GameVersion
{
    public string Version { get; init; }
    public string ReleaseType { get; init; }
    public DateTime ReleaseDate { get; init; }
}

#endregion

#region 游戏选项类型

public readonly record struct GameOption
{
    public string OptionName { get; init; }
    public string OptionValue { get; init; }
}

public sealed record class GameOptionsSnapshot
{
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    public GameOptionsSnapshot() { }

    public GameOptionsSnapshot(IReadOnlyDictionary<string, string> values)
    {
        Values = values;
    }
}

public sealed record class MinecraftOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("defaultValue")]
    public string DefaultValue { get; set; } = "";

    [JsonPropertyName("validValues")]
    public string ValidValues { get; set; } = "";

    [JsonPropertyName("introducedVersion")]
    public string IntroducedVersion { get; set; } = "";

    [JsonPropertyName("introducedVersionRaw")]
    public string IntroducedVersionRaw { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
}

public sealed record class OptionDefinition
{
    public string Name { get; set; } = "";
    public string DefaultValue { get; set; } = "";
    public string CurrentValue { get; set; } = "";
    public string Description { get; set; } = "(无描述)";
    public string ValidValuesRaw { get; set; } = "";
    public string IntroducedVersion { get; set; } = "";
    public bool IsAvailableInCurrentVersion { get; set; }
    public string ValueKind { get; set; } = "Text";
}

public sealed record class OptionViewItem
{
    public string Name { get; set; } = "";
    public string DefaultValue { get; set; } = "";
    public string CurrentValue { get; set; } = "";
    public string Description { get; set; } = "(无描述)";
    public string ValidValuesRaw { get; set; } = "";
    public string IntroducedVersion { get; set; } = "";
    public bool IsAvailableInCurrentVersion { get; set; }
    public string ValueKind { get; set; } = "Text";
}

#endregion

#region 服务器类型

public sealed record class ServerEntry
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? IconBase64 { get; set; }
    public bool AcceptTextures { get; set; }
}

public sealed record class ServerState
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public long Ping { get; set; }
    public int OnlinePlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? IconBase64 { get; set; }
}

public sealed record class LanServerEntry
{
    public string Motd { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public string DisplayAddress { get; set; } = string.Empty;
}

#endregion
