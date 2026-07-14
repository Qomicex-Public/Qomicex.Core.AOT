using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Qomicex.Core.AOT.Builder
{
    /// <summary>
    /// 启动核心的配置项类
    /// </summary>
    public sealed record class CoreOptions
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
        public string? MicrosoftClientId { get; set; }
    }

    public sealed record class AuthOptions
    {
        /// <summary>
        /// 登录方式
        /// </summary>
        public AuthMode Mode { get; set; } = AuthMode.Offline;
        public string? Uuid { get; set; }
        public string? Name { get; set; } = "Player";
        public string? Token { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }

    public sealed record class JavaOptions
    {
        public string JavaPath { get; set; } = "java";
        //public int MinMemoryMB { get; set; } = 1024;
        public int MaxMemoryMB { get; set; } = 512;
        public string[]? ExtraJvmArgs { get; set; }
    }

    public sealed record class LaunchOptions
    {
        public string? Version { get; set; }
        public bool? VersionIsolation {  get; set; }
        public string? JoinServer { get; set; }
        public string? JoinWorld { get; set; }
        public JavaOptions? JavaOptions { get; set; }
        public AuthOptions? AuthOptions { get; set; }
    }

    public enum DownloadMirror { Official, BMCLAPI }
    public enum AuthMode { Offline, Microsoft, Yggdrasil }
}
