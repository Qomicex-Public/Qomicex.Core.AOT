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
        var ms = new MicrosoftAuthProvider(http, clientId);
        _current = ms;

        FireAsync(async () =>
        {
            Trace.TraceInformation("正在获取设备码...");
            var dc = await ms.StartDeviceCodeAsync();
            if (dc == null)
            {
                Trace.TraceError("获取设备码失败");
                return;
            }

            var deviceCode = dc["device_code"]?.ToString();
            var userCode = dc["user_code"]?.ToString();
            var verifyUri = dc["verification_uri"]?.ToString();
            var interval = dc["interval"]?.GetValue<int>() ?? 5;
            var expiresIn = dc["expires_in"]?.GetValue<int>() ?? 900;

            if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode))
            {
                Trace.TraceError("设备码响应不完整");
                return;
            }

            Trace.TraceInformation($"请在浏览器打开: {verifyUri}");
            Trace.TraceInformation($"输入代码: {userCode}");

            var deadline = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(interval * 1000);
                var poll = await ms.PollForTokenAsync(deviceCode);
                if (poll == null) continue;

                var err = poll["error"]?.ToString();
                if (string.IsNullOrEmpty(err))
                {
                    var accessToken = poll["access_token"]?.ToString();
                    var refreshToken = poll["refresh_token"]?.ToString();
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        Trace.TraceError("poll 响应缺少 access_token");
                        return;
                    }

                    Trace.TraceInformation("用户已授权，正在完成登录...");
                    var result = await ms.CompleteLoginAsync(accessToken, refreshToken ?? "");
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
                    return;
                }

                if (err is "authorization_declined" or "expired_token")
                {
                    Trace.TraceError($"用户已拒绝或代码已过期: {err}");
                    return;
                }

                if (err == "slow_down")
                    interval += 5;
            }

            Trace.TraceError("设备码已过期，认证未完成");
        });
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
