using Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;
using Qomicex.Core.AOT.Services.Expansion.CurseForge;
using Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;
using System;
using System.Collections.Generic;
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

        Task<List<MissFileData>> IInstaller.GetMissLibrariesAsync(string versionId, string packId, string packVersionId)
        {
            var missFiles = new List<MissFileData>();
            int.TryParse(packId, out int _packId);
            int.TryParse(packVersionId, out int _packVersionId);

            var json = _ftb.GetVersionDetailAsync(_packId, _packVersionId).Result;

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

            ModsDetail modsInfo = _ftb.GetModDetailAsync(_packId, _packVersionId).Result;

            if (json is null)
                throw new Exception("无法获取整合包信息");

            var _cf = new Services.Expansion.CurseForge.Mods(_httpClient,_cfApiKety);

            foreach (FtbModInfo modIndo in modsInfo?.Mods)
            {
                string path = _versionIsolation ? Path.Combine(Path.Combine(_gameDir, "versions", versionId,"mods"), modIndo.FileName) : Path.Combine(Path.Combine(_gameDir,"mods"), modIndo.FileName);
                string url = _cf.GetDownloadUrlAsync(modIndo.ModId.ToString(), modIndo.FileId.ToString()).Result;
                string sha1 = "";
                string name = modIndo.Name;
                missFiles.Add(new MissFileData(name, path, url, sha1));
            }

            return Task.FromResult(missFiles);
        }
    }
}
