using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Qomicex.Core.AOT.Builder
{
    /// <summary>
    /// 启动核心的配置项类
    /// </summary>
    public sealed class CoreOptions
    {
        // 路径
        /// <summary>
        /// 游戏根目录, 默认值为 ".minecraft"
        /// </summary>
        public string GameRoot { get; set; } = ".minecraft";

        // 性能
        /// <summary>
        /// 最大并发下载数, 默认值为 8
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 8;
        /// <summary>
        /// 缓存过期时间, 默认值为 5 分钟
        /// </summary>
        public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromMinutes(5);

        // 下载源
        /// <summary>
        /// 下载源，默认官方源
        /// </summary>
        public DownloadMirror DownloadMirror { get; set; } = DownloadMirror.Official;

        // 子选项
        /// <summary>
        /// 账户选项
        /// </summary>
        public AuthOptions Auth { get; set; } = new();
        /// <summary>
        /// Java选项
        /// </summary>
        public JavaOptions Java { get; set; } = new();
    }

    public sealed class AuthOptions
    {
        /// <summary>
        /// 登录方式
        /// </summary>
        public AuthMode Mode { get; set; } = AuthMode.Offline;
        public string? MicrosoftClientId { get; set; }
        public string? OfflineUsername { get; set; } = "Player";
    }

    public sealed class JavaOptions
    {
        public string JavaPath { get; set; } = "java";
        //public int MinMemoryMB { get; set; } = 1024;
        public int MaxMemoryMB { get; set; } = 512;
        public string[]? ExtraJvmArgs { get; set; }
    }

    public enum DownloadMirror { Official, BMCLAPI, MCBBS }
    public enum AuthMode { Offline, Microsoft, Yggdrasil }
}
