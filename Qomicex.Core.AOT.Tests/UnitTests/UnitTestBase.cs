using Moq;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Models;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests.UnitTests;

public abstract class UnitTestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;

    protected readonly Mock<IVersionManagementService> MockVersionService;
    protected readonly Mock<IAuthProvider> MockAuthProvider;
    protected readonly Mock<ILaunchExecutor> MockLaunchExecutor;
    protected readonly Mock<IDownloadSourceManager> MockSourceManager;

    protected readonly DefaultGameCore Core;
    protected readonly string TestGameRoot;
    protected readonly HttpClient TestHttpClient;

    private bool _disposed;

    protected UnitTestBase(ITestOutputHelper output)
    {
        Output = output;
        TestGameRoot = Path.Combine(Path.GetTempPath(), "qomicex_ut", Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestGameRoot);

        TestHttpClient = new HttpClient();

        MockVersionService = new Mock<IVersionManagementService>();
        MockAuthProvider = new Mock<IAuthProvider>();
        MockLaunchExecutor = new Mock<ILaunchExecutor>();
        MockSourceManager = new Mock<IDownloadSourceManager>();

        SetupDefaultMocks();

        Core = new GameCoreBuilder()
            .WithVersionService(MockVersionService.Object)
            .WithAuthProvider(MockAuthProvider.Object)
            .WithLaunchExecutor(MockLaunchExecutor.Object)
            .WithDownloadSourceManager(MockSourceManager.Object)
            .WithHttpClient(TestHttpClient)
            .Build();

        Log("UnitTestBase 初始化完成");
    }

    protected virtual void SetupDefaultMocks()
    {
        MockVersionService
            .Setup(s => s.GetAvailableVersionsAsync(It.IsAny<bool>()))
            .ReturnsAsync(new List<ManifestVersionInfo>
            {
                new("1.20.1", "release", "https://example.com/1.20.1.json", DateTime.UtcNow, DateTime.UtcNow),
                new("1.19.4", "release", "https://example.com/1.19.4.json", DateTime.UtcNow, DateTime.UtcNow),
                new("24w14a", "snapshot", "https://example.com/24w14a.json", DateTime.UtcNow, DateTime.UtcNow)
            });

        MockVersionService
            .Setup(s => s.GetLatestVersionsAsync(It.IsAny<bool>()))
            .ReturnsAsync(new LatestVersionInfo("1.20.1", "24w14a"));

        MockVersionService
            .Setup(s => s.IsVersionInstalled(It.IsAny<string>()))
            .Returns(false);

        MockVersionService
            .Setup(s => s.GetInstalledVersions())
            .Returns(new List<LocalVersionInfo>());

        MockVersionService
            .Setup(s => s.GetVersionMetadataAsync(It.IsAny<string>()))
            .ReturnsAsync((string versionId) => CreateTestMetadata(versionId));

        MockVersionService
            .Setup(s => s.InstallVersionAsync(It.IsAny<string>(), It.IsAny<IProgress<DownloadProgress>?>()))
            .Returns(Task.CompletedTask);

        MockVersionService
            .Setup(s => s.UninstallVersionAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        MockAuthProvider
            .Setup(a => a.AuthenticateAsync(It.IsAny<AuthRequest>()))
            .ReturnsAsync(new AuthResult
            {
                Success = true,
                Username = "TestPlayer",
                AccessToken = Guid.NewGuid().ToString(),
                ClientToken = Guid.NewGuid().ToString(),
                Uuid = Guid.NewGuid().ToString()
            });

        MockAuthProvider
            .Setup(a => a.ValidateAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        MockLaunchExecutor
            .Setup(l => l.LaunchAsync(It.IsAny<LaunchOptions>()))
            .ReturnsAsync(new LaunchResult { Success = true, ProcessId = 1234, Message = "Mock 启动成功" });
    }

    protected static CompleteVersionMetadata CreateTestMetadata(string versionId = "1.20.1")
    {
        return new CompleteVersionMetadata(
            Id: versionId,
            Type: "release",
            MainClass: "net.minecraft.client.main.Main",
            InheritsFrom: null,
            Jar: null,
            Arguments: null,
            Libraries: new List<Library>(),
            AssetIndex: new AssetIndex("1.20", "abc123", 1000, 1000000, "https://example.com/index.json"),
            Downloads: new VersionDownloads(
                Client: new Artifact($"{versionId}/{versionId}.jar", $"https://example.com/{versionId}.jar", "def456", 10000000),
                Server: null
            ),
            JavaVersion: new JavaVersion("java-runtime-gamma", 17),
            MinimumLauncherVersion: 21,
            ReleaseTime: DateTime.UtcNow,
            Time: DateTime.UtcNow
        );
    }

    protected void Log(string message)
    {
        Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            Core?.Dispose();
            TestHttpClient?.Dispose();
            if (Directory.Exists(TestGameRoot))
                try { Directory.Delete(TestGameRoot, true); } catch { }
        }
        _disposed = true;
    }
}
