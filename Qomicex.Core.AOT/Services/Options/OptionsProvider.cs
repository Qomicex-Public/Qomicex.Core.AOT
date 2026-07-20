using Qomicex.Core.AOT.JsonContext;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qomicex.Core.AOT.Services.Options;

internal sealed partial class OptionsProvider
{
    private const string DefaultDescription = "(无描述)";
    private const string FallbackLanguage = "en-US";

    private readonly List<GameVersion> _versions;
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSpecific;

    private readonly List<MinecraftOption> _options;
    private readonly Dictionary<string, Dictionary<string, string>> _descriptions;

    public OptionsProvider(string optionsJsonPath, string descriptionsJsonPath, string minecraftManifest, string gameDirectory, string gameVersion, bool versionSpecific)
    {
        _gameDirectory = gameDirectory;
        _version = gameVersion;
        _versionSpecific = versionSpecific;

        var optionsJson = File.ReadAllText(optionsJsonPath);
        _options = JsonSerializer.Deserialize(optionsJson, OptionsJsonContext.Default.ListMinecraftOption)
            ?? new List<MinecraftOption>();

        var descJson = File.ReadAllText(descriptionsJsonPath);
        _descriptions = JsonSerializer.Deserialize(descJson, OptionsJsonContext.Default.DictionaryStringDictionaryStringString)
            ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        _versions = ParseVersionManifest(minecraftManifest);
    }

    #region 版本清单解析

    private static List<GameVersion> ParseVersionManifest(string manifestJson)
    {
        using var document = JsonDocument.Parse(manifestJson);
        var versions = new List<GameVersion>();

        if (!document.RootElement.TryGetProperty("versions", out var versionsElement))
        {
            return versions;
        }

        foreach (var version in versionsElement.EnumerateArray())
        {
            var id = version.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
            var type = version.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? string.Empty : string.Empty;
            var releaseTimeStr = version.TryGetProperty("releaseTime", out var rtElement) ? rtElement.GetString() : null;

            DateTime releaseTime = DateTime.MinValue;
            if (!string.IsNullOrEmpty(releaseTimeStr))
            {
                DateTime.TryParse(releaseTimeStr, out releaseTime);
            }

            versions.Add(new GameVersion
            {
                Version = id,
                ReleaseType = type,
                ReleaseDate = releaseTime
            });
        }

        return versions;
    }

    #endregion

    #region 选项定义查询

    public List<OptionDefinition> GetDefinitions()
    {
        return _options.Select(ToDefinition).ToList();
    }

    public OptionDefinition? GetDefinition(string name)
    {
        var option = FindOption(name);
        return option == null ? null : ToDefinition(option);
    }

    public string GetDescription(string name, string language)
    {
        if (_descriptions.TryGetValue(language, out var languageDescriptions)
            && languageDescriptions.TryGetValue(name, out var description)
            && !string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        if (_descriptions.TryGetValue(FallbackLanguage, out var fallbackDescriptions)
            && fallbackDescriptions.TryGetValue(name, out var fallbackDescription)
            && !string.IsNullOrWhiteSpace(fallbackDescription))
        {
            return fallbackDescription;
        }

        return DefaultDescription;
    }

    public string GetDescription(string name)
    {
        return GetDescription(name, FallbackLanguage);
    }

    public List<MinecraftOption> GetOptions()
    {
        return _options.Where(option => IsOptionAvailableInVersion(option.Name)).ToList();
    }

    #endregion

    #region 选项值读取与写入

    public string GetCurrentValue(string name)
    {
        var config = Load();
        return config.Values.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public string GetOption(string optionName)
    {
        return GetCurrentValue(optionName);
    }

    public List<GameOption> GetCurrentOptions()
    {
        return Load().Values.Select(kv => new GameOption
        {
            OptionName = kv.Key,
            OptionValue = kv.Value
        }).ToList();
    }

    public List<GameOption> GetAllOptions()
    {
        return Load().Values.Select(kv => new GameOption
        {
            OptionName = kv.Key,
            OptionValue = kv.Value
        }).ToList();
    }

    public List<OptionViewItem> GetOptionViewItems(string language)
    {
        var config = Load();

        return _options.Select(option =>
        {
            var definition = ToDefinition(option, config.Values, language);
            return new OptionViewItem
            {
                Name = definition.Name,
                DefaultValue = definition.DefaultValue,
                CurrentValue = definition.CurrentValue,
                Description = definition.Description,
                ValidValuesRaw = definition.ValidValuesRaw,
                IntroducedVersion = definition.IntroducedVersion,
                IsAvailableInCurrentVersion = definition.IsAvailableInCurrentVersion,
                ValueKind = definition.ValueKind
            };
        }).ToList();
    }

    public void SetOption(string name, string value)
    {
        if (!IsOptionAvailableInVersion(name))
        {
            throw new InvalidOperationException($"Option '{name}' is not available in version '{_version}'.");
        }

        var config = Load();
        var dict = new Dictionary<string, string>(config.Values, StringComparer.Ordinal);
        dict[name] = value;
        Save(dict);
    }

    public void SetOption(GameOption option)
    {
        SetOption(option.OptionName, option.OptionValue);
    }

    public GameOptionsSnapshot Load()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var optionFilePath = GetOptionFilePath();
        if (!File.Exists(optionFilePath))
        {
            return new GameOptionsSnapshot(dict);
        }

        foreach (var line in File.ReadAllLines(optionFilePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')
                || !line.Contains('=', StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                dict[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return new GameOptionsSnapshot(dict);
    }

    #endregion

    #region 内部方法

    private MinecraftOption? FindOption(string name)
    {
        return _options.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.Ordinal));
    }

    private OptionDefinition ToDefinition(MinecraftOption option)
    {
        var config = Load();
        return ToDefinition(option, config.Values, FallbackLanguage);
    }

    private OptionDefinition ToDefinition(MinecraftOption option, IReadOnlyDictionary<string, string> config)
    {
        return ToDefinition(option, config, FallbackLanguage);
    }

    private OptionDefinition ToDefinition(MinecraftOption option, IReadOnlyDictionary<string, string> config, string language)
    {
        var currentValue = config.TryGetValue(option.Name, out var value) ? value : option.DefaultValue;

        return new OptionDefinition
        {
            Name = option.Name,
            DefaultValue = option.DefaultValue,
            CurrentValue = currentValue,
            Description = GetDescription(option.Name, language),
            ValidValuesRaw = option.ValidValues,
            IntroducedVersion = option.IntroducedVersion,
            IsAvailableInCurrentVersion = IsOptionAvailableInVersion(option.Name),
            ValueKind = InferValueKind(option.ValidValues)
        };
    }

    private bool IsOptionAvailableInVersion(string optionName)
    {
        var option = FindOption(optionName);
        if (option == null || string.IsNullOrWhiteSpace(option.IntroducedVersion))
        {
            return option != null;
        }

        var introducedVersion = _versions.FirstOrDefault(v => string.Equals(v.Version, option.IntroducedVersion, StringComparison.Ordinal));
        var currentVersion = _versions.FirstOrDefault(v => string.Equals(v.Version, _version, StringComparison.Ordinal));

        if (string.IsNullOrEmpty(introducedVersion.Version) || string.IsNullOrEmpty(currentVersion.Version))
        {
            return true;
        }

        return currentVersion.ReleaseDate >= introducedVersion.ReleaseDate;
    }

    private static string InferValueKind(string validValues)
    {
        if (string.Equals(validValues, "true,false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(validValues, "false,true", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        if (RangePattern().IsMatch(validValues))
        {
            return "Range";
        }

        if (validValues.Contains(','))
        {
            return "Enum";
        }

        return "Text";
    }

    private string GetOptionFilePath()
    {
        if (_versionSpecific)
        {
            return Path.Combine(_gameDirectory, "versions", _version, "options.txt");
        }

        return Path.Combine(_gameDirectory, "options.txt");
    }

    private void Save(Dictionary<string, string> config)
    {
        using var writer = new StreamWriter(GetOptionFilePath());
        foreach (var kv in config)
        {
            writer.WriteLine($"{kv.Key}={kv.Value}");
        }
    }

    [GeneratedRegex(@"^\s*-?\d+(?:\.\d+)?\s*[-–]\s*-?\d+(?:\.\d+)?\s*$")]
    private static partial Regex RangePattern();

    #endregion
}
