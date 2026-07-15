# Expansion 迁移设计文档

> 将 Qomicex.Core 的第三方资源获取功能迁移到 Qomicex.Core.AOT

## 1. 概述

**目标：** 将 `Qomicex.Core` 项目中所有第三方资源获取相关功能（Modrinth、CurseForge、FTB、本地资源、安装器）完整迁移到 `Qomicex.Core.AOT`，保持 AOT 兼容性，统一遵循 AOT 项目的代码规范。

**设计原则：**
- record DTO 优先（位置 record + `[property: JsonPropertyName]`）
- 所有 JSON 反序列化通过源生成上下文（`JsonSerializerContext`）
- 共享 `HttpClient` 构造函数注入
- 接口抽象（为所有平台添加接口）
- 零外部依赖（除 `Tomlyn`）

## 2. 目录结构

```
Public/
  Expansion/
    IModrinthSource.cs       # Modrinth API 接口
    ICurseForgeSource.cs     # CurseForge API 接口
    IFTBSource.cs           # FTB API 接口

Models/
  Expansion/
    Modrinth/                # Modrinth DTO records
      SearchResult.cs
      ProjectInfo.cs
      VersionInfo.cs
      ModrinthVersionInfo.cs
      ProjectVersionInfo.cs
      ModrinthTag.cs
      FileHashes.cs
      GalleryItem.cs
      DependenciesInfo.cs
      ModLoaderType.cs
    CurseForge/              # CurseForge DTO records
      CurseForgeSearchResult.cs
      CurseForgeInfo.cs
      CurseForgeFileInfo.cs
      CurseForgeFilesMeta.cs
      FingerprintsFilesMeta.cs
      CurseForgeDependenciesMeta.cs
      CategoryMeta.cs
      AuthorMeta.cs
      ScreenshotsMeta.cs
      SortField.cs
      CurseForgeDependenciesType.cs
    FeedTheBeast/            # FTB DTO records
      ModpackInfo.cs
      TagInfo.cs
      VersionInfo.cs
      SpecsInfo.cs
      TargetInfo.cs
      AuthorInfo.cs
      LinkInfo.cs
      ArtInfo.cs
      MetaInfo.cs
      RatingInfo.cs
      VersionDetail.cs
      FileInfo.cs
      ChangelogResult.cs
      CacheData.cs
    Local/
      ModInfo.cs

Services/
  Expansion/
    Modrinth/                # Modrinth 服务实现
      ModrinthBase.cs
      Mods.cs / Modpacks.cs / ResourcePacks.cs / DataPacks.cs / Shaders.cs / Worlds.cs
    CurseForge/              # CurseForge 服务实现
      CurseForgeBase.cs
      Mods.cs / Modpacks.cs / ResourcePacks.cs / DataPacks.cs / Shaders.cs / Worlds.cs
    FeedTheBeast/            # FTB 服务实现
      FTBBase.cs
      Modpacks.cs
    Local/                   # 本地资源
      LocalResourceBase.cs
      Mods.cs / DataPacks.cs / Resourcepacks.cs / Shaders.cs / Saves.cs / Screenshots.cs
  Installers/                # 安装器
    IInstaller.cs
    InstallerBase.cs
    FabricInstaller.cs / ForgeInstaller.cs / ForgeInstallerBase.cs
    NeoForgeInstaller.cs / QuiltInstaller.cs / LiteLoaderInstaller.cs / OptiFineInstaller.cs
    Modpacks/
      Modrinth.cs / CurseForge.cs

JsonContext/
  ModrinthJsonContext.cs
  CurseForgeJsonContext.cs
  FTBJsonContext.cs
  LocalResourceJsonContext.cs
```

## 3. 模型设计

所有 DTO 使用位置 `record` + `[property: JsonPropertyName(...)]`：

```csharp
public record SearchResult(
    [property: JsonPropertyName("hits")] List<SearchResultInfo> Results,
    [property: JsonPropertyName("total_hits")] int TotalResults
);
```

- 可空值用 `string?`（而非 `""` 默认值）
- 枚举用 `UseStringEnumConverter = true` 统一处理（无需每个枚举标 `[JsonConverter]`）
- 去掉源项目中的静态常量类（`Index`, `ProjectType`, `SupportType`）

## 4. JSON 源生成

每个平台独立 `internal partial class ...JsonContext : JsonSerializerContext`

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(SearchResult))]
internal partial class ModrinthJsonContext : JsonSerializerContext;
```

泛型类型（如 `Dictionary<string, T>`）也需要显式注册。

## 5. 服务实现

- 所有服务接收 `HttpClient` 构造函数注入
- Modrinth：全程 source-gen DTO 反序列化
- CurseForge：搜索用 `JsonNode` 手动导航，详情/指纹用 source-gen
- FTB：全程 source-gen（含文件缓存）
- 移除 `ToObject<T>()` 反射调用 → `JsonSerializer.Deserialize(json, ctx.Type)`

## 6. HTTP 客户端

GameCoreBuilder 创建共享 `HttpClient`，注入给所有服务。服务不再自己 `new HttpClient()`。

## 7. 依赖项

新增：`Tomlyn 0.17.0`（用于 Local/Mods.cs 的 Forge/NeoForge mods.toml 解析）

## 8. Builder 集成

CoreOptions 新增 `CurseForgeApiKey` 配置项。
GameCoreBuilder 新增 `WithCurseForgeApiKey()` 方法。
Expansion 服务在 `Build()` 中统一创建。

## 9. 不走 DTO 反序列化的模块

- 整合包安装器 zip 解析：继续 `JsonNode` 手动导航
- CurseForge 搜索：手动 `JsonNode` 提取
- 加载器安装器的元数据解析：手动字符串处理
