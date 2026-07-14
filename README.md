# Qomicex.Core.AOT

Minecraft 启动核心库，以 NuGet 包形式被前端（WPF / Avalonia / MAUI）消费。
全链路覆盖：**认证 → 版本管理 → 资源下载 → 游戏启动**。

- .NET 10 + AOT（`PublishAot=true`）
- 零外部 NuGet 依赖
- 手动依赖注入（无 DI 容器）

## 项目结构

```
Qomicex.Core.AOT/
│
├── Builder/
│   ├── GameCoreBuilder.cs    # 唯一入口，收集配置，组装所有服务
│   └── CoreOptions.cs        # 所有可配置项
│
├── Core/
│   └── DefaultGameCore.cs    # 门面，聚合三个公开领域接口
│
├── Public/                   # 外部可见的 API
│   ├── IVersionManagement.cs
│   ├── IAuthService.cs
│   ├── ILaunchExecutor.cs
│   └── Models/
│       ├── Session.cs
│       ├── LaunchOptions.cs
│       ├── LaunchResult.cs
│       ├── DownloadProgress.cs
│       └── InstallProgress.cs
│
├── Services/                 # 全部 internal 实现
│   ├── VersionManagementService.cs    # 编排者：协调以下服务
│   ├── VersionManifestService.cs      # 远程版本清单
│   ├── DefaultVersionLocator.cs       # 本地版本目录扫描
│   ├── DefaultResourceCompleter.cs    # 资源下载 + 重试 + 校验
│   ├── DefaultDownloadSourceManager.cs# 镜像 URL 转换
│   ├── VersionManifestCache.cs        # 文件缓存 + TTL
│   ├── MicrosoftAuthService.cs        # Microsoft OAuth 设备代码流
│   ├── OfflineAuthService.cs          # 离线模式
│   └── LaunchExecutor.cs              # JVM 参数组装 + 进程启动
│
├── Models/                   # 内部数据模型
│   ├── VersionManifest/
│   ├── VersionMetadata/
│   ├── Download/
│   ├── Local/
│   └── ResourceType.cs
│
├── JsonContext/              # System.Text.Json 源生成上下文
├── Exceptions/               # 自定义异常
├── Utils/                    # 工具类
└── Resource/                 # 嵌入式资源（Java 启动器 JAR）
```

## 分层架构

```
┌──────────────────────────────────────┐
│  Public API                          │  ← 前端直接调用的接口
│  IVersionManagement / IAuthService   │
│  / ILaunchExecutor                   │
├──────────────────────────────────────┤
│  DefaultGameCore (Facade)            │  ← 聚合三个领域
├──────────────────────────────────────┤
│  Services (internal)                 │  ← 全部实现，外部不可见
│  编排层 → VersionManagementService    │
│  核心层 → Manifest / Locator /        │
│           Completer / DownloadSource │
│  功能层 → AuthService / LaunchExecutor│
├──────────────────────────────────────┤
│  Infrastructure                      │
│  HttpClient / FileSystem / Process   │
└──────────────────────────────────────┘
```

## 调用方式

```csharp
var core = new GameCoreBuilder()
    .UseGameRoot(@"C:\minecraft")
    .UseMicrosoftAuth("client-id")
    .Build();

await core.Version.InstallAsync("1.20.4");
await core.Auth.LoginAsync();
await core.Launch.ExecuteAsync("1.20.4");
```

## 关键设计决策

| 决策 | 理由 |
|---|---|
| 手动 DI，无容器 | AOT 安全，学习曲线低，依赖透明 |
| Builder 模式 | 前端只需了解一个入口，配置集中 |
| 接口分为 public / internal | 公开 API 极简，降低使用者认知负担 |
| 使用 IProgress\<T\> 报告进度 | .NET 标准，AOT 安全 |
| Options 用 POCO | 不用 IConfiguration，保持零依赖 |
| 自己实现 OAuth 设备代码流 | 避免 MSAL 库的反射问题 |
