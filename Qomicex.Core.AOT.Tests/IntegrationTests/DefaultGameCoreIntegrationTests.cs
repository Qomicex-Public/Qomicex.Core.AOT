using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests.IntegrationTests;

public class DefaultGameCoreIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly DefaultGameCore _core;
    private readonly string _gameRoot;
    private bool _disposed;

    public DefaultGameCoreIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _gameRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft_test",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(_gameRoot);

        _core = new GameCoreBuilder()
            .UseGameRoot(_gameRoot)
            .Build();
        Log("集成测试初始化完成");
    }

    [Fact]
    public async Task GetAvailableVersionsAsync_ShouldReturnRealData()
    {
        var versions = await _core.Version.GetAvailableVersionsAsync(forceRefresh: true);

        versions.Should().NotBeNull();
        versions.Count.Should().BeGreaterThan(100);
        Log($"获取到 {versions.Count} 个版本");
    }

    [Fact]
    public async Task GetLatestVersionsAsync_ShouldReturnRealData()
    {
        var latest = await _core.Version.GetLatestVersionsAsync(forceRefresh: true);

        latest.Should().NotBeNull();
        latest.Release.Should().NotBeNullOrEmpty();
        latest.Snapshot.Should().NotBeNullOrEmpty();
        Log($"最新正式版: {latest.Release}, 快照版: {latest.Snapshot}");
    }

    [Fact]
    public async Task GetVersionMetadataAsync_ShouldReturnRealData()
    {
        var metadata = await _core.Version.GetVersionMetadataAsync("1.20.1");

        metadata.Should().NotBeNull();
        metadata.Id.Should().Be("1.20.1");
        metadata.Libraries.Should().NotBeEmpty();
        Log($"库文件数: {metadata.Libraries.Count}");
    }

    private void Log(string msg) => _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

    public void Dispose()
    {
        if (_disposed) return;
        _core?.Dispose();
        if (Directory.Exists(_gameRoot))
            try { Directory.Delete(_gameRoot, true); } catch { }
        _disposed = true;
    }
}
