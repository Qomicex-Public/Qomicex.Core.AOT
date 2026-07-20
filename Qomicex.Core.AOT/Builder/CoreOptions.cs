namespace Qomicex.Core.AOT.Builder;

public sealed record class CoreOptions
{
    public string LauncherName { get; set; } = "Qomicex.Core.AOT";
    public string GameRoot { get; set; } = ".minecraft";
    public string UserAgent { get; set; } = "Qomicex.Core/1.0 (tmoaminecraft@gmail.com; lenmei233@vip.qq.com)";
    public int MaxConcurrentDownloads { get; set; } = 8;
    public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
    public DownloadMirror DownloadMirror { get; set; } = DownloadMirror.Official;
    public AuthMode AuthMode { get; set; } = AuthMode.Offline;
    public string? MicrosoftClientId { get; set; }
    public string? YggdrasilServerUrl { get; set; }
    public AuthOptions AuthOptions { get; set; } = new();
    public string? OptionsJsonPath { get; set; }
    public string? DescriptionsJsonPath { get; set; }
    public string? MinecraftManifestPath { get; set; }
}

public sealed record class AuthOptions
{
    public AuthMode Mode { get; set; } = AuthMode.Offline;
    public string? Uuid { get; set; }
    public string? Name { get; set; } = "Player";
    public string? Token { get; set; }
    public string? AccessToken { get; set; } = "0";
    public string? RefreshToken { get; set; }
    public string? ServerUrl { get; set; }
    public string? AuthlibInjectorParam { get; set; }
}

public sealed record class JavaOptions
{
    public string JavaPath { get; set; } = "java";
    public int MaxMemoryMB { get; set; } = 512;
    public string[]? ExtraJvmArgs { get; set; }
}

public sealed record class LaunchOptions
{
    public string Version { get; set; } = string.Empty;
    public bool VersionIsolation { get; set; }
    public string? JoinServer { get; set; }
    public string? JoinWorld { get; set; }
    public JavaOptions? JavaOptions { get; set; }
    public AuthOptions? AuthOptions { get; set; }
    public string? GameRoot { get; set; }
}
public enum AuthMode { Offline, Microsoft, Yggdrasil }

public enum DownloadMirror { Official, BMCLAPI }

