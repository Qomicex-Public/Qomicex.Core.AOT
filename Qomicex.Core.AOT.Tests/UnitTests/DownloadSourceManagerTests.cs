using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Models;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Services;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests.UnitTests;

public class DownloadSourceManagerTests
{
    private readonly ITestOutputHelper _output;

    public DownloadSourceManagerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DefaultConstructor_ShouldHaveBothOfficialAndBMCLAPI()
    {
        var manager = new DefaultDownloadSourceManager();
        var sources = manager.GetAvailableSources(ResourceType.Library);

        sources.Should().HaveCount(2);
        sources.Should().Contain(s => s.Type == DownloadSourceType.Official);
        sources.Should().Contain(s => s.Type == DownloadSourceType.BMCLAPI);
    }

    [Fact]
    public void DefaultConstructor_BMCLAPIShouldBePreferred()
    {
        var manager = new DefaultDownloadSourceManager();
        var preferred = manager.GetPreferredSource(ResourceType.Library);

        preferred.Should().NotBeNull();
        preferred!.Type.Should().Be(DownloadSourceType.BMCLAPI);
    }

    [Fact]
    public void OfficialMirror_OfficialShouldBePreferred()
    {
        var manager = new DefaultDownloadSourceManager(DownloadMirror.Official);
        var preferred = manager.GetPreferredSource(ResourceType.Library);

        preferred.Should().NotBeNull();
        preferred!.Type.Should().Be(DownloadSourceType.Official);
    }

    [Fact]
    public void GenerateMirrorUrls_OfficialUrl_ShouldReturnOriginalFirst()
    {
        var manager = new DefaultDownloadSourceManager();
        var url = "https://launcher.mojang.com/maven/net/minecraft/1.20.1.jar";

        var urls = manager.GenerateMirrorUrls(url, ResourceType.Library).ToList();

        urls[0].Should().Be(url);
    }

    [Fact]
    public void GenerateMirrorUrls_Maven_ShouldHaveBMCLAPIMirror()
    {
        var manager = new DefaultDownloadSourceManager();
        var url = "https://launcher.mojang.com/maven/net/minecraft/1.20.1.jar";

        var urls = manager.GenerateMirrorUrls(url, ResourceType.Library).ToList();

        urls.Should().Contain(u => u.StartsWith("https://bmclapi2.bangbang93.com/maven/"));
    }

    [Fact]
    public void GenerateMirrorUrls_Assets_ShouldHaveBMCLAPIMirror()
    {
        var manager = new DefaultDownloadSourceManager();
        var url = "https://resources.download.minecraft.net/ab/abc123";

        var urls = manager.GenerateMirrorUrls(url, ResourceType.Asset).ToList();

        urls.Should().Contain(u => u.StartsWith("https://bmclapi2.bangbang93.com/assets/"));
    }

    [Fact]
    public void GenerateMirrorUrls_Meta_ShouldHaveBMCLAPIMirror()
    {
        var manager = new DefaultDownloadSourceManager();
        var url = "https://piston-meta.mojang.com/v1/packages/abc123/1.20.1.json";

        var urls = manager.GenerateMirrorUrls(url, ResourceType.AssetIndex).ToList();

        urls.Should().Contain(u => u.StartsWith("https://bmclapi2.bangbang93.com/meta/"));
    }

    [Fact]
    public void GenerateMirrorUrls_UnknownDomain_ShouldOnlyReturnOriginal()
    {
        var manager = new DefaultDownloadSourceManager();
        var url = "https://example.com/random/file.jar";

        var urls = manager.GenerateMirrorUrls(url, ResourceType.Library).ToList();

        urls.Should().HaveCount(1);
        urls[0].Should().Be(url);
    }

    [Fact]
    public void GetAvailableSources_ShouldNotIncludeDisabledSources()
    {
        var manager = new DefaultDownloadSourceManager();

        manager.GetAvailableSources(ResourceType.Library).Should().AllSatisfy(
            s => s.IsEnabled.Should().BeTrue()
        );
    }

    [Fact]
    public void AddCustomSource_ShouldAddNewSource()
    {
        var manager = new DefaultDownloadSourceManager();
        var custom = new DownloadSource(DownloadSourceType.Custom, "Test", "https://test.com/", true, 50);

        manager.AddCustomSource(custom);
        var sources = manager.GetAvailableSources(ResourceType.Library);

        sources.Should().Contain(s => s.Type == DownloadSourceType.Custom);
    }

    [Fact]
    public void AddCustomSource_DuplicateType_ShouldThrow()
    {
        var manager = new DefaultDownloadSourceManager();
        var dup = new DownloadSource(DownloadSourceType.Official, "Duplicate", "https://dup.com/", true, 10);

        manager.Invoking(m => m.AddCustomSource(dup))
            .Should().Throw<InvalidOperationException>();
    }
}
