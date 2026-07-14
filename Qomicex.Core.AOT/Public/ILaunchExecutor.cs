using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Public.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Interfaces;

internal interface ILaunchExecutor
{
    Task<LaunchResult> LaunchAsync(LaunchOptions options);
}