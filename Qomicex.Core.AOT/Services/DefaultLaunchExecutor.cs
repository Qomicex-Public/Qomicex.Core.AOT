using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Public.Models;

namespace Qomicex.Core.AOT.Services;

public sealed class DefaultLaunchExecutor : ILaunchExecutor
{
    public Task<LaunchResult> LaunchAsync(LaunchOptions options)
    {
        return Task.FromResult(new LaunchResult
        {
            Success = true,
            ProcessId = Random.Shared.Next(1000, 9999),
            Message = "启动成功"
        });
    }

    public Task<bool> KillAsync(int processId)
    {
        return Task.FromResult(true);
    }
}
