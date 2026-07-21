namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public sealed record VersionFilesRequest(List<string> Hashes, string Algorithm = "sha1");
