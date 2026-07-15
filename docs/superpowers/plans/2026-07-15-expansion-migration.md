# Expansion 迁移实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将 Qomicex.Core 的第三方资源获取功能完整迁移到 Qomicex.Core.AOT，保持 AOT 兼容

**Architecture:** record DTO + 各平台独立 JsonSerializerContext + 共享 HttpClient 构造注入 + 接口抽象

**Tech Stack:** .NET 10 AOT, System.Text.Json source generators, Tomlyn 0.17.0

## Global Constraints

- `PublishAot=true`, `IsAotCompatible=true`
- 所有 JSON 反序列化必须通过源生成上下文（`JsonSerializerContext`）
- DTO 使用位置 record + `[property: JsonPropertyName]`
- 服务实现 `internal sealed class`
- 接口放在 `Public/`，模型在 `Models/`，服务在 `Services/Expansion/{Platform}/`
- 零外部依赖（除 Tomlyn 0.17.0）
- HttpClient 通过构造函数注入

---

## Phase 1: 基础基础设施

### Task P1-1: Tomlyn 依赖 + 目录结构

**Files:**
- Modify: `Qomicex.Core.AOT/Qomicex.Core.AOT.csproj`
- Create: `Public/Expansion/` (目录)
- Create: `Models/Expansion/Modrinth/`, `Models/Expansion/CurseForge/`, `Models/Expansion/FeedTheBeast/`, `Models/Expansion/Local/`
- Create: `Services/Expansion/Modrinth/`, `Services/Expansion/CurseForge/`, `Services/Expansion/FeedTheBeast/`, `Services/Expansion/Local/`
- Create: `Services/Installers/`, `Services/Installers/Modpacks/`

**Steps:**
1. 在 `.csproj` 添加 Tomlyn 包引用
2. 创建上述目录
3. `dotnet build` 验证通过

### Task P1-2: JsonHelper 扩展

**Files:**
- Modify: `Utils/JsonHelper.cs`

新增：
```csharp
public static T? ToObject<T>(this JsonNode node, JsonTypeInfo<T> typeInfo)
    => JsonSerializer.Deserialize(node.ToJsonString(), typeInfo);
```

---

## Phase 2: Modrinth 模块

### Task M-1: Modrinth DTO 模型 (8 个文件)

**Files:** Create all under `Models/Expansion/Modrinth/`

1. **SearchResult.cs** — `SearchResult`, `SearchResultInfo`
2. **ProjectInfo.cs** — `ProjectInfo`, `GalleryItem`
3. **VersionInfo.cs** — `VersionInfo`, `VersionFileInfo`, `FileHashes`, `DependenciesInfo`
4. **ModrinthVersionInfo.cs** — `ModrinthVersionInfo`, `ModrinthFile`, `ModrinthVersionResponse : Dictionary<string, ModrinthVersionInfo>`
5. **ProjectVersionInfo.cs** — `ProjectVersionInfo`
6. **ModrinthTag.cs** — `ModrinthTag`
7. **ModLoaderType.cs** — 枚举 (`minecraft, forge, fabric, quilt, neoForge, rift, liteLoader, modLoader, nilloader, ornithe`)
8. **StaticIndex.cs** — `Index` 常量类、`ProjectType` 常量类、`SupportType` 常量类

全部用位置 `record` + `[property: JsonPropertyName("...")]`。枚举用 `public enum ModLoaderType { ... }`（不需要 `[JsonConverter]`，由 `UseStringEnumConverter` 处理）。

### Task M-2: ModrinthJsonContext

**Files:**
- Create: `JsonContext/ModrinthJsonContext.cs`

注册所有 M-1 的类型 + `Dictionary<string, ModrinthVersionInfo>` + `List<ModrinthTag>` + `List<string>`。

### Task M-3: IModrinthSource 接口

**Files:**
- Create: `Public/Expansion/IModrinthSource.cs`

```csharp
namespace Qomicex.Core.AOT.Public.Expansion;

public interface IModrinthSource
{
    Task<SearchResult> SearchAsync(string query, string? projectType, string? gameVersion,
        string[]? categories, string[]? loaders, string index, int page, int pageSize);
    Task<ProjectInfo> GetProjectInfoAsync(string projectId);
    Task<List<ProjectVersionInfo>> GetProjectVersionInfoAsync(string projectId);
    Task<VersionInfo> GetVersionInfoAsync(string versionId);
    Task<List<ProjectVersionInfo>> GetProjectVersionsFromHashesAsync(List<string> hashes);
    Task<Dictionary<string, ProjectVersionInfo>> GetProjectVersionsFromHashesDictAsync(List<string> hashes);
    Task<List<ModrinthTag>> GetCategoriesAsync();
    Task<List<ModrinthTag>> GetLoadersAsync();
    Task<List<ModrinthTag>> GetProjectTypesAsync();
}
```

### Task M-4: ModrinthBase 实现

**Files:**
- Create: `Services/Expansion/Modrinth/ModrinthBase.cs`

```csharp
namespace Qomicex.Core.AOT.Services.Expansion.Modrinth;

using static Qomicex.Core.AOT.Models.Expansion.Modrinth.StaticIndex;

internal sealed class ModrinthBase(HttpClient http) : IModrinthSource
{
    private const string BaseUrl = "https://api.modrinth.com/";
    // User-Agent 设置
    // 所有接口实现
    // 内部 GetDataAsync / PostDataAsync
}
```

关键转换：
- 移除 `new HttpClient()` → 使用构造注入 `http`
- `JsonSerializer.Deserialize<T>(response)` → `JsonSerializer.Deserialize(response, ctx.Default.Type)`
- `ModrinthVersionResponse : Dictionary<string, ModrinthVersionInfo>` 作为 record 声明
- `ModLoaderType` 枚举不需要 `[JsonConverter]`

### Task M-5: Modrinth 资源类型 (6 个文件)

**Files:** Create under `Services/Expansion/Modrinth/`:
- `Mods.cs` — `ProjectType = "mod"`
- `Modpacks.cs` — `ProjectType = "modpack"`
- `ResourcePacks.cs` — `ProjectType = "resourcepack"`
- `DataPacks.cs` — `ProjectType = "datapack"`
- `Shaders.cs` — `ProjectType = "shader"`
- `Worlds.cs` — `ProjectType = "mod"`

```csharp
internal sealed class Mods(HttpClient http) : ModrinthBase(http)
{
    public new const string ProjectType = "mod";
}
```

### Task M-6: Modrinth 单元测试

**Files:**
- Create: `Qomicex.Core.AOT.Tests/UnitTests/Expansion/ModrinthTests.cs`

---

## Phase 3: CurseForge 模块

### Task C-1: CurseForge DTO 模型

**Files:** Create under `Models/Expansion/CurseForge/`:
- `CurseForgeSearchResult.cs` — 注意：源项目搜索返回复杂嵌套 JSON，DTO 可以保持 class（手动构建）
- `CurseForgeInfo.cs`, `CurseForgeFileInfo.cs`, `CurseForgeFilesMeta.cs`
- `FingerprintsFilesMeta.cs`, `CurseForgeDependenciesMeta.cs`
- `CategoryMeta.cs`, `AuthorMeta.cs`, `ScreenshotsMeta.cs`
- `SortField.cs` (enum), `CurseForgeDependenciesType.cs` (enum)
- `ModLoaderType.cs` (enum — 替代源项目的静态类)

注意：`CurseForgeInfo.IconUrl` 在源中用 `[JsonIgnore]` + 手动从 `logo.url` 提取 → DTO 中不包含 `IconUrl`

### Task C-2: CurseForgeJsonContext

**Files:**
- Create: `JsonContext/CurseForgeJsonContext.cs`

### Task C-3: ICurseForgeSource 接口

**Files:**
- Create: `Public/Expansion/ICurseForgeSource.cs`

### Task C-4: CurseForgeBase 实现

**Files:**
- Create: `Services/Expansion/CurseForge/CurseForgeBase.cs`

关键转换：
- 构造：`(HttpClient http, string apiKey)`
- 搜索保持 `JsonNode.Parse` + `.ToString()` 手动提取
- `GetInfoAsync` 中的 `modInfo.ToObject<CurseForgeInfo>()` → `JsonSerializer.Deserialize(json, ctx.CurseForgeInfo)`
- `GetFileInfo` 中的 `fileInfo.ToObject<CurseForgeFileInfo>()` → source-gen
- `POSTData` 中的 `JsonSerializer.Serialize(data)` 无 context（发送匿名对象，AOT 兼容）

### Task C-5: CurseForge 资源类型 (6 个文件)

同 M-5 模式。

### Task C-6: CurseForge 单元测试

---

## Phase 4: FTB 模块

### Task F-1: FTB DTO 模型

**Files:** Create under `Models/Expansion/FeedTheBeast/`:
- `ModpackInfo.cs`, `TagInfo.cs`, `VersionInfo.cs`, `SpecsInfo.cs`, `TargetInfo.cs`
- `AuthorInfo.cs`, `LinkInfo.cs`, `ArtInfo.cs`, `MetaInfo.cs`, `RatingInfo.cs`
- `VersionDetail.cs`, `FileInfo.cs`
- `ChangelogResult.cs`, `CacheData.cs`

### Task F-2: FTBJsonContext

**Files:**
- Create: `JsonContext/FTBJsonContext.cs`

### Task F-3: IFTBSource 接口

**Files:**
- Create: `Public/Expansion/IFTBSource.cs`

### Task F-4: FTBBase 实现

**Files:**
- Create: `Services/Expansion/FeedTheBeast/FTBBase.cs`

关键转换：
- `JsonSerializer.Deserialize/Serialize` → source-gen context
- `BASEURL` 和 `_httpClient` 合并：构造注入 `HttpClient`
- `<appdata>/Qomicex/ftb_cache.json` 缓存路径不变

### Task F-5: FTB Modpacks 类型

### Task F-6: FTB 单元测试

---

## Phase 5: 本地资源

### Task L-1: LocalResourceBase + MurmurHash2

**Files:**
- Create: `Services/Expansion/Local/LocalResourceBase.cs`

纯代码迁移（无 AOT 隐患）：`CurseForgeFingerprint`, `MurmurHash2`, `TryReadFileFromZip`

### Task L-2: Local/Mods

**Files:**
- Create: `Services/Expansion/Local/Mods.cs`
- Create: `Models/Expansion/Local/ModInfo.cs`

关键转换：
- `SHA1.Create()` → `SHA1.HashData(byte[])` 静态方法
- Tomlyn 保留
- `new HttpClient()` → 构造注入
- `ModInfo` 提取为独立 record

### Task L-3: 其他本地资源 (DataPacks, Resourcepacks, Shaders, Saves, Screenshots)

纯目录枚举，无 JSON 反序列化。

---

## Phase 6: 安装器

### Task I-1: IInstaller + InstallerBase

**Files:**
- Create: `Services/Installers/IInstaller.cs`
- Create: `Services/Installers/InstallerBase.cs`

`DownloadFileAsync` 签名改为 `(HttpClient client, string url, string destinationPath, int maxRedirects = 5)`。
`MergeJson` 保持 `JsonNode` 操作。
`MavenToPath`, `GetJarMainClass`, `RunInstallProcess` 保持原样。

### Task I-2 ~ I-7: 各加载器安装器

`FabricInstaller`, `ForgeInstaller`, `ForgeInstallerBase`, `NeoForgeInstaller`, `QuiltInstaller`, `LiteLoaderInstaller`, `OptiFineInstaller`

- 接收 `HttpClient` 注入
- 无 JSON 反序列化（大多解析 HTML/字符串）

### Task I-8: 整合包安装器 (Modpacks/Modrinth.cs, Modpacks/CurseForge.cs)

- 保留 `JsonNode.Parse` 手动导航
- 纯 zip 解压 + 文件复制

---

## Phase 7: Builder 集成 + 验证

### Task B-1: CoreOptions + GameCoreBuilder

**Files:**
- Modify: `Builder/CoreOptions.cs` — 加 `CurseForgeApiKey`
- Modify: `Builder/GameCoreBuilder.cs`

### Task B-2: 全量构建 + AOT 警告检查

```bash
dotnet build Qomicex.Core.AOT/Qomicex.Core.AOT.csproj
```
检查无 IL3050/IL2026 等 AOT 警告。

### Task B-3: 测试运行

```bash
dotnet test Qomicex.Core.AOT.Tests/Qomicex.Core.AOT.Tests.csproj
```
