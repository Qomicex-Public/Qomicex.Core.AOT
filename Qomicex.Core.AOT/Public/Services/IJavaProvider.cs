using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Public.Services
{
    public interface IJavaProvider
    {
        Task<List<JavaResult>> Search(JavaSearchOptions options);
        Task<JavaResult> Recommand(List<JavaResult> javaResults, CompleteVersionMetadata metadata);
    }
}
