using Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;
using Qomicex.Core.AOT.Services.Expansion.CurseForge;
using Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Qomicex.Core.AOT.Services.Installers.Modpacks
{
    internal class FTBModpackInstaller : InstallerBase, IInstaller
    {
        private readonly string _gameDir;
        private readonly bool _versionIsolation;
        private readonly string _cfApiKety;
        private HttpClient _httpClient;
        private FTBBase _ftb { get; set; }

        internal FTBModpackInstaller(string gameDir, bool versionIsolation, HttpClient httpClient, string cfApiKey)
        {
            _gameDir = gameDir;
            _versionIsolation = versionIsolation;
            _ftb = new FTBBase(httpClient);
            _httpClient = httpClient;
            _cfApiKety = cfApiKey;
        }
        Task IInstaller.InstallAsync(string versionId, string inheritsFromJson, string packId, string packVersionId, string? para3, string? para4)
        {
            return Task.CompletedTask;
        }

        async Task<List<MissFileData>> IInstaller.GetMissLibrariesAsync(string versionId, string packId, string packVersionId)
        {
            var missFiles = new List<MissFileData>();
            int.TryParse(packId, out int _packId);
            int.TryParse(packVersionId, out int _packVersionId);

            Console.WriteLine($"[FTB] 开始解析版本清单 packId={_packId}, packVersionId={_packVersionId}");
            Console.WriteLine("[FTB] 请求版本详情...");
            var json = await _ftb.GetVersionDetailAsync(_packId, _packVersionId);
            Console.WriteLine($"[FTB] 版本详情获取完成，文件数={json?.Files?.Count ?? 0}");

            if (json is null)
                throw new Exception("无法获取整合包信息");

            foreach(var file in json?.Files)
            {
                if (file.ServerOnly == true)
                    continue;
                string path = _versionIsolation ? Path.Combine(file.Path.Replace("./", Path.Combine(_gameDir, "versions", versionId)), file.Name) : Path.Combine(file.Path.Replace("./", _gameDir), file.Name);
                string url = file.Url;
                string sha1 = file.Sha1;
                string name = file.Name;
                missFiles.Add(new MissFileData(name, path, url, sha1));
            }

            Console.WriteLine("[FTB] 请求 Mod 清单...");
            ModsDetail modsInfo = await _ftb.GetModDetailAsync(_packId, _packVersionId);
            Console.WriteLine($"[FTB] Mod 清单获取完成，mod数={modsInfo?.Mods?.Count ?? 0}");

            if (modsInfo is null)
                throw new Exception("无法获取整合包 Mod 信息");

            var _cf = new Services.Expansion.CurseForge.Mods(_httpClient,_cfApiKety);

            var fileIds = modsInfo.Mods.Select(m => m.FileId).Where(id => id > 0).Distinct().ToList();
            Console.WriteLine($"[FTB] 批量查询 CurseForge 下载链接，有效fileId数={fileIds.Count}（总数={modsInfo.Mods.Count}）");
            var fileInfoMap = await _cf.GetFilesBatchAsync(fileIds);
            Console.WriteLine($"[FTB] CurseForge 批量查询完成，成功获取={fileInfoMap.Count}");

            foreach (FtbModInfo modIndo in modsInfo?.Mods)
            {
                string path = _versionIsolation ? Path.Combine(Path.Combine(_gameDir, "versions", versionId,"mods"), modIndo.FileName) : Path.Combine(Path.Combine(_gameDir,"mods"), modIndo.FileName);

                if (!fileInfoMap.TryGetValue(modIndo.FileId, out var info)
                    || string.IsNullOrEmpty(info.DownloadUrl))
                {
                    System.Diagnostics.Trace.WriteLine($"[FTB] 跳过 Mod {modIndo.Name} (ID={modIndo.ModId}, FileID={modIndo.FileId}): 无下载链接或已下架");
                    continue;
                }

                missFiles.Add(new MissFileData(modIndo.Name, path, info.DownloadUrl, info.Sha1 ?? ""));
            }

            Console.WriteLine($"[FTB] 清单解析完成，共 {missFiles.Count} 个文件需要下载");
            return missFiles;
        }
    }
}
