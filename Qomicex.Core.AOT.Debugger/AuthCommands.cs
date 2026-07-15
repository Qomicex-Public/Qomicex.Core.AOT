using System.Diagnostics;
using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;

namespace Qomicex.Core.AOT.Debugger;

internal static class AuthCommands
{
    static DefaultGameCore? _core;
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
        DisposeCore();
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        _core = new GameCoreBuilder()
            .UseGameRoot(tmp)
            .UseOfflineAuth(username)
            .Build();

        Trace.TraceInformation($"离线认证: {username}");
        FireAuth(() => _core.Auth.AuthenticateAsync(new AuthRequest
        {
            Username = username,
            IsOffline = true
        }));
    }

    static void AuthYggdrasil(string url, string email, string? password)
    {
        DisposeCore();
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        _core = new GameCoreBuilder()
            .UseGameRoot(tmp)
            .UseYggdrasilAuth(url, email)
            .Build();

        Trace.TraceInformation($"Yggdrasil 认证: {email} @ {url}");
        FireAuth(() => _core.Auth.AuthenticateAsync(new AuthRequest
        {
            Username = email,
            Password = password ?? ""
        }));
    }

    static void AuthMicrosoft(string clientId)
    {
        DisposeCore();
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        _core = new GameCoreBuilder()
            .UseGameRoot(tmp)
            .UseMicrosoftAuth(clientId)
            .Build();

        FireAsync(async () =>
        {
            Trace.TraceInformation("正在获取设备码...");
            var dc = await _core.Auth.StartDeviceCodeAsync();
            if (dc == null)
            {
                Trace.TraceError("获取设备码失败");
                return;
            }

            Trace.TraceInformation($"请在浏览器打开: {dc.VerificationUri}");
            Trace.TraceInformation($"输入代码: {dc.UserCode}");

            var interval = dc.Interval;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(dc.ExpiresIn);

            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(interval * 1000);
                var poll = await _core.Auth.PollForTokenAsync(dc.DeviceCode);
                if (poll == null) continue;

                if (poll.IsCompleted)
                {
                    if (string.IsNullOrEmpty(poll.AccessToken))
                    {
                        Trace.TraceError("poll 响应缺少 access_token");
                        return;
                    }

                    Trace.TraceInformation("用户已授权，正在完成登录...");
                    var result = await _core.Auth.CompleteLoginAsync(poll.AccessToken, poll.RefreshToken ?? "");
                    _lastResult = result;
                    ShowResult(result);
                    return;
                }

                if (!poll.IsPending)
                {
                    Trace.TraceError($"用户已拒绝或代码已过期: {poll.Error}");
                    return;
                }

                if (poll.Error == "slow_down")
                    interval += 5;
            }

            Trace.TraceError("设备码已过期，认证未完成");
        });
    }

    static void ValidateToken()
    {
        if (_core == null) { Trace.TraceError("请先认证 (auth offline/yggdrasil/microsoft)"); return; }
        if (_lastResult?.AccessToken == null) { Trace.TraceError("没有缓存的 token，请先认证"); return; }

        FireAsync(async () =>
        {
            Trace.TraceInformation("验证 token...");
            var valid = await _core.Auth.ValidateAsync(_lastResult.AccessToken);
            Trace.TraceInformation($"token 状态: {(valid ? "有效" : "无效")}");
        });
    }

    static void InvalidateToken()
    {
        if (_core == null) { Trace.TraceError("请先认证 (auth offline/yggdrasil/microsoft)"); return; }
        if (_lastResult?.AccessToken == null) { Trace.TraceError("没有缓存的 token，请先认证"); return; }

        FireAsync(async () =>
        {
            Trace.TraceInformation("吊销 token...");
            await _core.Auth.InvalidateAsync(_lastResult.AccessToken);
            Trace.TraceInformation("token 已吊销");
        });
    }

    static void FireAuth(Func<Task<AuthResult>> action)
    {
        FireAsync(async () =>
        {
            var result = await action();
            _lastResult = result;
            ShowResult(result);
        });
    }

    static void ShowResult(AuthResult result)
    {
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
    }

    static void FireAsync(Func<Task> action)
    {
        _ = Task.Run(async () =>
        {
            try { await action(); }
            catch (Exception ex) { Trace.TraceError($"操作失败: {ex.Message}"); }
        });
    }

    static void DisposeCore()
    {
        _core?.Dispose();
        _core = null;
        _lastResult = null;
    }
}
