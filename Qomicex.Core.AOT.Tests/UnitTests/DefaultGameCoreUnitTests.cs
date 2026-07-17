using Moq;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.Local;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests.UnitTests;

public class DefaultGameCoreUnitTests : UnitTestBase
{
    public DefaultGameCoreUnitTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Constructor_ShouldInitializeVersionService()
    {
        Core.Version.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailableVersionsAsync_ShouldCallVersionService()
    {
        var versions = await Core.Version.GetAvailableVersionsAsync();

        versions.Should().NotBeNull();
        versions.Should().HaveCount(3);
        versions.Should().Contain(v => v.Id == "1.20.1");

        MockVersionService.Verify(
            s => s.GetAvailableVersionsAsync(It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLatestVersionsAsync_ShouldReturnCorrectData()
    {
        var latest = await Core.Version.GetLatestVersionsAsync();

        latest.Should().NotBeNull();
        latest.Release.Should().Be("1.20.1");
        latest.Snapshot.Should().Be("24w14a");
    }

    [Fact]
    public async Task IsVersionInstalled_ShouldReturnFalse_WhenNotInstalled()
    {
        var result = Core.Version.IsVersionInstalled("1.20.1");

        result.Should().BeFalse();
        MockVersionService.Verify(s => s.IsVersionInstalled("1.20.1"), Times.Once);
    }

    [Fact]
    public void IsVersionInstalled_ShouldReturnTrue_WhenInstalled()
    {
        MockVersionService
            .Setup(s => s.IsVersionInstalled("1.20.1"))
            .Returns(true);

        var result = Core.Version.IsVersionInstalled("1.20.1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InstallVersionAsync_ShouldCallVersionService()
    {
        var called = false;
        MockVersionService
            .Setup(s => s.InstallVersionAsync("1.20.1", It.IsAny<IProgress<DownloadProgress>?>()))
            .Callback(() => called = true)
            .Returns(Task.CompletedTask);

        await Core.Version.InstallVersionAsync("1.20.1");

        called.Should().BeTrue();
        MockVersionService.Verify(
            s => s.InstallVersionAsync("1.20.1", It.IsAny<IProgress<DownloadProgress>?>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallVersionAsync_ShouldCallVersionService()
    {
        var called = false;
        MockVersionService
            .Setup(s => s.UninstallVersionAsync("1.20.1"))
            .Callback(() => called = true)
            .Returns(Task.CompletedTask);

        await Core.Version.UninstallVersionAsync("1.20.1");

        called.Should().BeTrue();
        MockVersionService.Verify(s => s.UninstallVersionAsync("1.20.1"), Times.Once);
    }

    [Fact]
    public void GetInstalledVersions_ShouldReturnList()
    {
        var expected = new List<LocalVersionInfo>
        {
            new("1.20.1", new List<ModloaderInfo> { new(ModloaderType.Vanilla, "") }, DateTime.UtcNow, true, "/path/1.20.1", "1.20.1", 10000000),
            new("1.19.4", new List<ModloaderInfo> { new(ModloaderType.Vanilla, "") }, DateTime.UtcNow, true, "/path/1.19.4", "1.19.4", 8000000)
        };

        MockVersionService
            .Setup(s => s.GetInstalledVersions())
            .Returns(expected);

        var result = Core.Version.GetInstalledVersions();

        result.Should().HaveCount(2);
        result.Should().Contain(v => v.Id == "1.20.1");
    }
}
