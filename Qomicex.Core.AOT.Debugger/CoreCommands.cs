using System.Diagnostics;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Models.Download;

namespace Qomicex.Core.AOT.Debugger;

internal static class CoreCommands
{
    static DefaultGameCore? _core;

    public static DefaultGameCore? Core => _core;

    public static bool IsInitialized => _core != null;

    public static void DisposeCore()
    {
        _core?.Dispose();
        _core = null;
    }

    public static bool EnsureCore()
    {
        if (_core != null) return true;
        Trace.TraceError("请先用 use 命令设置游戏根目录.  示例: use C:\\Games\\.minecraft");
        return false;
    }

    public static void InitCore(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            Trace.TraceError("用法: use <游戏根目录>");
            return;
        }

        try
        {
            _core?.Dispose();
            _core = new GameCoreBuilder().UseGameRoot(gameRoot).Build();
            Trace.TraceInformation($"Core 已初始化, 游戏根目录: {gameRoot}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Core 初始化失败: {ex.Message}");
        }
    }

    public static void FireAsync(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex) { Trace.TraceError($"操作失败: {ex.Message}"); }
        });
    }

    public static async Task ListVersionsAsync(DefaultGameCore core, bool forceRefresh)
    {
        Trace.TraceInformation("正在获取版本列表...");
        var versions = await core.GetAvailableVersionsAsync(forceRefresh);
        Trace.TraceInformation($"共 {versions.Count} 个版本");

        var groups = versions.GroupBy(v => v.Type).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            Trace.TraceInformation($"  {g.Key}: {g.Count()} 个版本");

            var sample = g.Take(5).Select(v => v.Id);
            Trace.TraceInformation($"    例: {string.Join(", ", sample)}");
        }
    }

    public static async Task ShowLatestAsync(DefaultGameCore core)
    {
        Trace.TraceInformation("正在获取最新版本...");
        var latest = await core.GetLatestVersionsAsync();
        Trace.TraceInformation($"Release:  {latest.Release}");
        Trace.TraceInformation($"Snapshot: {latest.Snapshot}");
    }

    public static void ListInstalled(DefaultGameCore core)
    {
        var installed = core.GetInstalledVersions();
        Trace.TraceInformation($"已安装 {installed.Count} 个版本");

        foreach (var v in installed)
        {
            var size = v.TotalSize > 1024 * 1024
                ? $"{v.TotalSize / 1024.0 / 1024.0:F1} MB"
                : $"{v.TotalSize / 1024.0:F1} KB";
            Trace.TraceInformation($"  {v.Id,-20} {v.Type,-10} {size,10}  {(v.IsComplete ? "完整" : "不完整")}");
        }
    }

    public static async Task ShowVersionInfoAsync(string versionId)
    {
        try
        {
            var meta = await _core!.GetVersionMetadataAsync(versionId);
            Trace.TraceInformation($"ID:        {meta.Id}");
            Trace.TraceInformation($"类型:      {meta.Type}");
            Trace.TraceInformation($"发布时间:  {meta.ReleaseTime}");
            Trace.TraceInformation($"继承自:    {meta.InheritsFrom ?? "(无)"}");
            Trace.TraceInformation($"参数格式:  {meta.Arguments?.GetType().Name ?? "(无)"}");
            Trace.TraceInformation($"主类:      {meta.MainClass}");
            Trace.TraceInformation($"Libraries: {meta.Libraries.Count} 个");
            Trace.TraceInformation($"Asset索引: {meta.AssetIndex?.Id ?? "(无)"}");

            if (meta.JavaVersion != null)
                Trace.TraceInformation($"Java版本:  {meta.JavaVersion.MajorVersion}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"获取元数据失败: {ex.Message}");
        }
    }

    public static void CheckVersion(string versionId)
    {
        var installed = _core!.IsVersionInstalled(versionId);
        Trace.TraceInformation($"版本 {versionId}: {(installed ? "已安装" : "未安装")}");
    }

    public static async Task InstallVersionAsync(string versionId)
    {
        Trace.TraceInformation($"开始安装 {versionId}...");
        var progress = new Progress<DownloadProgress>(p =>
        {
            var status = p.Status switch
            {
                DownloadStatus.Downloading => "下载中",
                DownloadStatus.Completed => "完成",
                DownloadStatus.Retrying => "重试",
                _ => p.Status.ToString(),
            };
            Trace.TraceInformation($"[{status}] {p.FileName}  {p.Percentage:F1}%");
        });

        await _core!.InstallVersionAsync(versionId, progress);
        Trace.TraceInformation($"版本 {versionId} 安装完成");
    }

    public static async Task UninstallVersionAsync(string versionId)
    {
        Trace.TraceInformation($"卸载 {versionId}...");
        await _core!.UninstallVersionAsync(versionId);
        Trace.TraceInformation($"版本 {versionId} 已卸载");
    }
}
