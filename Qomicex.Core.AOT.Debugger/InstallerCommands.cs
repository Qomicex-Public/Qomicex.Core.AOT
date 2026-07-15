using System.Diagnostics;
using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Services.Installers;

namespace Qomicex.Core.AOT.Debugger;

internal static class InstallerCommands
{
    static string GameRoot => CoreCommands.Core?.GameRoot ?? "";

    public static bool Execute(string[] args)
    {
        if (args.Length < 1) return false;

        var cmd = args[0].ToLowerInvariant();
        var rest = args[1..];

        switch (cmd)
        {
            case "fabric":
                return FabricCmd(rest);
            case "forge":
                return ForgeCmd(rest);
            case "neoforge":
                return NeoForgeCmd(rest);
            case "quilt":
                return QuiltCmd(rest);
            case "litelloader":
                return LiteLoaderCmd(rest);
            case "optifine":
                return OptiFineCmd(rest);
            default:
                return false;
        }
    }

    public static void ShowHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("安装器命令:");
        Console.ResetColor();

        var cmds = new (string Cmd, string Desc)[]
        {
            ("install-loader loader fabric <vid> <gv> <fv>", "安装 Fabric: vid=版本ID, gv=游戏版本, fv=Fabric版本"),
            ("install-loader forge <vid> <gv> <path> <java>", "安装 Forge: path=安装器路径, java=java路径"),
            ("install-loader neoforge <vid> <gv> <path> <java>", "安装 NeoForge"),
            ("install-loader quilt <vid> <gv> <qv>", "安装 Quilt"),
            ("install-loader litelloader <vid> <gv> <lv>", "安装 LiteLoader"),
            ("install-loader optifine <vid> <gv> <type-patch> <path> <java>", "安装 OptiFine"),
        };

        foreach (var (c, d) in cmds)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  {c,-50}");
            Console.ResetColor();
            Console.WriteLine(d);
        }
        Console.WriteLine();
    }

    static bool FabricCmd(string[] args)
    {
        if (args.Length < 3) { Trace.TraceError("用法: install-loader loader fabric <vid> <gv> <fv>"); return true; }
        var (vid, gv, fv) = (args[0], args[1], args[2]);

        FireAsync(async () =>
        {
            var installer = new FabricInstaller(0, GameRoot);
            await installer.InstallAsync(vid, "", fv, gv, null, null);
            Trace.TraceInformation($"Fabric {fv} 安装完成 -> {vid}");
        });
        return true;
    }

    static bool ForgeCmd(string[] args)
    {
        if (args.Length < 4) { Trace.TraceError("用法: install-loader forge <vid> <gv> <path> <java>"); return true; }
        var (vid, gv, path, java) = (args[0], args[1], args[2], args[3]);

        FireAsync(async () =>
        {
            var installer = new ForgeInstaller(0, GameRoot, gv);
            await installer.InstallAsync(vid, "", java, path, null, null);
            Trace.TraceInformation($"Forge 安装完成 -> {vid}");
        });
        return true;
    }

    static bool NeoForgeCmd(string[] args)
    {
        if (args.Length < 4) { Trace.TraceError("用法: install-loader neoforge <vid> <gv> <path> <java>"); return true; }
        var (vid, gv, path, java) = (args[0], args[1], args[2], args[3]);

        FireAsync(async () =>
        {
            var installer = new NeoForgeInstaller(0, GameRoot, gv);
            await installer.InstallAsync(vid, "", java, path, null, null);
            Trace.TraceInformation($"NeoForge 安装完成 -> {vid}");
        });
        return true;
    }

    static bool QuiltCmd(string[] args)
    {
        if (args.Length < 3) { Trace.TraceError("用法: install-loader quilt <vid> <gv> <qv>"); return true; }
        var (vid, gv, qv) = (args[0], args[1], args[2]);

        FireAsync(async () =>
        {
            var installer = new QuiltInstaller(0, GameRoot);
            await installer.InstallAsync(vid, "", qv, gv, null, null);
            Trace.TraceInformation($"Quilt {qv} 安装完成 -> {vid}");
        });
        return true;
    }

    static bool LiteLoaderCmd(string[] args)
    {
        if (args.Length < 3) { Trace.TraceError("用法: install-loader litelloader <vid> <gv> <lv>"); return true; }
        var (vid, gv, lv) = (args[0], args[1], args[2]);

        FireAsync(async () =>
        {
            var installer = new LiteloaderInstaller(0, GameRoot, gv);
            await installer.InstallAsync(vid, "", lv, gv, null, null);
            Trace.TraceInformation($"LiteLoader {lv} 安装完成 -> {vid}");
        });
        return true;
    }

    static bool OptiFineCmd(string[] args)
    {
        if (args.Length < 5) { Trace.TraceError("用法: install-loader optifine <vid> <gv> <type-patch> <path> <java>"); return true; }
        var (vid, gv, tp, path, java) = (args[0], args[1], args[2], args[3], args[4]);

        FireAsync(async () =>
        {
            var installer = new OptiFineInstaller(0, GameRoot, gv);
            await installer.InstallAsync(vid, "", tp, path, java, null);
            Trace.TraceInformation($"OptiFine 安装完成 -> {vid}");
        });
        return true;
    }

    static void FireAsync(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex) { Trace.TraceError($"安装失败: {ex.Message}"); }
        });
    }
}
