# Qomicex.Core.AOT

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)
![AOT](https://img.shields.io/badge/AOT-✓-brightgreen?style=for-the-badge)
![NuGet](https://img.shields.io/badge/NuGet-v1.0.0-004880?logo=nuget&style=for-the-badge)
![License](https://img.shields.io/badge/License-GPL%20V3-yellow?style=for-the-badge&logo=gnu)

以 C# 写就的下一代 Minecraft 启动核心，全栈 Native AOT 编译，提供最轻量、快速和完整的开发体验。

> 本仓库是 [Qomicex-Public](https://github.com/Qomicex-Public) 组织下的独立仓库，是 [Qomicex.Core](https://github.com/Qomicex-Public/Qomicex.Core) 的 **Native AOT 重构版本**。新版核心将逐步接入 [Qomicex.Tauri](https://github.com/Qomicex-Public/Qomicex.Tauri) 启动器，直至替换原核心。

零外部 NuGet 依赖（仅 Tomlyn 用于 TOML 解析），全链路覆盖：**认证 → 版本管理 → ModLoader 安装 → 资源下载 → 游戏启动 → 扩展平台**。

## Native AOT 支持

Qomicex.Core.AOT 为 [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) 提供全面支持：

- `PublishAot=true` — 全量 AOT 编译
- `IsAotCompatible=true` — 标记为 AOT 兼容
- `EnableAotAnalyzer=true` — 编译期 AOT 兼容性分析
- 所有 JSON 序列化使用 System.Text.Json Source Generator，无运行时反射
- 不使用 DI 容器，手动依赖注入，零反射开销

Native AOT 应用程序启动极快、内存占用极少，用户无需安装 .NET 运行时即可运行。

## 跨平台支持

| 平台 | 状态 |
| --- | --- |
| Windows | ✅ |
| macOS | ✅ |
| Linux | ✅ |

## 功能列表

| 功能 | 状态 |
| --- | --- |
| 离线认证 | ✅ |
| Microsoft 正版认证 (OAuth 设备码流) | ✅ |
| Yggdrasil 外置登录 (authlib-injector) | ✅ |
| 令牌刷新与验证 | ✅ |
| 版本清单获取与缓存 | ✅ |
| 版本扫描与定位 | ✅ |
| 版本隔离 | ✅ |
| 资源自动补全（多线程下载） | ✅ |
| 缺失资源检查（库 / 主 Jar / Assets） | ✅ |
| 下载镜像切换（官方 / BMCLAPI） | ✅ |
| 自定义下载源 | ✅ |
| 原生进程启动 | ✅ |
| JVM 参数自动组装 | ✅ |
| Java 运行时检测与推荐 | ✅ |
| Java 在线下载（Adoptium / Zulu / BMCLAPI） | ✅ |
| 注册表 / 环境变量 / PATH / 多源扫描 | ✅ |
| Forge 安装（Legacy + New） | ✅ |
| NeoForge 安装 | ✅ |
| Fabric 安装 | ✅ |
| Quilt 安装 | ✅ |
| OptiFine 安装 | ✅ |
| LiteLoader 安装 | ✅ |
| ModLoader 版本查询（6 种加载器） | ✅ |
| Modrinth API（搜索 / 详情 / 版本 / 哈希反查） | ✅ |
| CurseForge API（搜索 / 详情 / 文件 / 指纹反查） | ✅ |
| Feed The Beast API（整合包 / 版本 / 更新日志） | ✅ |
| 本地文件 MurmurHash2 指纹 | ✅ |
| launcher_profiles.json 解析与生成 | ✅ |
| 游戏版本 JSON 继承与合并 | ✅ |
| 自定义窗口标题 / 分辨率 | ✅ |
| 加入指定服务器 / 存档 | ✅ |
| NuGet 分发 | ✅ |

## 安装方法

通过 NuGet 包管理器安装：

```
dotnet add package Qomicex.Core.AOT
```

或在包管理器控制台中执行：

```
Install-Package Qomicex.Core.AOT
```

**要求：** .NET 10.0 及以上版本。

## 使用说明

### 初始化核心

```csharp
using Qomicex.Core.AOT.Builder;

var core = new GameCoreBuilder()
    .UseGameRoot(@"C:\.minecraft")
    .UseMicrosoftAuth("your-client-id")
    .UseDownloadMirror(DownloadMirror.BMCLAPI)
    .Build();
```

### 认证

**离线模式：**

```csharp
core.Auth.AuthenticateAsync(new AuthRequest
{
    Mode = AuthMode.Offline,
    Name = "Player"
});
```

**Microsoft 正版登录（设备码流）：**

```csharp
var deviceCode = await core.Auth.LoginAsync().ConfigureAwait(false);
// 打开 deviceCode.VerificationUri，输入 deviceCode.UserCode
// 等待用户授权后调用：
await core.Auth.CompleteLoginAsync().ConfigureAwait(false);
```

**Yggdrasil 外置登录：**

```csharp
core.Auth.AuthenticateAsync(new AuthRequest
{
    Mode = AuthMode.Yggdrasil,
    Email = "user@example.com",
    Password = "password",
    ServerUrl = "https://auth.example.com/authserver"
});
```

### 版本管理

```csharp
var versions = await core.Version.GetRemoteVersionsAsync();
var localVersions = core.Version.GetLocalVersions();

// 安装指定版本（自动补全资源）
await core.Version.InstallAsync("1.20.4");
```

### Java 运行时检测

```csharp
var javaList = await core.JavaProvider.SearchAsync(new JavaSearchOptions
{
    Mode = JavaSearchMode.Deep,
    MaxResults = 10
});

var recommended = await core.JavaProvider.Recommand(javaList, metadata);
```

**Java 在线下载：**

```csharp
var packages = await core.JavaProvider.GetPackages(
    majorVersion: 21,
    platform: JavaPlatform.Windows,
    architecture: JavaArchitecture.X64,
    packageType: JavaPackageType.JDK,
    source: JavaDownloadSource.Adoptium);
```

### ModLoader 版本查询与安装

```csharp
// 查询支持的 ModLoader 版本
var forgeVersions = await core.InstallerProvider.GetAvailableModLoaders(
    "1.20.4", ModLoaderType.Forge);

var allLoaders = await core.InstallerProvider.GetAvailableModLoaders("1.20.4");

// 使用安装器安装 ModLoader
var installer = new ForgeInstaller();
await installer.InstallAsync(
    versionId: "1.20.4",
    inheritsFromJson: vanillaVersionJson,
    forgeVersion: "49.0.0",
    gameDir: ".minecraft");
```

### 启动游戏

```csharp
var result = await core.Launch.ExecuteAsync(new LaunchOptions
{
    Version = "1.20.4-forge-49.0.0",
    JavaOptions = new JavaOptions
    {
        JavaPath = "path/to/java",
        MaxMemoryMB = 4096
    },
    VersionIsolation = true
});
```

### 扩展平台

**Modrinth：**

```csharp
var modrinth = core.CreateModrinthSource();
var results = await modrinth.SearchAsync("sodium", index: 0, pageSize: 20);
var project = await modrinth.GetProjectInfoAsync("sodium");
```

**CurseForge：**

```csharp
var curseforge = core.CreateCurseForgeSource("your-api-key");
var mods = await curseforge.SearchAsync(gameVersion: "1.20.4",
    classId: 6, // Mods
    searchFilter: "jei");

// 根据指纹反查本地模组
var result = await curseforge.GetInfoFromHashesAsync(fingerprints);
```

**Feed The Beast：**

```csharp
var ftb = core.CreateFTBSource();
var packs = await ftb.SearchAsync(mcVersion: "1.20.1",
    sortField: "updated");
var detail = await ftb.GetPackDetailAsync(packId);
```

### 进度监控

所有异步操作通过 `IProgress<T>` 报告进度：

```csharp
var progress = new Progress<DownloadProgress>(p =>
    Console.WriteLine($"{p.FileName}: {p.Percentage:P2}"));

await core.Version.InstallAsync("1.20.4", progress: progress);
```

## 项目结构

```
Qomicex.Core.AOT/
│
├── Builder/
│   ├── GameCoreBuilder.cs       # 唯一入口，Builder 模式组装所有服务
│   └── CoreOptions.cs           # 所有可配置项（枚举、认证、Java 等）
│
├── Core/
│   └── DefaultGameCore.cs       # Facade，聚合所有公开领域接口
│
├── Public/                      # 外部可见的 API
│   ├── IAuthProvider.cs         # 认证接口
│   ├── ILaunchExecutor.cs       # 启动执行器接口
│   ├── IVersionManagementService.cs  # 版本管理接口
│   ├── IVersionManifestService.cs    # 版本清单接口
│   ├── Expansion/
│   │   ├── ICurseForgeSource.cs
│   │   ├── IModrinthSource.cs
│   │   └── IFTBSource.cs
│   ├── Core/
│   │   ├── IVersionLocator.cs
│   │   ├── IResourceCompleter.cs
│   │   └── IDownloadSourceManager.cs
│   ├── Services/
│   │   ├── IInstallerProvider.cs
│   │   └── IJavaProvider.cs
│   └── Models/
│       ├── LaunchResult.cs
│       ├── JavaResult.cs
│       └── ModLoaderResult.cs
│
├── Services/                    # 全部 internal 实现
│   ├── VersionManagementService.cs    # 版本管理编排者
│   ├── VersionManifestService.cs      # 远程版本清单
│   ├── DefaultVersionLocator.cs       # 本地版本扫描
│   ├── DefaultResourceCompleter.cs    # 资源下载 + 重试 + 校验
│   ├── DefaultDownloadSourceManager.cs# 镜像 URL 转换
│   ├── MicrosoftAuthProvider.cs       # Microsoft OAuth 设备码流
│   ├── YggdrasilAuthProvider.cs       # Yggdrasil 外置登录
│   ├── DefaultAuthProvider.cs         # 离线认证
│   ├── LaunchExecutor.cs              # JVM 参数组装 + 进程启动
│   ├── JavaProvider.cs                # Java 运行时检测与推荐
│   ├── InstallerProvider.cs           # ModLoader 版本查询
│   ├── Installers/                    # 6 种 ModLoader 安装器
│   └── Expansion/                     # Modrinth / CurseForge / FTB / Local
│
├── Models/                      # 内部数据模型
├── JsonContext/                 # System.Text.Json Source Generator 上下文
├── Exceptions/                  # 自定义异常
├── Utils/                       # 工具类
└── Resource/                    # 嵌入式资源
```

## 分层架构

```
┌──────────────────────────────────────────┐
│  Public API                              │  ← 前端直接调用
│  IAuthProvider / ILaunchExecutor /       │
│  IVersionManagementService               │
├──────────────────────────────────────────┤
│  DefaultGameCore (Facade)                │  ← 聚合所有领域
├──────────────────────────────────────────┤
│  Services (internal)                     │  ← 全部实现，外部不可见
│  编排层 → VersionManagementService        │
│  核心层 → Manifest / Locator / Completer  │
│  安装层 → Forge / Fabric / NeoForge ...  │
│  扩展层 → Modrinth / CurseForge / FTB    │
│  支持层 → Auth / Java / InstallerProvider │
├──────────────────────────────────────────┤
│  Infrastructure                          │
│  HttpClient / FileSystem / Process       │
└──────────────────────────────────────────┘
```

## 关键设计决策

| 决策 | 理由 |
| --- | --- |
| Native AOT 全量编译 | 启动快、内存少、零运行依赖 |
| 手动依赖注入 | AOT 安全，依赖透明，学习成本低 |
| System.Text.Json Source Generator | 无反射，AOT 兼容 |
| Builder 模式 | 前端只需了解一个入口，配置集中 |
| 零外部依赖（除 Tomlyn） | 最小化发布体积和供应链风险 |
| 接口均分为 public / internal | 公开 API 极简，降低认知负担 |
| 使用 IProgress\<T\> 报告进度 | .NET 标准，AOT 安全 |
| Options 用 POCO | 不用 IConfiguration，保持零依赖 |
| 自实现 OAuth 设备码流 | 避免 MSAL 库的反射问题 |

## 免责声明

Qomicex.Core.AOT 不隶属于 Mojang Studios 或 Microsoft 及其附属软件的任何一部分。

## 许可证

本项目基于 [GNU General Public License v3.0](./LICENSE) 授权。

## 相关项目

| 项目 | 说明 |
| --- | --- |
| [Qomicex.Core](https://github.com/Qomicex-Public/Qomicex.Core) | 旧版核心库（非 AOT），本项目的上游 |
| [Qomicex.Tauri](https://github.com/Qomicex-Public/Qomicex.Tauri) | Qomicex 启动器主体（Tauri + React），消费本核心 |

## 反馈与参与

欢迎提交 Issue 或 PR 以修正 bug 并完善代码。

## 免责声明

Qomicex.Core.AOT 不隶属于 Mojang Studios 或 Microsoft 及其附属软件的任何一部分。
