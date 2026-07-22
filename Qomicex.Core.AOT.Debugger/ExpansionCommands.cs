using System.Diagnostics;
using Qomicex.Core.AOT.Core;
using LocalMods = Qomicex.Core.AOT.Services.Expansion.Local.Mods;

namespace Qomicex.Core.AOT.Debugger;

internal static class ExpansionCommands
{
    public static void Execute(DefaultGameCore core, string[] args)
    {
        if (args.Length < 1) { Trace.TraceError("用法: modrinth|curseforge|ftb|mods ..."); return; }

        var sub = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (sub)
        {
            case "modrinth":   ModrinthCmd(core, rest); break;
            case "curseforge": CurseForgeCmd(core, rest); break;
            case "ftb":        FtbCmd(core, rest); break;
            case "mods":       ModsCmd(core, rest); break;
            default: Trace.TraceError($"未知扩展命令: {sub}"); break;
        }
    }

    static void ModrinthCmd(DefaultGameCore core, string[] args)
    {
        if (args.Length < 1) { Trace.TraceError("用法: modrinth search|info ..."); return; }

        var sub = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (sub)
        {
            case "search":
                if (rest.Length < 1) { Trace.TraceError("用法: modrinth search <query> [-t type] [-v ver] [-l loader]"); return; }
                var query = rest[0];
                var type = ParseFlag(rest, "-t");
                var ver = ParseFlag(rest, "-v");
                var loader = ParseFlag(rest, "-l");
                FireAsync(async () =>
                {
                    var mr = core.CreateModrinthSource();
                    var results = await mr.SearchAsync(query, projectType: type, gameVersion: ver, loaders: loader != null ? [loader] : null);
                    Trace.TraceInformation($"Modrinth 搜索结果 ({results.TotalResults}):");
                    foreach (var r in results.Results.Take(10))
                        Trace.TraceInformation($"  {r.Id,-20} {r.Name,-30} {r.Type,-10} {r.DownloadCount,8}");
                });
                break;

            case "info":
                if (rest.Length < 1) { Trace.TraceError("用法: modrinth info <project-id>"); return; }
                var pid = rest[0];
                FireAsync(async () =>
                {
                    var mr = core.CreateModrinthSource();
                    var info = await mr.GetProjectInfoAsync(pid);
                    Trace.TraceInformation($"标题:     {info.Name}");
                    Trace.TraceInformation($"描述:     {info.Description}");
                    Trace.TraceInformation($"类型:     {info.Type}");
                    Trace.TraceInformation($"下载量:   {info.DownloadCount}");
                    Trace.TraceInformation($"加载器:   {string.Join(", ", info.Loaders ?? [])}");
                    Trace.TraceInformation($"版本:     {string.Join(", ", info.GameVersionIds ?? [])}");
                });
                break;

            default:
                Trace.TraceError($"未知 modrinth 子命令: {sub}");
                break;
        }
    }

    static void CurseForgeCmd(DefaultGameCore core, string[] args)
    {
        if (args.Length < 1) { Trace.TraceError("用法: curseforge search|info ..."); return; }

        var sub = args[0].ToLowerInvariant();
        var rest = args[1..];
        var apiKey = ParseFlag(rest, "-k");

        if (string.IsNullOrEmpty(apiKey))
        {
            Trace.TraceError("需要 API 密钥: -k <apiKey>");
            return;
        }

        switch (sub)
        {
            case "search":
                if (rest.Length < 1) { Trace.TraceError("用法: curseforge search <query> [-v ver] [-l loader] -k <apiKey>"); return; }
                var query = rest[0];
                var ver = ParseFlag(rest, "-v");
                var loader = ParseFlag(rest, "-l");
                FireAsync(async () =>
                {
                    var cf = core.CreateCurseForgeSource(apiKey);
                    var response = await cf.SearchAsync(query, gameVersions: ver != null ? [ver] : null, categories: null, modLoaderTypes: loader != null ? [loader] : null);
                    Trace.TraceInformation($"CurseForge 搜索结果 ({response.TotalCount} total, showing {response.Results.Count}):");
                    foreach (var r in response.Results.Take(10))
                        Trace.TraceInformation($"  {r.Id,-10} {r.Name,-30} {r.DownloadCount,8}");
                });
                break;

            case "info":
                if (rest.Length < 1) { Trace.TraceError("用法: curseforge info <mod-id> -k <apiKey>"); return; }
                var id = rest[0];
                FireAsync(async () =>
                {
                    var cf = core.CreateCurseForgeSource(apiKey);
                    var info = await cf.GetModInfoAsync(id);
                    Trace.TraceInformation($"名称:     {info.Name}");
                    Trace.TraceInformation($"摘要:     {info.Summary}");
                    Trace.TraceInformation($"下载量:   {info.DownloadCount}");
                });
                break;

            default:
                Trace.TraceError($"未知 curseforge 子命令: {sub}");
                break;
        }
    }

    static void FtbCmd(DefaultGameCore core, string[] args)
    {
        if (args.Length < 1) { Trace.TraceError("用法: ftb search|info ..."); return; }

        var sub = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (sub)
        {
            case "search":
                if (rest.Length < 1) { Trace.TraceError("用法: ftb search <query> [-v ver]"); return; }
                var query = rest[0];
                var ver = ParseFlag(rest, "-v");
                FireAsync(async () =>
                {
                    var ftb = core.CreateFTBSource();
                    var packs = await ftb.SearchAsync(query, mcVersion: ver);
                    Trace.TraceInformation($"FTB 搜索结果 ({packs.Count}):");
                    foreach (var p in packs.Take(10))
                        Trace.TraceInformation($"  {p.Id,-6} {p.Name,-30} {p.Plays,8}");
                });
                break;

            case "info":
                if (rest.Length < 1) { Trace.TraceError("用法: ftb info <pack-id>"); return; }
                if (!int.TryParse(rest[0], out var packId))
                { Trace.TraceError("pack-id 必须为数字"); return; }
                FireAsync(async () =>
                {
                    var ftb = core.CreateFTBSource();
                    var detail = await ftb.GetPackDetailAsync(packId);
                    if (detail == null) { Trace.TraceError("未找到该整合包"); return; }
                    Trace.TraceInformation($"名称:     {detail.Name}");
                    Trace.TraceInformation($"简介:     {detail.Synopsis}");
                    Trace.TraceInformation($"下载量:   {detail.Installs}");
                });
                break;

            default:
                Trace.TraceError($"未知 ftb 子命令: {sub}");
                break;
        }
    }

    static void ModsCmd(DefaultGameCore core, string[] args)
    {
        if (args.Length < 1) { Trace.TraceError("用法: mods list <version> [--segmented]"); return; }

        var sub = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (sub)
        {
            case "list":
                if (rest.Length < 1) { Trace.TraceError("用法: mods list <version> [--segmented]"); return; }
                var ver = rest[0];
                var segmented = rest.Contains("--segmented");
                FireAsync(async () =>
                {
                    var mods = new LocalMods(core.HttpClient, core.GameRoot, ver, segmented, "");
                    var list = await mods.GetModList();
                    Trace.TraceInformation($"本地模组 ({list.Count}):");
                    foreach (var m in list)
                        Trace.TraceInformation($"  {m.Name,-30} {m.Version,-15} CF:{m.CurseForgeId} MR:{m.ModrinthId}");
                });
                break;

            default:
                Trace.TraceError($"未知 mods 子命令: {sub}");
                break;
        }
    }

    static string? ParseFlag(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    static void FireAsync(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex) { Trace.TraceError($"操作失败: {ex.Message}"); }
        });
    }
}
