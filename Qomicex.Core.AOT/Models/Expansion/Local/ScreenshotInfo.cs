namespace Qomicex.Core.AOT.Models.Expansion.Local;

public class ScreenshotInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long FileSize { get; set; }
}
