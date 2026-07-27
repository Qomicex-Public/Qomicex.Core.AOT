# ADR-001：新增 Legacy Fabric 加载器支持

| 属性 | 内容 |
|:---|:---|
| 状态 | 已采纳 |
| 决策者 | 开发团队 |
| 日期 | 2026-07-27 |
| 关联 | 无 |

## 背景

Legacy Fabric 是 Fabric 加载器的前身/分支，专门用于 Minecraft 1.12.2 及以下版本。用户需要在启动器中支持安装 Legacy Fabric 加载器以运行使用了 Legacy Fabric API 的老版本整合包。

## 决策

使用与 Fabric 相同的架构模式实现 Legacy Fabric 支持：

1. **仅使用官方源**：`https://meta.legacyfabric.net/v2/versions`
2. **版本限制**：仅对 MC ≤ 1.12.2 的版本显示 Legacy Fabric 选项
3. **安装流程**：与 Fabric 完全相同——从 meta API 获取 profile JSON，下载 libraries，合并版本 JSON

## 备选方案

### 方案 A：独立安装器（选中）
- 优点：与现有 Fabric 安装流程一致，代码复用度高
- 缺点：无
- 为何选：与 Fabric 共享相同的数据结构（profile JSON），代码几乎可完全复用

### 方案 B：与 Fabric 共用安装器
- 优点：更少的代码
- 缺点：增加了 Fabric 安装器的复杂度，需要在内部区分 Legacy Fabric 和 Fabric
- 为何不选：破坏 SRP，且两个项目数据源不同

## 影响

- 正面影响：用户可在 MC 1.12.2 及以下版本安装 Legacy Fabric
- 负面影响/权衡：无
- 影响的模块/组件：ModLoaderType 枚举、InstallerProvider、IInstallerFactory、新增 LegacyFabricInstaller

## 后续行动

- [ ] 编写自动化测试用例

## 修订记录

| 日期 | 版本 | 修改内容 | 修改人 |
|:---|:---|:---|:---|
| 2026-07-27 | v1.0 | 初版创建 | AI |
