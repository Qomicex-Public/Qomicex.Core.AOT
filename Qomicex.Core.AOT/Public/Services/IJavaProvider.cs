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
        bool Check(JavaResult java, CompleteVersionMetadata metadata);
        Task<List<JavaPackageInfo>> GetPackages(int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType,
            JavaDownloadSource source = JavaDownloadSource.Adoptium);
    }
}
