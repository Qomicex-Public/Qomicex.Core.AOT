using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Services;
using Qomicex.Core.AOT.Utils;

namespace Qomicex.Core.AOT.Tests.UnitTests;

public class VersionLocatorMissFilesTests : IDisposable
{
    private readonly string _gameDir;

    public VersionLocatorMissFilesTests()
    {
        _gameDir = Path.Combine(Path.GetTempPath(), "QomicexMissFilesTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_gameDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_gameDir, true); } catch { }
    }

    private DefaultVersionLocator CreateLocator(DownloadMirror mirror = DownloadMirror.Official)
        => new(_gameDir, mirror);

    private void WriteVersionJson(string versionId, string json)
    {
        var dir = Path.Combine(_gameDir, "versions", versionId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{versionId}.json"), json);
    }

    private string WriteLibraryFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_gameDir, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private static string Sha1Of(string content)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(content))).ToLower();

    [Fact]
    public void IsRulesSuitable_AllowAll_ReturnsTrue()
    {
        var rules = new List<Rule> { new("allow", null, null) };
        LibHelper.IsRulesSuitable(rules).Should().BeTrue();
    }

    [Fact]
    public void IsRulesSuitable_AllowAllThenDisallowCurrentOs_ReturnsFalse()
    {
        var rules = new List<Rule>
        {
            new("allow", null, null),
            new("disallow", new OsRequirement(SystemHelper.GetCurrentOsName(), null, null), null)
        };
        LibHelper.IsRulesSuitable(rules).Should().BeFalse();
    }

    [Fact]
    public void IsRulesSuitable_AllowOtherOsOnly_ReturnsFalse()
    {
        var otherOs = SystemHelper.GetCurrentOsName() == "windows" ? "linux" : "windows";
        var rules = new List<Rule> { new("allow", new OsRequirement(otherOs, null, null), null) };
        LibHelper.IsRulesSuitable(rules).Should().BeFalse();
    }

    [Fact]
    public void CheckLibsVer_SameArtifactDifferentVersions_KeepsNewest()
    {
        var libs = new List<Library>
        {
            new("com.example:shared:1.0", null!, null, null, null),
            new("com.example:shared:2.0", null!, null, null, null)
        };
        var result = LibHelper.CheckLibsVer(libs);
        result.Should().ContainSingle();
        result[0].Name.Should().Be("com.example:shared:2.0");
    }

    [Fact]
    public async Task GetMissLibrariesAsync_MissingLibrary_IsReported()
    {
        var json = """
        {
          "id": "test",
          "libraries": [
            {
              "name": "com.example:lib:1.0",
              "downloads": {
                "artifact": {
                  "path": "com/example/lib/1.0/lib-1.0.jar",
                  "url": "https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar",
                  "sha1": "0000000000000000000000000000000000000000",
                  "size": 10
                }
              }
            }
          ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(json);

        miss.Should().ContainSingle();
        miss[0].Name.Should().Be("com.example:lib:1.0");
        miss[0].Url.Should().Be("https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar");
        miss[0].Sha1.Should().Be("0000000000000000000000000000000000000000");
        miss[0].Path.Should().Be(Path.Combine(_gameDir, "libraries", "com", "example", "lib", "1.0", "lib-1.0.jar"));
    }

    [Fact]
    public async Task GetMissLibrariesAsync_ExistingWithCorrectSha1_NotReported()
    {
        var content = "library-bytes";
        WriteLibraryFile("com/example/lib/1.0/lib-1.0.jar", content);
        var json = $$"""
        {
          "id": "test",
          "libraries": [
            {
              "name": "com.example:lib:1.0",
              "downloads": {
                "artifact": {
                  "path": "com/example/lib/1.0/lib-1.0.jar",
                  "url": "https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar",
                  "sha1": "{{Sha1Of(content)}}",
                  "size": 10
                }
              }
            }
          ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(json);

        miss.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMissLibrariesAsync_ExistingWithWrongSha1_IsReported()
    {
        WriteLibraryFile("com/example/lib/1.0/lib-1.0.jar", "corrupted-bytes");
        var json = """
        {
          "id": "test",
          "libraries": [
            {
              "name": "com.example:lib:1.0",
              "downloads": {
                "artifact": {
                  "path": "com/example/lib/1.0/lib-1.0.jar",
                  "url": "https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar",
                  "sha1": "0000000000000000000000000000000000000000",
                  "size": 10
                }
              }
            }
          ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(json);

        miss.Should().ContainSingle();
    }

    [Fact]
    public async Task GetMissLibrariesAsync_NameOnlyLibrary_UsesMavenPathAndLibrariesSource()
    {
        var json = """
        {
          "id": "test",
          "libraries": [
            { "name": "com.example:nolib:2.0" }
          ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(json);

        miss.Should().ContainSingle();
        miss[0].Url.Should().Be("https://libraries.minecraft.net/com/example/nolib/2.0/nolib-2.0.jar");
        miss[0].Sha1.Should().BeEmpty();
        miss[0].Path.Should().Be(Path.Combine(_gameDir, "libraries", "com", "example", "nolib", "2.0", "nolib-2.0.jar"));
    }

    [Fact]
    public async Task GetMissLibrariesAsync_RuleNotSuitable_Excluded()
    {
        var otherOs = SystemHelper.GetCurrentOsName() == "windows" ? "linux" : "windows";
        var json = $$"""
        {
          "id": "test",
          "libraries": [
            {
              "name": "com.example:oslib:1.0",
              "rules": [ { "action": "allow", "os": { "name": "{{otherOs}}" } } ]
            }
          ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(json);

        miss.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMissLibrariesAsync_InheritsFrom_MergesAndDeduplicates()
    {
        WriteVersionJson("parent", """
        {
          "id": "parent",
          "libraries": [
            { "name": "com.example:shared:1.0" },
            { "name": "com.example:parentonly:1.0" }
          ]
        }
        """);
        var childJson = """
        {
          "id": "child",
          "inheritsFrom": "parent",
          "libraries": [
            { "name": "com.example:shared:2.0" }
          ]
        }
        """;
        WriteVersionJson("child", childJson);
        var locator = CreateLocator();

        var miss = await locator.GetMissLibrariesAsync(childJson);

        miss.Should().HaveCount(2);
        miss.Should().Contain(m => m.Name == "com.example:shared:2.0");
        miss.Should().Contain(m => m.Name == "com.example:parentonly:1.0");
        miss.Should().NotContain(m => m.Name == "com.example:shared:1.0");
    }

    [Fact]
    public async Task GetMissMainJarAsync_JarMissing_Reported()
    {
        var json = """
        {
          "id": "test",
          "downloads": { "client": { "url": "https://piston-data.mojang.com/v1/objects/abc/client.jar", "sha1": "0000000000000000000000000000000000000000", "size": 1 } }
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissMainJarAsync(json);

        miss.Should().NotBeNull();
        miss!.Name.Should().Be("test.jar");
        miss.Url.Should().Be("https://piston-data.mojang.com/v1/objects/abc/client.jar");
        miss.Sha1.Should().Be("0000000000000000000000000000000000000000");
        miss.Path.Should().Be(Path.Combine(_gameDir, "versions", "test", "test.jar"));
    }

    [Fact]
    public async Task GetMissMainJarAsync_JarValid_ReturnsNull()
    {
        var content = "jar-bytes";
        var jarDir = Path.Combine(_gameDir, "versions", "test");
        Directory.CreateDirectory(jarDir);
        File.WriteAllText(Path.Combine(jarDir, "test.jar"), content);
        var json = $$"""
        {
          "id": "test",
          "downloads": { "client": { "url": "https://x/client.jar", "sha1": "{{Sha1Of(content)}}", "size": 1 } }
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissMainJarAsync(json);

        miss.Should().BeNull();
    }

    [Fact]
    public async Task GetMissMainJarAsync_Sha1Mismatch_Reported()
    {
        var jarDir = Path.Combine(_gameDir, "versions", "test");
        Directory.CreateDirectory(jarDir);
        File.WriteAllText(Path.Combine(jarDir, "test.jar"), "wrong-bytes");
        var json = """
        {
          "id": "test",
          "downloads": { "client": { "url": "https://x/client.jar", "sha1": "0000000000000000000000000000000000000000", "size": 1 } }
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissMainJarAsync(json);

        miss.Should().NotBeNull();
        miss!.Sha1.Should().Be("0000000000000000000000000000000000000000");
    }

    [Fact]
    public async Task GetMissMainJarAsync_InheritsFrom_ChecksParentJar()
    {
        WriteVersionJson("parent", """
        {
          "id": "parent",
          "downloads": { "client": { "url": "https://x/client.jar", "sha1": "0000000000000000000000000000000000000000", "size": 1 } }
        }
        """);
        var childJson = """
        { "id": "child", "inheritsFrom": "parent" }
        """;
        WriteVersionJson("child", childJson);
        var locator = CreateLocator();

        var miss = await locator.GetMissMainJarAsync(childJson);

        miss.Should().NotBeNull();
        miss!.Name.Should().Be("parent.jar");
        miss.Path.Should().Be(Path.Combine(_gameDir, "versions", "parent", "parent.jar"));
    }

    [Fact]
    public async Task GetMissAssetsAsync_LocalIndex_EnumeratesMissingObjects()
    {
        var indexContent = """
        { "objects": { "icons/icon_16x16.png": { "hash": "bdf48ef6b5d0d23bbb02e17d04865216179f510a", "size": 3665 } } }
        """;
        var indexDir = Path.Combine(_gameDir, "assets", "indexes");
        Directory.CreateDirectory(indexDir);
        File.WriteAllText(Path.Combine(indexDir, "17.json"), indexContent);
        var json = $$"""
        {
          "id": "test",
          "assetIndex": { "id": "17", "sha1": "{{Sha1Of(indexContent)}}", "size": 1, "totalSize": 1, "url": "https://piston-meta.mojang.com/x/17.json" }
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissAssetsAsync(json);

        miss.Should().ContainSingle();
        miss[0].Name.Should().Be("bdf48ef6b5d0d23bbb02e17d04865216179f510a");
        miss[0].Url.Should().Be("https://resources.download.minecraft.net/bd/bdf48ef6b5d0d23bbb02e17d04865216179f510a");
        miss[0].Sha1.Should().Be("bdf48ef6b5d0d23bbb02e17d04865216179f510a");
        miss[0].Path.Should().Be(Path.Combine(_gameDir, "assets", "objects", "bd", "bdf48ef6b5d0d23bbb02e17d04865216179f510a"));
    }

    [Fact]
    public async Task GetMissAssetsAsync_ObjectValid_NotReported()
    {
        var assetContent = "asset-bytes";
        var hash = Sha1Of(assetContent);
        var objDir = Path.Combine(_gameDir, "assets", "objects", hash[..2]);
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, hash), assetContent);
        var indexContent = $$"""
        { "objects": { "a.png": { "hash": "{{hash}}", "size": 1 } } }
        """;
        var indexDir = Path.Combine(_gameDir, "assets", "indexes");
        Directory.CreateDirectory(indexDir);
        File.WriteAllText(Path.Combine(indexDir, "17.json"), indexContent);
        var json = $$"""
        {
          "id": "test",
          "assetIndex": { "id": "17", "sha1": "{{Sha1Of(indexContent)}}", "size": 1, "totalSize": 1, "url": "https://piston-meta.mojang.com/x/17.json" }
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissAssetsAsync(json);

        miss.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMissAssetsAsync_NoAssetIndexNoInherits_ReturnsEmpty()
    {
        var json = """
        { "id": "test" }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissAssetsAsync(json);

        miss.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMissFilesAsync_CombinesLibrariesAndMainJar()
    {
        var json = """
        {
          "id": "test",
          "downloads": { "client": { "url": "https://x/client.jar", "sha1": "0000000000000000000000000000000000000000", "size": 1 } },
          "libraries": [ { "name": "com.example:lib:1.0" } ]
        }
        """;
        var locator = CreateLocator();

        var miss = await locator.GetMissFilesAsync(json);

        miss.Should().HaveCount(2);
        miss.Should().Contain(m => m.Name == "com.example:lib:1.0");
        miss.Should().Contain(m => m.Name == "test.jar");
    }

    [Fact]
    public async Task GetMissFilesAsync_InvalidJson_ThrowsArgumentException()
    {
        var locator = CreateLocator();

        var act = () => locator.GetMissFilesAsync("not json");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetMissLibrariesAsync_BmclapiMirror_ReplacesUrl()
    {
        var json = """
        {
          "id": "test",
          "libraries": [
            {
              "name": "com.example:lib:1.0",
              "downloads": {
                "artifact": {
                  "path": "com/example/lib/1.0/lib-1.0.jar",
                  "url": "https://libraries.minecraft.net/com/example/lib/1.0/lib-1.0.jar",
                  "sha1": "0000000000000000000000000000000000000000",
                  "size": 10
                }
              }
            }
          ]
        }
        """;
        var locator = CreateLocator(DownloadMirror.BMCLAPI);

        var miss = await locator.GetMissLibrariesAsync(json);

        miss[0].Url.Should().Be("https://bmclapi2.bangbang93.com/maven/com/example/lib/1.0/lib-1.0.jar");
    }

    [Fact]
    public async Task GetMissAssetsAsync_BmclapiMirror_UsesBmclapiAssetsSource()
    {
        var indexContent = """
        { "objects": { "a.png": { "hash": "bdf48ef6b5d0d23bbb02e17d04865216179f510a", "size": 1 } } }
        """;
        var indexDir = Path.Combine(_gameDir, "assets", "indexes");
        Directory.CreateDirectory(indexDir);
        File.WriteAllText(Path.Combine(indexDir, "17.json"), indexContent);
        var json = $$"""
        {
          "id": "test",
          "assetIndex": { "id": "17", "sha1": "{{Sha1Of(indexContent)}}", "size": 1, "totalSize": 1, "url": "https://piston-meta.mojang.com/x/17.json" }
        }
        """;
        var locator = CreateLocator(DownloadMirror.BMCLAPI);

        var miss = await locator.GetMissAssetsAsync(json);

        miss[0].Url.Should().Be("https://bmclapi2.bangbang93.com/assets/bd/bdf48ef6b5d0d23bbb02e17d04865216179f510a");
    }
}
