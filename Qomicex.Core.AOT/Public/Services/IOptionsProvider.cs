using Qomicex.Core.AOT.Services.Options;

namespace Qomicex.Core.AOT.Public.Services;

public interface IOptionsProvider
{
    GameOptionsSnapshot Load();
    void SetOption(string name, string value);
    void SetOption(GameOption option);
    string GetCurrentValue(string name);
    string GetOption(string optionName);
    List<OptionDefinition> GetDefinitions();
    OptionDefinition? GetDefinition(string name);
    List<OptionViewItem> GetOptionViewItems(string language);
    List<MinecraftOption> GetOptions();
    List<GameOption> GetCurrentOptions();
    List<GameOption> GetAllOptions();
    string GetDescription(string name);
    string GetDescription(string name, string language);
}
