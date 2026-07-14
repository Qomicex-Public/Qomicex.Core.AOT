using System.Diagnostics;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Services;

namespace Qomicex.Core.AOT.Debugger;

internal static class AuthCommands
{
    static IAuthProvider? _current;
    static AuthResult? _lastResult;

    public static void Execute(string[] args)
    {
        var sub = args[0].ToLowerInvariant();

        switch (sub)
        {
            case "offline":
                if (args.Length < 2) { Trace.TraceError("用法: auth offline <用户名>"); return; }
                AuthOffline(args[1]);
                break;

            case "yggdrasil":
                if (args.Length < 3) { Trace.TraceError("用法: auth yggdrasil <url> <邮箱> [密码]"); return; }
                AuthYggdrasil(args[1], args[2], args.Length > 3 ? args[3] : null);
                break;

            case "microsoft":
                if (args.Length < 2) { Trace.TraceError("用法: auth microsoft <clientId>"); return; }
                AuthMicrosoft(args[1]);
                break;

            case "validate":
                ValidateToken();
                break;

            case "invalidate":
                InvalidateToken();
                break;

            default:
                Trace.TraceError($"未知 auth 子命令: {sub}");
                break;
        }
    }

    static void AuthOffline(string username)
    {
        _current = new DefaultAuthProvider();
        Trace.TraceInformation($"离线认证: {username}");
        FireAuth(() => _current.AuthenticateAsync(new AuthRequest
        {
            Username = username,
            IsOffline = true
        }));
    }

    static void AuthYggdrasil(string url, string email, string? password)
    {
        var http = new HttpClient();
        _current = new YggdrasilAuthProvider(http, url);
        Trace.TraceInformation($"Yggdrasil 认证: {email} @ {url}");
        FireAuth(() => _current.AuthenticateAsync(new AuthRequest
        {
            Username = email,
            Password = password ?? ""
        }));
    }

    static void AuthMicrosoft(string clientId)
    {
        var http = new HttpClient();
        _current = new MicrosoftAuthProvider(http, clientId);
        Trace.TraceInformation("Microsoft 设备码认证开始");
        Trace.TraceInformation("请打开浏览器并完成登录...");
        FireAuth(() => _current.AuthenticateAsync(new AuthRequest()));
    }

    static void ValidateToken()
    {
        if (_current == null) { Trace.TraceError("请先认证 (auth offline/yggdrasil/microsoft)"); return; }
        if (_lastResult?.AccessToken == null) { Trace.TraceError("没有缓存的 token，请先认证"); return; }

        FireAsync(async () =>
        {
            Trace.TraceInformation("验证 token...");
            var valid = await _current.ValidateAsync(_lastResult.AccessToken);
            Trace.TraceInformation($"token 状态: {(valid ? "有效" : "无效")}");
        });
    }

    static void InvalidateToken()
    {
        if (_current == null) { Trace.TraceError("请先认证 (auth offline/yggdrasil/microsoft)"); return; }
        if (_lastResult?.AccessToken == null) { Trace.TraceError("没有缓存的 token，请先认证"); return; }

        FireAsync(async () =>
        {
            Trace.TraceInformation("吊销 token...");
            await _current.InvalidateAsync(_lastResult.AccessToken);
            Trace.TraceInformation("token 已吊销");
        });
    }

    static void FireAuth(Func<Task<AuthResult>> action)
    {
        FireAsync(async () =>
        {
            var result = await action();
            _lastResult = result;
            if (result.Success)
            {
                Trace.TraceInformation($"认证成功: {result.Username} ({result.Uuid})");
                Trace.TraceInformation($"  AccessToken: {result.AccessToken?[..Math.Min(20, result.AccessToken?.Length ?? 0)]}...");
                Trace.TraceInformation($"  UserType:    {result.UserType}");
                Trace.TraceInformation($"  ExpiresAt:   {result.ExpiresAt?.ToString("O") ?? "(永不过期)"}");
            }
            else
            {
                Trace.TraceError($"认证失败: {result.ErrorMessage}");
            }
        });
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
