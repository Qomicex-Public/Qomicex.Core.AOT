namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

/// <summary>
/// 批量 GetFiles API 返回的单条文件信息（精简版，含 downloadUrl 和 Sha1）。
/// </summary>
public sealed class CurseForgeBatchFileInfo
{
    public long Id { get; set; }
    public long ModId { get; set; }
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public long? FileLength { get; set; }
    public string? Sha1 { get; set; }
}
