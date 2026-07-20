using Qomicex.Core.AOT.Public.Services;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Qomicex.Core.AOT.Services.Options;

internal sealed class ServerManager : IServerManager
{
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSpecific;

    public ServerManager(string gameDirectory, string version, bool versionSpecific)
    {
        _gameDirectory = gameDirectory ?? throw new ArgumentNullException(nameof(gameDirectory));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        _versionSpecific = versionSpecific;
    }

    #region 服务器列表持久化

    public List<ServerEntry> LoadServerList()
    {
        var serverFilePath = GetServerFilePath();
        if (!File.Exists(serverFilePath))
        {
            return new List<ServerEntry>();
        }

        try
        {
            using var fileStream = File.OpenRead(serverFilePath);
            using var dataStream = CreateReadStream(fileStream);
            var root = NbtIO.Read(dataStream);

            if (!root.TryGetValue("servers", out var serversTag))
            {
                return new List<ServerEntry>();
            }

            if (serversTag is not List<NbtCompound> compounds)
            {
                throw new InvalidDataException("The 'servers' tag is not a compound list.");
            }

            return compounds.Select(ToServerEntry).ToList();
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"Failed to read Minecraft servers file '{serverFilePath}': {ex.Message}", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to read Minecraft servers file '{serverFilePath}': {ex.Message}", ex);
        }
    }

    public void SaveServerList(IReadOnlyList<ServerEntry> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);

        var serverFilePath = GetServerFilePath();
        var directory = Path.GetDirectoryName(serverFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = File.Create(serverFilePath);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
        NbtIO.Write(gzipStream, new NbtCompound(StringComparer.Ordinal)
        {
            ["servers"] = servers.Select(ToNbtCompound).ToList()
        });
    }

    public void AddOrUpdateServer(ServerEntry server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var servers = LoadServerList();
        var index = servers.FindIndex(existing => string.Equals(existing.Address, server.Address, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            servers[index] = Clone(server);
        }
        else
        {
            servers.Add(Clone(server));
        }

        SaveServerList(servers);
    }

    public bool RemoveServer(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var servers = LoadServerList();
        var removed = servers.RemoveAll(server => string.Equals(server.Address, address, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            SaveServerList(servers);
        }

        return removed;
    }

    public ServerEntry? GetServer(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return LoadServerList()
            .FirstOrDefault(server => string.Equals(server.Address, address, StringComparison.OrdinalIgnoreCase));
    }

    public bool ServerFileExists()
    {
        return File.Exists(GetServerFilePath());
    }

    public void ClearServers()
    {
        SaveServerList(Array.Empty<ServerEntry>());
    }

    public string GetServerFilePath()
    {
        if (_versionSpecific)
        {
            return Path.Combine(_gameDirectory, "versions", _version, "servers.dat");
        }

        return Path.Combine(_gameDirectory, "servers.dat");
    }

    #endregion

    #region 服务器状态查询

    public ServerState? GetServerStateByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var server = LoadServerList()
            .FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));

        return server is null ? null : GetServerStateByAddress(server.Address);
    }

    public ServerState GetServerStateByAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var state = new ServerState
        {
            Address = address,
            Name = TryGetServerNameByAddress(address)
        };

        var endpoint = ResolveStatusEndpoint(address);
        var tcpConnected = false;

        try
        {
            return QueryModernServerState(endpoint, state, out tcpConnected);
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException or JsonException or FormatException or TimeoutException)
        {
            if (tcpConnected && ShouldFallbackToLegacy(ex))
            {
                return QueryLegacyServerState(endpoint, state, ex.Message);
            }

            state.IsOnline = false;
            state.ErrorMessage = ex.Message;
            return state;
        }
    }

    public async Task<ServerState?> PingAsync(string host, int port, CancellationToken ct)
    {
        var state = new ServerState
        {
            Address = host,
            Name = TryGetServerNameByAddress(host)
        };

        try
        {
            return await QueryModernServerStateAsync(host, port, state, ct);
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException or JsonException or FormatException or TimeoutException or OperationCanceledException)
        {
            state.IsOnline = false;
            state.ErrorMessage = ex.Message;
            return state;
        }
    }

    public async Task<ServerState?> PingAsync(ServerEntry entry, CancellationToken ct)
    {
        return await PingAsync(entry.Address, 25565, ct);
    }

    #endregion

    #region 局域网发现

    public IReadOnlyList<LanServerEntry> DiscoverLanServers(TimeSpan timeout)
    {
        using var cancellationTokenSource = new CancellationTokenSource(timeout);
        var entries = new List<LanServerEntry>();

        try
        {
            var enumerator = DiscoverLanAsync(cancellationTokenSource.Token).GetAsyncEnumerator(cancellationTokenSource.Token);
            try
            {
                while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    entries.Add(enumerator.Current);
                }
            }
            finally
            {
                enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (OperationCanceledException)
        {
        }

        return entries;
    }

    public async IAsyncEnumerable<LanServerEntry> DiscoverLanAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UdpClient? client = null;

        try
        {
            client = CreateLanDiscoveryClient();
        }
        catch (SocketException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }

        using (client)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (!cancellationToken.IsCancellationRequested)
            {
                UdpReceiveResult result;

                try
                {
                    result = await client.ReceiveAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (SocketException)
                {
                    yield break;
                }

                var payload = Encoding.UTF8.GetString(result.Buffer);
                var entry = ParseLanBroadcast(payload, result.RemoteEndPoint.Address.ToString());
                if (entry is null)
                {
                    continue;
                }

                var key = $"{entry.Address}|{entry.Port}|{entry.Motd}";
                if (seen.Add(key))
                {
                    yield return entry;
                }
            }
        }
    }

    #endregion

    #region NBT 转换

    private static ServerEntry ToServerEntry(NbtCompound compound)
    {
        return new ServerEntry
        {
            Name = NbtIO.GetOptionalString(compound, "name") ?? string.Empty,
            Address = NbtIO.GetOptionalString(compound, "ip") ?? string.Empty,
            IconBase64 = NbtIO.GetOptionalString(compound, "icon"),
            AcceptTextures = NbtIO.GetOptionalBool(compound, "acceptTextures")
        };
    }

    private static NbtCompound ToNbtCompound(ServerEntry server)
    {
        var compound = new NbtCompound(StringComparer.Ordinal)
        {
            ["name"] = server.Name ?? string.Empty,
            ["ip"] = server.Address ?? string.Empty,
            ["acceptTextures"] = server.AcceptTextures
        };

        if (!string.IsNullOrEmpty(server.IconBase64))
        {
            compound["icon"] = server.IconBase64;
        }

        return compound;
    }

    private static ServerEntry Clone(ServerEntry server)
    {
        return new ServerEntry
        {
            Name = server.Name,
            Address = server.Address,
            IconBase64 = server.IconBase64,
            AcceptTextures = server.AcceptTextures
        };
    }

    #endregion

    #region 地址解析与 SRV

    private static (string Host, ushort Port) ParseServerAddress(string address)
    {
        var trimmed = address.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new FormatException("Server address cannot be empty.");
        }

        const ushort defaultPort = 25565;

        if (trimmed.StartsWith('['))
        {
            var closingIndex = trimmed.IndexOf(']');
            if (closingIndex < 0)
            {
                throw new FormatException("IPv6 server address is missing a closing bracket.");
            }

            var host = trimmed[1..closingIndex];
            if (closingIndex == trimmed.Length - 1)
            {
                return (host, defaultPort);
            }

            if (trimmed[closingIndex + 1] != ':')
            {
                throw new FormatException("IPv6 server address must use [host]:port format.");
            }

            return (host, ParsePort(trimmed[(closingIndex + 2)..]));
        }

        var firstColonIndex = trimmed.IndexOf(':');
        var lastColonIndex = trimmed.LastIndexOf(':');
        if (firstColonIndex >= 0 && firstColonIndex == lastColonIndex)
        {
            return (trimmed[..firstColonIndex], ParsePort(trimmed[(firstColonIndex + 1)..]));
        }

        return (trimmed, defaultPort);
    }

    private static ushort ParsePort(string text)
    {
        if (!ushort.TryParse(text, out var port))
        {
            throw new FormatException($"Invalid server port '{text}'.");
        }

        return port;
    }

    public async Task<string?> ResolveSrvAsync(string host, CancellationToken ct)
    {
        var result = await ResolveSrvInternalAsync(host, ct);
        return result?.Target;
    }

    private StatusEndpoint ResolveStatusEndpoint(string address)
    {
        var (host, port) = ParseServerAddress(address);
        if (port != 25565 || IPAddress.TryParse(host, out _))
        {
            return new StatusEndpoint(host, port, host);
        }

        try
        {
            var result = ResolveSrvInternalAsync(host, CancellationToken.None).GetAwaiter().GetResult();
            if (result is null)
            {
                return new StatusEndpoint(host, port, host);
            }

            var target = result.Value.Target;
            if (string.IsNullOrWhiteSpace(target))
            {
                return new StatusEndpoint(host, port, host);
            }

            return new StatusEndpoint(target, result.Value.Port, host);
        }
        catch
        {
            return new StatusEndpoint(host, port, host);
        }
    }

    private static async Task<(string Target, ushort Port)?> ResolveSrvInternalAsync(string host, CancellationToken ct)
    {
        var queryName = EncodeDnsName($"_minecraft._tcp.{host}");
        var query = BuildDnsQuery(queryName);
        byte[] response;

        try
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 3000;
            udp.Client.SendTimeout = 3000;

            var dnsServers = GetDnsServers();
            foreach (var dnsServer in dnsServers)
            {
                await udp.SendAsync(query, query.Length, dnsServer);
            }

            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(ct);
            }
            catch (SocketException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }

            response = result.Buffer;
        }
        catch
        {
            return null;
        }

        return ParseDnsSrvResponse(response);
    }

    private static List<IPEndPoint> GetDnsServers()
    {
        var servers = new List<IPEndPoint>();
        var dnsAddresses = new List<IPAddress>();

        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
            {
                continue;
            }

            var props = ni.GetIPProperties();
            if (props is null)
            {
                continue;
            }

            foreach (var dns in props.DnsAddresses)
            {
                if (!dnsAddresses.Contains(dns))
                {
                    dnsAddresses.Add(dns);
                }
            }
        }

        foreach (var addr in dnsAddresses)
        {
            servers.Add(new IPEndPoint(addr, 53));
        }

        if (servers.Count == 0)
        {
            servers.Add(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53));
        }

        return servers;
    }

    private static int _dnsQueryId = new Random().Next(1, 65535);

    private static byte[] BuildDnsQuery(byte[] queryName)
    {
        var id = (ushort)(Interlocked.Increment(ref _dnsQueryId) & 0xFFFF);
        var query = new byte[12 + queryName.Length + 4];
        query[0] = (byte)(id >> 8);
        query[1] = (byte)(id & 0xFF);
        query[5] = 1;
        Array.Copy(queryName, 0, query, 12, queryName.Length);
        query[^4] = 0;
        query[^3] = 33;
        query[^2] = 0;
        query[^1] = 1;
        return query;
    }

    private static byte[] EncodeDnsName(string name)
    {
        var labels = name.Split('.');
        var writer = new MemoryStream();
        foreach (var label in labels)
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            writer.WriteByte((byte)bytes.Length);
            writer.Write(bytes);
        }
        writer.WriteByte(0);
        return writer.ToArray();
    }

    private static (string Target, ushort Port)? ParseDnsSrvResponse(byte[] response)
    {
        if (response.Length < 12)
        {
            return null;
        }

        var header = response.AsSpan(0, 12);
        var anCount = (ushort)((header[6] << 8) | header[7]);

        var offset = 12;

        while (offset < response.Length && response[offset] != 0)
        {
            if ((response[offset] & 0xC0) == 0xC0)
            {
                offset += 2;
                break;
            }
            offset += response[offset] + 1;
        }

        if (response[offset] == 0)
        {
            offset++;
        }

        offset += 4;

        for (var i = 0; i < anCount; i++)
        {
            if (offset >= response.Length)
            {
                break;
            }

            if ((response[offset] & 0xC0) == 0xC0)
            {
                offset += 2;
            }
            else
            {
                while (offset < response.Length && response[offset] != 0)
                {
                    offset += response[offset] + 1;
                }
                offset++;
            }

            if (offset + 10 > response.Length)
            {
                break;
            }

            var ansType = (ushort)((response[offset] << 8) | response[offset + 1]);
            var rdLength = (ushort)((response[offset + 8] << 8) | response[offset + 9]);
            offset += 10;

            if (ansType == 33 && rdLength >= 6)
            {
                var port = (ushort)((response[offset + 4] << 8) | response[offset + 5]);
                var target = DecodeDnsName(response, offset + 6);
                return (target, port);
            }

            offset += rdLength;
        }

        return null;
    }

    private static string DecodeDnsName(byte[] message, int offset)
    {
        var labels = new List<string>();
        var jumped = false;
        var originalOffset = offset;

        while (offset < message.Length && message[offset] != 0)
        {
            if ((message[offset] & 0xC0) == 0xC0)
            {
                if (!jumped)
                {
                    originalOffset = offset + 2;
                    jumped = true;
                }

                var pointer = ((message[offset] & 0x3F) << 8) | message[offset + 1];
                offset = pointer;
                continue;
            }

            var length = message[offset];
            offset++;
            if (offset + length > message.Length)
            {
                break;
            }

            labels.Add(Encoding.ASCII.GetString(message, offset, length));
            offset += length;
        }

        if (jumped)
        {
            offset = originalOffset;
        }

        return string.Join(".", labels);
    }

    #endregion

    #region Minecraft 协议通信

    private static ServerState QueryModernServerState(StatusEndpoint endpoint, ServerState state, out bool tcpConnected)
    {
        using var client = new TcpClient();
        client.ReceiveTimeout = 5000;
        client.SendTimeout = 5000;
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        client.ConnectAsync(endpoint.ConnectHost, endpoint.ConnectPort, connectCts.Token)
            .GetAwaiter()
            .GetResult();

        tcpConnected = true;

        using var stream = client.GetStream();
        SendHandshake(stream, endpoint.HandshakeHost, endpoint.ConnectPort);
        SendStatusRequest(stream);

        using var responseDocument = ReadStatusResponse(stream);
        PopulateStateFromResponse(state, responseDocument.RootElement);

        state.Ping = MeasurePing(stream);
        state.IsOnline = true;
        state.ErrorMessage = string.Empty;
        return state;
    }

    private static async Task<ServerState> QueryModernServerStateAsync(string host, int port, ServerState state, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(host, port, timeoutCts.Token);

        using var stream = client.GetStream();
        SendHandshake(stream, host, (ushort)port);
        SendStatusRequest(stream);

        using var responseDocument = ReadStatusResponse(stream);
        PopulateStateFromResponse(state, responseDocument.RootElement);

        state.Ping = MeasurePing(stream);
        state.IsOnline = true;
        state.ErrorMessage = string.Empty;
        return state;
    }

    private static bool ShouldFallbackToLegacy(Exception exception)
    {
        return exception is InvalidDataException or JsonException or EndOfStreamException;
    }

    private static ServerState QueryLegacyServerState(StatusEndpoint endpoint, ServerState state, string priorError)
    {
        try
        {
            var response = QueryLegacyPingHost(endpoint);
            PopulateStateFromLegacyResponse(state, response);
            state.IsOnline = true;
            state.ErrorMessage = string.Empty;
            return state;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or SocketException or TimeoutException)
        {
            try
            {
                var response = QueryLegacyFe01(endpoint);
                PopulateStateFromLegacyResponse(state, response);
                state.IsOnline = true;
                state.ErrorMessage = string.Empty;
                return state;
            }
            catch (Exception fallbackEx) when (fallbackEx is InvalidDataException or IOException or SocketException or TimeoutException)
            {
                state.IsOnline = false;
                state.ErrorMessage = string.IsNullOrWhiteSpace(fallbackEx.Message) ? priorError : fallbackEx.Message;
                return state;
            }
        }
    }

    private static LegacyServerResponse QueryLegacyPingHost(StatusEndpoint endpoint)
    {
        using var client = CreateStatusClient(endpoint);
        using var stream = client.GetStream();
        SendLegacyPingHostRequest(stream, endpoint.HandshakeHost, endpoint.ConnectPort);
        return ReadLegacyResponse(stream);
    }

    private static LegacyServerResponse QueryLegacyFe01(StatusEndpoint endpoint)
    {
        using var client = CreateStatusClient(endpoint);
        using var stream = client.GetStream();
        stream.WriteByte(0xFE);
        stream.WriteByte(0x01);
        return ReadLegacyResponse(stream);
    }

    private static TcpClient CreateStatusClient(StatusEndpoint endpoint)
    {
        var client = new TcpClient();
        try
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            client.ConnectAsync(endpoint.ConnectHost, endpoint.ConnectPort, connectCts.Token)
                .GetAwaiter()
                .GetResult();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static void SendLegacyPingHostRequest(Stream stream, string host, ushort port)
    {
        var hostBytes = Encoding.BigEndianUnicode.GetBytes(host);
        var payloadLength = (ushort)(7 + hostBytes.Length);

        stream.WriteByte(0xFE);
        stream.WriteByte(0x01);
        stream.WriteByte(0xFA);
        WriteUInt16BigEndian(stream, 11);
        stream.Write(Encoding.BigEndianUnicode.GetBytes("MC|PingHost"));
        WriteUInt16BigEndian(stream, payloadLength);
        stream.WriteByte(127);
        WriteUInt16BigEndian(stream, (ushort)host.Length);
        stream.Write(hostBytes);
        WriteInt32BigEndian(stream, port);
    }

    private static LegacyServerResponse ReadLegacyResponse(Stream stream)
    {
        var header = stream.ReadByte();
        if (header != 0xFF)
        {
            throw new InvalidDataException($"Unexpected legacy response header {header}.");
        }

        var length = ReadUInt16BigEndian(stream);
        var bytes = new byte[length * 2];
        ReadExactly(stream, bytes, "Legacy server response");
        var text = Encoding.BigEndianUnicode.GetString(bytes);

        return ParseLegacyResponse(text);
    }

    private static LegacyServerResponse ParseLegacyResponse(string text)
    {
        if (text.StartsWith("§1\0", StringComparison.Ordinal))
        {
            var parts = text.Split('\0');
            if (parts.Length < 6)
            {
                throw new InvalidDataException("Legacy server response does not contain all expected fields.");
            }

            return new LegacyServerResponse(
                parts[3],
                ParseLegacyPlayerCount(parts[4], "online players"),
                ParseLegacyPlayerCount(parts[5], "max players"),
                parts[2]);
        }

        var segments = text.Split('§');
        if (segments.Length < 3)
        {
            throw new InvalidDataException("Legacy server response format is not recognized.");
        }

        return new LegacyServerResponse(
            segments[0],
            ParseLegacyPlayerCount(segments[1], "online players"),
            ParseLegacyPlayerCount(segments[2], "max players"),
            string.Empty);
    }

    private static int ParseLegacyPlayerCount(string text, string valueName)
    {
        if (!int.TryParse(text, out var value))
        {
            throw new InvalidDataException($"Legacy server response contains an invalid {valueName} value '{text}'.");
        }

        return value;
    }

    private static void PopulateStateFromLegacyResponse(ServerState state, LegacyServerResponse response)
    {
        state.Description = response.Motd;
        state.OnlinePlayers = response.OnlinePlayers;
        state.MaxPlayers = response.MaxPlayers;
        state.Version = response.VersionName;
        state.Ping = 0;
    }

    private static void SendHandshake(Stream stream, string host, ushort port)
    {
        using var packetStream = new MemoryStream();
        WriteVarInt(packetStream, 0);
        WriteVarInt(packetStream, 47);
        WriteString(packetStream, host);
        WriteUInt16BigEndian(packetStream, port);
        WriteVarInt(packetStream, 1);
        WritePacket(stream, packetStream.ToArray());
    }

    private static void SendStatusRequest(Stream stream)
    {
        using var packetStream = new MemoryStream();
        WriteVarInt(packetStream, 0);
        WritePacket(stream, packetStream.ToArray());
    }

    private static JsonDocument ReadStatusResponse(Stream stream)
    {
        _ = ReadVarInt(stream);
        var packetId = ReadVarInt(stream);
        if (packetId != 0)
        {
            throw new InvalidDataException($"Unexpected status response packet id {packetId}.");
        }

        var json = ReadString(stream);
        return JsonDocument.Parse(json);
    }

    private static long MeasurePing(Stream stream)
    {
        using var packetStream = new MemoryStream();
        WriteVarInt(packetStream, 1);
        var timestamp = Stopwatch.GetTimestamp();
        Span<byte> payload = stackalloc byte[sizeof(long)];
        BitConverter.TryWriteBytes(payload, timestamp);
        if (BitConverter.IsLittleEndian)
        {
            payload.Reverse();
        }

        packetStream.Write(payload);

        var stopwatch = Stopwatch.StartNew();
        WritePacket(stream, packetStream.ToArray());

        _ = ReadVarInt(stream);
        var packetId = ReadVarInt(stream);
        if (packetId != 1)
        {
            throw new InvalidDataException($"Unexpected pong response packet id {packetId}.");
        }

        _ = ReadInt64BigEndian(stream);
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    private static void PopulateStateFromResponse(ServerState state, JsonElement response)
    {
        if (response.TryGetProperty("version", out var version) && version.TryGetProperty("name", out var versionName))
        {
            state.Version = versionName.GetString() ?? string.Empty;
        }

        if (response.TryGetProperty("players", out var players))
        {
            if (players.TryGetProperty("online", out var onlinePlayers) && onlinePlayers.TryGetInt32(out var online))
            {
                state.OnlinePlayers = online;
            }

            if (players.TryGetProperty("max", out var maxPlayers) && maxPlayers.TryGetInt32(out var max))
            {
                state.MaxPlayers = max;
            }
        }

        if (response.TryGetProperty("description", out var description))
        {
            state.Description = FlattenDescription(description).Trim();
        }
    }

    private static string FlattenDescription(JsonElement description)
    {
        return description.ValueKind switch
        {
            JsonValueKind.String => description.GetString() ?? string.Empty,
            JsonValueKind.Object => FlattenDescriptionObject(description),
            JsonValueKind.Array => string.Concat(description.EnumerateArray().Select(FlattenDescription)),
            _ => string.Empty
        };
    }

    private static string FlattenDescriptionObject(JsonElement description)
    {
        var builder = new StringBuilder();

        if (description.TryGetProperty("text", out var text))
        {
            builder.Append(text.GetString());
        }

        if (description.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in extra.EnumerateArray())
            {
                builder.Append(FlattenDescription(element));
            }
        }

        return builder.ToString();
    }

    #endregion

    #region 局域网发现辅助

    private static UdpClient CreateLanDiscoveryClient()
    {
        var client = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.ExclusiveAddressUse = false;
            client.Client.Bind(new IPEndPoint(IPAddress.Any, 4445));
            client.JoinMulticastGroup(IPAddress.Parse("224.0.2.60"));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static LanServerEntry? ParseLanBroadcast(string payload, string sourceAddress)
    {
        var motd = ExtractLanTag(payload, "MOTD") ?? "missing no";
        var portText = ExtractLanTag(payload, "AD");
        if (!int.TryParse(portText, out var port))
        {
            return null;
        }

        return new LanServerEntry
        {
            Motd = motd,
            Address = sourceAddress,
            Port = port,
            DisplayAddress = $"{sourceAddress}:{port}"
        };
    }

    private static string? ExtractLanTag(string payload, string tagName)
    {
        var startTag = $"[{tagName}]";
        var endTag = $"[/{tagName}]";
        var startIndex = payload.IndexOf(startTag, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return null;
        }

        startIndex += startTag.Length;
        var endIndex = payload.IndexOf(endTag, startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return null;
        }

        return payload[startIndex..endIndex];
    }

    #endregion

    #region 二进制读写辅助

    private string TryGetServerNameByAddress(string address)
    {
        try
        {
            return LoadServerList()
                .FirstOrDefault(server => string.Equals(server.Address, address, StringComparison.OrdinalIgnoreCase))?.Name
                ?? string.Empty;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static Stream CreateReadStream(FileStream fileStream)
    {
        Span<byte> header = stackalloc byte[2];
        var read = fileStream.Read(header);
        fileStream.Position = 0;

        var isGzip = read == 2 && header[0] == 0x1F && header[1] == 0x8B;
        if (isGzip)
        {
            return new GZipStream(fileStream, CompressionMode.Decompress);
        }

        return fileStream;
    }

    private static int ReadVarInt(Stream stream)
    {
        var value = 0;
        var position = 0;

        while (true)
        {
            var currentByte = stream.ReadByte();
            if (currentByte < 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading VarInt.");
            }

            value |= (currentByte & 0x7F) << position;
            if ((currentByte & 0x80) == 0)
            {
                return value;
            }

            position += 7;
            if (position >= 35)
            {
                throw new InvalidDataException("VarInt is too large.");
            }
        }
    }

    private static string ReadString(Stream stream)
    {
        var length = ReadVarInt(stream);
        if (length < 0)
        {
            throw new InvalidDataException("String length cannot be negative.");
        }

        var bytes = new byte[length];
        ReadExactly(stream, bytes, "String");
        return Encoding.UTF8.GetString(bytes);
    }

    private static long ReadInt64BigEndian(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        ReadExactly(stream, bytes, "Int64");

        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        return BitConverter.ToInt64(bytes);
    }

    private static ushort ReadUInt16BigEndian(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        ReadExactly(stream, bytes, "UInt16");

        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        return BitConverter.ToUInt16(bytes);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer, string valueName)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of stream while reading {valueName}.");
            }

            totalRead += read;
        }
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var unsignedValue = unchecked((uint)value);

        do
        {
            var currentByte = (byte)(unsignedValue & 0x7F);
            unsignedValue >>= 7;
            if (unsignedValue != 0)
            {
                currentByte |= 0x80;
            }

            stream.WriteByte(currentByte);
        }
        while (unsignedValue != 0);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WritePacket(Stream stream, byte[] payload)
    {
        WriteVarInt(stream, payload.Length);
        stream.Write(payload);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        stream.Write(bytes);
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        stream.Write(bytes);
    }

    #endregion

    #region 内部类型

    private readonly record struct StatusEndpoint(string ConnectHost, ushort ConnectPort, string HandshakeHost);

    private readonly record struct LegacyServerResponse(string Motd, int OnlinePlayers, int MaxPlayers, string VersionName);

    #endregion
}
