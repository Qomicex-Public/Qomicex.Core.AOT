using Qomicex.Core.AOT.Models.Expansion.Local;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

public class Screenshots : LocalResourceBase
{
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSegmented;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public Screenshots(HttpClient http, string gameDirectory, string version, bool versionSegmented, string apiKey)
    {
        _http = http;
        _gameDirectory = gameDirectory;
        _version = version;
        _versionSegmented = versionSegmented;
        _apiKey = apiKey;
    }

    #region 文件扫描

    private List<string> GetScreenshotFiles()
    {
        string screenshotDirectory = _versionSegmented
            ? Path.Combine(_gameDirectory, "versions", _version, "screenshots")
            : Path.Combine(_gameDirectory, "screenshots");

        if (!Directory.Exists(screenshotDirectory))
            return [];

        return Directory.GetFiles(screenshotDirectory, "*.png").ToList();
    }

    #endregion

    #region 截图列表

    public List<ScreenshotInfo> GetScreenshotList()
    {
        var files = GetScreenshotFiles();
        var screenshotInfos = new List<ScreenshotInfo>();

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);

            screenshotInfos.Add(new ScreenshotInfo
            {
                FilePath = file,
                FileName = fileInfo.Name,
                CreatedAt = fileInfo.CreationTime,
                FileSize = fileInfo.Length
            });
        }

        return screenshotInfos;
    }

    #endregion
}
