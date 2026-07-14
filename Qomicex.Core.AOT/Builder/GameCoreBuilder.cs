using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Builder
{
    /// <summary>
    /// 构建 <see cref="DefaultGameCore"/> 的构建器类。
    /// </summary>
    public sealed class GameCoreBuilder
    {
        private CoreOptions _options = new();
        public DefaultGameCore Build()
        {
            var http = new HttpClient();
            var downloadSource = new DefaultDownloadSourceManager();
            
            var 

            return new DefaultGameCore();
        }
    }
}
