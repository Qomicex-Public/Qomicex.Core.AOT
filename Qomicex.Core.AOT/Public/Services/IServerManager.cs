using Qomicex.Core.AOT.Services.Options;
using System.Runtime.CompilerServices;

namespace Qomicex.Core.AOT.Public.Services;

public interface IServerManager
{
    List<ServerEntry> LoadServerList();
    void SaveServerList(IReadOnlyList<ServerEntry> servers);
    void AddOrUpdateServer(ServerEntry server);
    bool RemoveServer(string address);
    ServerEntry? GetServer(string address);
    bool ServerFileExists();
    void ClearServers();
    string GetServerFilePath();
    ServerState? GetServerStateByName(string name);
    ServerState GetServerStateByAddress(string address);
    Task<ServerState?> PingAsync(string host, int port, CancellationToken ct);
    Task<ServerState?> PingAsync(ServerEntry entry, CancellationToken ct);
    IReadOnlyList<LanServerEntry> DiscoverLanServers(TimeSpan timeout);
    IAsyncEnumerable<LanServerEntry> DiscoverLanAsync([EnumeratorCancellation] CancellationToken ct = default);
    Task<string?> ResolveSrvAsync(string host, CancellationToken ct);
}
