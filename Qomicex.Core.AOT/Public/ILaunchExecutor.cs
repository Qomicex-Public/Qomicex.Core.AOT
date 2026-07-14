using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Public.Models;

namespace Qomicex.Core.AOT.Interfaces;

public interface ILaunchExecutor
{
    Task<LaunchResult> LaunchAsync(LaunchOptions options);

    Task<bool> KillAsync(int processId);
}
