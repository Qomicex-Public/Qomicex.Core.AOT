using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示启动参数
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(VersionArgumentsOld), typeDiscriminator: "old")]
[JsonDerivedType(typeof(VersionArgumentsNew), typeDiscriminator: "new")]
public abstract record VersionArguments;

/// <summary>
/// 新格式启动参数（1.13+）
/// </summary>
public record VersionArgumentsNew(
    [property: JsonPropertyName("game")] List<ArgumentItem> Game,
    [property: JsonPropertyName("jvm")] List<ArgumentItem> Jvm
) : VersionArguments;

/// <summary>
/// 旧格式启动参数（1.12 及以下）
/// </summary>
public record VersionArgumentsOld(
    [property: JsonPropertyName("game")] List<string> Game,
    [property: JsonPropertyName("jvm")] List<string> Jvm
) : VersionArguments;

/// <summary>
/// 参数项，可以是字符串或带规则的对象
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ArgumentString), typeDiscriminator: "string")]
[JsonDerivedType(typeof(ArgumentObject), typeDiscriminator: "object")]
public abstract record ArgumentItem;

/// <summary>
/// 字符串格式的参数
/// </summary>
public record ArgumentString(
    [property: JsonPropertyName("value")] string Value
) : ArgumentItem;

/// <summary>
/// 带规则的对象格式参数
/// </summary>
public record ArgumentObject(
    [property: JsonPropertyName("value")] List<string> Value,
    [property: JsonPropertyName("rules")] List<Rule> Rules
) : ArgumentItem;