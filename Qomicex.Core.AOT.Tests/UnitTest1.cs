using System.Diagnostics;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Xunit.Abstractions;

namespace Qomicex.Core.AOT.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly DefaultGameCore Core;
    protected readonly string GameRootPath;
    protected readonly Stopwatch Stopwatch;
    private bool _disposed;

    protected TestBase(ITestOutputHelper output)
    {
        Output = output;
        Stopwatch = new Stopwatch();

        GameRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft_test",
            Guid.NewGuid().ToString()
        );

        Directory.CreateDirectory(GameRootPath);

        Core = new GameCoreBuilder()
            .UseGameRoot(GameRootPath)
            .Build();

        Log($"测试初始化完成");
        Log($"游戏根目录: {GameRootPath}");
    }

    protected void Log(string message)
    {
        Output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    protected void LogElapsed(string operation, long elapsedMs)
    {
        Log($"{operation} 耗时: {elapsedMs}ms");
    }

    protected async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Stopwatch.Restart();
        try
        {
            var result = await action();
            Stopwatch.Stop();
            LogElapsed(operation, Stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            Stopwatch.Stop();
            Log($"❌ {operation} 失败: {ex.Message}");
            throw;
        }
    }

    protected T Measure<T>(string operation, Func<T> action)
    {
        Stopwatch.Restart();
        try
        {
            var result = action();
            Stopwatch.Stop();
            LogElapsed(operation, Stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            Stopwatch.Stop();
            Log($"❌ {operation} 失败: {ex.Message}");
            throw;
        }
    }

    protected bool ValidateFile(string path)
    {
        if (!File.Exists(path))
        {
            Log($"⚠️ 文件不存在: {path}");
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length == 0)
        {
            Log($"⚠️ 文件为空: {path}");
            return false;
        }

        Log($"✅ 文件验证通过: {path} ({info.Length:N0} bytes)");
        return true;
    }

    protected long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }

    protected void LogDirectoryInfo(string path, string label = "目录")
    {
        if (!Directory.Exists(path))
        {
            Log($"⚠️ {label}不存在: {path}");
            return;
        }

        var fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
        var size = GetDirectorySize(path);
        Log($"📁 {label}: {path}");
        Log($"   文件数: {fileCount}");
        Log($"   大小: {size / 1024.0 / 1024.0:F2} MB");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            Core?.Dispose();
            if (Directory.Exists(GameRootPath))
            {
                Log($"测试目录: {GameRootPath}");
                LogDirectoryInfo(GameRootPath, "测试目录内容");
            }
        }
        _disposed = true;
    }
}
