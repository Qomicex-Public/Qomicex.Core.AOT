using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Qomicex.Core.AOT.Debugger;

internal sealed class Program
{
    static readonly CancellationTokenSource _shutdownCts = new();
    static readonly Dictionary<int, Job> _jobs = [];
    static int _nextJobId = 1;

    record Job(int Id, string Kind, string Target, CancellationTokenSource Cts);

    static void Main(string[] args)
    {
        Console.Title = "Qomicex.Core.AOT Debugger";

        Trace.Listeners.Clear();
        Trace.Listeners.Add(new ColoredConsoleTraceListener());
        Trace.AutoFlush = true;

        if (args.Length > 0)
        {
            RunOneShot(args);
            return;
        }

        RunRepl();
    }

    static void RunRepl()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _shutdownCts.Cancel();
        };

        WriteHeader();

        while (!_shutdownCts.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("dbg> ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (input == null) break;

            if (!ExecuteCommand(input.Trim())) break;
        }

        CoreCommands.DisposeCore();
        StopAllJobs();
        Trace.TraceInformation("再见");
    }

    static void RunOneShot(string[] args)
    {
        WriteHeader();

        if (args[0] is "help" or "?") { ShowHelp(); return; }

        if (args[0].ToLowerInvariant() == "use" && args.Length >= 2)
        {
            CoreCommands.InitCore(args[1]);
            return;
        }

        Trace.TraceError("用法: dotnet run use <游戏根目录>");
    }

    static bool ExecuteCommand(string input)
    {
        var parts = ParseArgs(input);
        if (parts.Length == 0) return true;

        var cmd = parts[0].ToLowerInvariant();
        var args = parts[1..];

        switch (cmd)
        {
            case "exit":
            case "quit":
            case "q":
                return false;

            case "help":
            case "?":
                ShowHelp();
                break;

            case "clear":
            case "cls":
                Console.Clear();
                break;

            case "echo":
                Console.WriteLine(string.Join(" ", args));
                break;

            case "use":
                CoreCommands.InitCore(string.Join(" ", args));
                break;

            case "versions":
                if (CoreCommands.IsInitialized)
                    CoreCommands.FireAsync(() => CoreCommands.ListVersionsAsync(CoreCommands.Core!, args.Contains("--refresh")));
                else CoreCommands.EnsureCore();
                break;

            case "latest":
                if (CoreCommands.IsInitialized)
                    CoreCommands.FireAsync(() => CoreCommands.ShowLatestAsync(CoreCommands.Core!));
                else CoreCommands.EnsureCore();
                break;

            case "installed":
                if (CoreCommands.IsInitialized) CoreCommands.ListInstalled(CoreCommands.Core!);
                else CoreCommands.EnsureCore();
                break;

            case "launch":
                if (CoreCommands.IsInitialized) CoreCommands.LaunchGame(CoreCommands.Core!, args[0], args[1]);
                else if (args.Length < 1) Trace.TraceError("用法: launch <版本ID> <Java路径>");
                else CoreCommands.EnsureCore();
                break;

            case "info":
                if (args.Length >= 1 && CoreCommands.IsInitialized) CoreCommands.FireAsync(() => CoreCommands.ShowVersionInfoAsync(args[0]));
                else if (args.Length < 1) Trace.TraceError("用法: info <版本ID>");
                else CoreCommands.EnsureCore();
                break;

            case "check":
                if (args.Length >= 1 && CoreCommands.IsInitialized) CoreCommands.CheckVersion(args[0]);
                else if (args.Length < 1) Trace.TraceError("用法: check <版本ID>");
                else CoreCommands.EnsureCore();
                break;

            case "install":
                if (args.Length >= 1 && CoreCommands.IsInitialized) CoreCommands.FireAsync(() => CoreCommands.InstallVersionAsync(args[0]));
                else if (args.Length < 1) Trace.TraceError("用法: install <版本ID>");
                else CoreCommands.EnsureCore();
                break;

            case "uninstall":
                if (args.Length >= 1 && CoreCommands.IsInitialized) CoreCommands.FireAsync(() => CoreCommands.UninstallVersionAsync(args[0]));
                else if (args.Length < 1) Trace.TraceError("用法: uninstall <版本ID>");
                else CoreCommands.EnsureCore();
                break;

            case "log":
                EmitTestLog(args);
                break;

            case "tail":
                StartTail(args);
                break;

            case "watch":
                StartWatch(args);
                break;

            case "exec":
                StartExec(args);
                break;

            case "jobs":
                ListJobs();
                break;

            case "stop":
                StopJob(args);
                break;

            case "stop-all":
                StopAllJobs();
                break;

            case "auth":
                if (args.Length >= 1) AuthCommands.Execute(args);
                else Trace.TraceError("用法: auth offline|yggdrasil|microsoft|validate|invalidate");
                break;

            case "exp":
            case "expansion":
                if (CoreCommands.IsInitialized)
                    ExpansionCommands.Execute(CoreCommands.Core!, args);
                else CoreCommands.EnsureCore();
                break;

            case "install-loader":
                if (CoreCommands.IsInitialized)
                    InstallerCommands.Execute(args);
                else CoreCommands.EnsureCore();
                break;

            case "sysinfo":
                ShowSysInfo();
                break;

            default:
                Trace.TraceError($"未知命令: {cmd}   输入 help 查看帮助");
                break;
        }

        return true;
    }

    static int StartJob(string kind, string target, CancellationToken ct, Action action)
    {
        var id = _nextJobId++;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var job = new Job(id, kind, target, cts);
        _jobs[id] = job;

        Task.Run(() =>
        {
            try { action(); }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_jobs) _jobs.Remove(id);
                Trace.TraceInformation($"[任务 #{id}] {kind} {target} 已结束");
            }
        }, cts.Token);

        Trace.TraceInformation($"[任务 #{id}] {kind} {target} 已启动");
        return id;
    }

    static void StopJob(string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var id))
        {
            Trace.TraceError("用法: stop <任务ID>");
            return;
        }

        if (!_jobs.TryGetValue(id, out var job))
        {
            Trace.TraceError($"任务 #{id} 不存在");
            return;
        }

        job.Cts.Cancel();
        Trace.TraceInformation($"[任务 #{id}] 已发送停止信号");
    }

    static void StopAllJobs()
    {
        lock (_jobs)
        {
            foreach (var (id, job) in _jobs)
                job.Cts.Cancel();
            _jobs.Clear();
        }
    }

    static void ListJobs()
    {
        lock (_jobs)
        {
            if (_jobs.Count == 0)
            {
                Console.WriteLine("  无运行中的任务");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ID  类型     目标");
            Console.WriteLine("  --  ----     ----");
            Console.ResetColor();

            foreach (var (id, job) in _jobs)
                Console.WriteLine($"  {id,-3} {job.Kind,-8} {job.Target}");
        }
    }

    static string[] ParseArgs(string input)
    {
        if (string.IsNullOrEmpty(input)) return [];

        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0) parts.Add(current.ToString());
        return [.. parts];
    }

    static void WriteHeader()
    {
        Console.Clear();
        Trace.WriteLine("============================================", "Init");
        Trace.WriteLine("  Qomicex.Core.AOT Debugger", "Init");
        Trace.WriteLine("============================================", "Init");
        Trace.TraceInformation($"PID: {Environment.ProcessId}  .NET: {Environment.Version}");
        Trace.TraceInformation($"工作目录: {Environment.CurrentDirectory}");
        Trace.TraceInformation("输入 help 查看命令列表");
        Console.WriteLine();
    }

    static void ShowHelp()
    {
        var cmds = new (string Cmd, string Desc)[]
        {
            ("help / ?", "显示此帮助"),
            ("use <游戏根目录>", "初始化 Core (连接到游戏目录)"),
            ("versions [--refresh]", "获取远程版本列表"),
            ("latest", "显示最新 Release/Snapshot"),
            ("installed", "列出本地已安装版本"),
            ("info <版本ID>", "查看版本元数据"),
            ("check <版本ID>", "检查版本是否已安装"),
            ("install <版本ID>", "安装指定版本"),
            ("uninstall <版本ID>", "卸载指定版本"),
            ("launch <版本ID> <Java路径>", "启动指定版本"),
            ("auth offline <用户名>", "测试离线认证"),
            ("auth yggdrasil <url> <邮箱> [密码]", "测试 Yggdrasil 认证"),
            ("auth microsoft <clientId>", "测试 Microsoft 设备码认证"),
            ("auth validate", "验证当前 token"),
            ("auth invalidate", "吊销当前 token"),
            ("", ""),
            ("tail <文件路径>", "后台跟踪日志文件"),
            ("watch <目录> [模式]", "后台监视目录文件变化"),
            ("exec <程序> [参数...]", "后台启动进程，捕获 stdout/stderr"),
            ("jobs", "列出后台任务"),
            ("stop <ID>", "停止指定后台任务"),
            ("stop-all", "停止所有后台任务"),
            ("", ""),
            ("log [级别] <消息>", "发送测试日志 (trace/debug/info/warn/error)"),
            ("echo <消息>", "回显"),
            ("sysinfo", "系统信息"),
            ("", ""),
            ("expansion modrinth search <q> [-t type] [-v ver] [-l loader]", "搜索 Modrinth"),
            ("expansion modrinth info <id>", "Modrinth 项目详情"),
            ("expansion curseforge search <q> [-v ver] [-l loader] -k <key>", "搜索 CurseForge"),
            ("expansion curseforge info <id> -k <key>", "CurseForge 模组详情"),
            ("expansion ftb search <q> [-v ver]", "搜索 FTB 整合包"),
            ("expansion ftb info <id>", "FTB 整合包详情"),
            ("expansion mods list <ver> [--segmented]", "列出本地模组"),
            ("", ""),
            ("install-loader fabric <vid> <gv> <fv>", "安装 Fabric 加载器"),
            ("install-loader forge <vid> <gv> <path> <java>", "安装 Forge 加载器"),
            ("install-loader neoforge <vid> <gv> <path> <java>", "安装 NeoForge 加载器"),
            ("install-loader quilt <vid> <gv> <qv>", "安装 Quilt 加载器"),
            ("install-loader litelloader <vid> <gv> <lv>", "安装 LiteLoader"),
            ("install-loader optifine <vid> <gv> <tp> <path> <java>", "安装 OptiFine"),
            ("", ""),
            ("clear / cls", "清屏"),
            ("exit / quit / q", "退出"),
        };

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Core 测试命令:");
        Console.ResetColor();

        foreach (var (cmd, desc) in cmds)
        {
            if (string.IsNullOrEmpty(cmd) && string.IsNullOrEmpty(desc))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("系统命令:");
                Console.ResetColor();
                continue;
            }
            if (string.IsNullOrEmpty(cmd)) continue;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  {cmd,-30}");
            Console.ResetColor();
            Console.WriteLine(desc);
        }

        Console.WriteLine();
    }

    static void ShowSysInfo()
    {
        Console.WriteLine($"  OS:           {Environment.OSVersion}");
        Console.WriteLine($"  .NET:         {Environment.Version}");
        Console.WriteLine($"  进程架构:     {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  OS 架构:      {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"  CPU 核心:     {Environment.ProcessorCount}");
        Console.WriteLine($"  PID:          {Environment.ProcessId}");
        Console.WriteLine($"  机器名:       {Environment.MachineName}");
        Console.WriteLine($"  用户:         {Environment.UserName}");
        Console.WriteLine($"  工作目录:     {Environment.CurrentDirectory}");
        Console.WriteLine($"  启动时间:     {Environment.TickCount64 / 1000 / 60} 分钟前");
        Console.WriteLine($"  64位进程:     {Environment.Is64BitProcess}");
        Console.WriteLine($"  调试器附加:   {System.Diagnostics.Debugger.IsAttached}");
        Console.WriteLine();
    }

    static void EmitTestLog(string[] args)
    {
        var level = args.Length > 0 ? args[0].ToLowerInvariant() : "info";
        var msg = args.Length > 1 ? string.Join(" ", args[1..]) : "测试消息";

        switch (level)
        {
            case "trace":  Trace.WriteLine(msg, "Test"); break;
            case "debug":  Debug.WriteLine(msg); break;
            case "info":   Trace.TraceInformation(msg); break;
            case "warn":   Trace.TraceWarning(msg); break;
            case "error":  Trace.TraceError(msg); break;
            default:       Trace.TraceInformation($"[{level}] {msg}"); break;
        }
    }

    static void StartTail(string[] args)
    {
        if (args.Length < 1)
        {
            Trace.TraceError("用法: tail <文件路径>");
            return;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Trace.TraceError($"文件不存在: {path}");
            return;
        }

        StartJob("tail", path, _shutdownCts.Token, () =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            fs.Seek(0, SeekOrigin.End);

            while (!_shutdownCts.IsCancellationRequested)
            {
                var line = reader.ReadLine();
                if (line != null)
                    Trace.TraceInformation(line);
                else
                    Thread.Sleep(100);
            }
        });
    }

    static void StartWatch(string[] args)
    {
        if (args.Length < 1)
        {
            Trace.TraceError("用法: watch <目录> [模式]");
            return;
        }

        var dir = args[0];
        if (!Directory.Exists(dir))
        {
            Trace.TraceError($"目录不存在: {dir}");
            return;
        }

        var pattern = args.Length > 1 ? args[1] : "*.log";

        StartJob("watch", $"{dir} ({pattern})", _shutdownCts.Token, () =>
        {
            using var watcher = new FileSystemWatcher(dir, pattern)
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = true,
            };

            watcher.Created += (_, e) => Trace.TraceInformation($"[新建] {e.FullPath}");
            watcher.Changed += (_, e) => Trace.TraceInformation($"[修改] {e.FullPath}");
            watcher.Deleted += (_, e) => Trace.TraceInformation($"[删除] {e.FullPath}");
            watcher.Renamed += (_, e) => Trace.TraceInformation($"[重命名] {e.OldFullPath} -> {e.FullPath}");
            watcher.Error += (_, e) => Trace.TraceWarning($"监视器错误: {e.GetException()?.Message}");

            _shutdownCts.Token.WaitHandle.WaitOne();
        });
    }

    static void StartExec(string[] args)
    {
        if (args.Length < 1)
        {
            Trace.TraceError("用法: exec <程序> [参数...]");
            return;
        }

        var fileName = args[0];
        var arguments = args.Length > 1 ? string.Join(" ", args[1..].Select(a => a.Contains(' ') ? $"\"{a}\"" : a)) : "";

        StartJob("exec", fileName, _shutdownCts.Token, () =>
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) Trace.TraceInformation(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) Trace.TraceError(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Trace.TraceInformation($"PID: {process.Id}");

            while (!_shutdownCts.IsCancellationRequested && !process.HasExited)
                _shutdownCts.Token.WaitHandle.WaitOne(500);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Trace.TraceWarning("进程已强制终止");
            }

            Trace.TraceInformation($"退出码: {process.ExitCode}");
        });
    }
}
