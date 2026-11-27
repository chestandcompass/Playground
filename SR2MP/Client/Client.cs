using System.Buffers;
using System.Net;
using System.Net.Sockets;
using JetBrains.Annotations;
using SR2MP.Api;
using SR2MP.Client.Managers;
using SR2MP.Client.Models;
using SR2MP.Components.UI;
using SR2MP.Packets.Api;
using SR2MP.Packets.Internal;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using SR2MP.Shared.Transport;

namespace SR2MP.Client;

/// <summary>
/// Provides the main client interface.
/// </summary>
public sealed class SR2MPClient
{
    private IDatagramTransport? transport;
    private IPEndPoint? serverEndPoint;
    private System.Threading.Thread? receiveThread;
    private ReliabilityManager? reliabilityManager;
    private long epoch;
    internal long SessionEpoch => Interlocked.Read(ref epoch);

    private volatile bool isConnected;
    private volatile bool isConnecting;
    private volatile bool connectionAcknowledged;
    private Timer? connectionTimeoutTimer;
    private const int ConnectionTimeoutSeconds = 10;
    private long lastHostPacket;
    internal double HostSilenceSeconds => Math.Max(0, Environment.TickCount64 - Interlocked.Read(ref lastHostPacket)) / 1000d;

    private bool shownConnectionError;
    private bool subscribedToQuitting;

    private readonly ClientPacketManager packetManager;

    /// <summary>
    /// Gets a value indicating the connection status of the client.
    /// </summary>
    public bool IsConnected => isConnected;
    
    /// <summary>
    /// Gets a value indicating the connection status of the client.
    /// </summary>
    public bool IsConnecting => isConnecting;

    /// <summary>
    /// Gets a value indicating whether the client is currently fully in a world
    /// </summary>
    internal bool IsJoined => isConnected && connectionAcknowledged;

    /// <summary>
    /// Gets a value indicating the client's identifier.
    /// </summary>
    public string PlayerId { get; private set; } = string.Empty;

    /// <summary>
    /// Occurs when the client successfully connects to the server.
    /// </summary>
    /// <remarks>The parameter typically provides the connection details or the assigned client ID.</remarks>
    public event Action<string>? OnConnected;

    /// <summary>
    /// Occurs when the client is disconnected from the server.
    /// </summary>
    public event Action? OnDisconnected;

    /// <summary>
    /// Occurs when a new remote player joins the multiplayer session.
    /// </summary>
    /// <remarks>The parameter provides the unique identifier of the player who joined.</remarks>
    public event Action<string>? OnPlayerJoined;

    /// <summary>
    /// Occurs when a remote player leaves the multiplayer session.
    /// </summary>
    /// <remarks>The parameter provides the unique identifier of the player who left.</remarks>
    public event Action<string>? OnPlayerLeft;

    /// <summary>
    /// Occurs when a remote player's state (such as position, rotation, or animation) is updated.
    /// </summary>
    /// <remarks>The first parameter is the player's unique ID, and the second is their updated <see cref="RemotePlayer"/> data.</remarks>
    public event Action<string, RemotePlayer>? OnPlayerUpdate;

    internal SR2MPClient()
    {
        packetManager = new ClientPacketManager(this);

        PlayerManager.OnPlayerAdded += playerId => OnPlayerJoined?.Invoke(playerId);
        PlayerManager.OnPlayerRemoved += playerId => OnPlayerLeft?.Invoke(playerId);
        PlayerManager.OnPlayerUpdated += (playerId, player) => OnPlayerUpdate?.Invoke(playerId, player);
    }

    internal void Connect(string serverIp, int port, IDatagramTransport? prepared = null)
    {
        if (!CoopSaveManager.CanNetwork("guest")) return;
        if (Main.Server.IsRunning)
        {
            SrLogger.LogWarning("You can not join a world while hosting a server.");
            return;
        }

        if (isConnected)
        {
            SrLogger.LogWarning("You are already connected to a Server!");
            return;
        }
        
        if (isConnecting)
        {
            SrLogger.LogWarning("You are already trying to connect to a Server!");
            return;
        }

        if (serverIp == "127.0.0.1" && !DevMode)
        {
            SrLogger.LogWarning("You can not connect to this IP!");
            SrLogger.LogWarning("If you want to connect to someone on your local network, use their local IP!");
            SrLogger.LogWarning("To get the local IP, check your routers Website or use the 'ìpconfig' command prompt command!");
            return;
        }
        
        SessionGuard.BeginConnect();
        Interlocked.Exchange(ref lastHostPacket, Environment.TickCount64);
        isConnecting = true;

        try
        {
            var parsedIp = IPAddress.Parse(serverIp);

            if (prepared != null) transport = prepared;
            else if (parsedIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (!Socket.OSSupportsIPv6)
                {
                    SrLogger.LogError("IPv6 is not supported on this machine! Please enable IPv6 or use an IPv4 address.");
                    throw new NotSupportedException("IPv6 is not available on this system");
                }

                transport = new UdpDatagramTransport(new UdpClient(AddressFamily.InterNetworkV6));
                SrLogger.LogMessage("Using IPv6 connection");
            }
            else
            {
                transport = new UdpDatagramTransport(new UdpClient(AddressFamily.InterNetwork));
                SrLogger.LogMessage("Using IPv4 connection");
            }

            PacketDeduplication.Clear();

            serverEndPoint = new IPEndPoint(parsedIp, port);
            // UDP's connected socket rejects unexpected senders. Relays authenticate
            // their single virtual peer before a transport is passed into this method.
            if (transport is UdpDatagramTransport udp) udp.Connect(serverEndPoint);

            PlayerId = PlayerIdGenerator.GeneratePersistentPlayerId();

            var session = transport!;
            var generation = Interlocked.Increment(ref epoch);
            reliabilityManager = new ReliabilityManager((data, endpoint) =>
            {
                if (generation == SessionEpoch && data.Array != null) session.Send(data, endpoint);
            });
            reliabilityManager.Start();

            packetManager.RegisterHandlers(Main.Core);

            isConnected = true;
            connectionAcknowledged = false;

            receiveThread = new System.Threading.Thread(()=>ReceiveLoop(session,generation))
            {
                IsBackground = true
            };
            receiveThread.Start();

            connectionTimeoutTimer = new Timer(CheckConnectionTimeout, generation,
                TimeSpan.FromSeconds(ConnectionTimeoutSeconds), Timeout.InfiniteTimeSpan);

            if (!subscribedToQuitting)
            {
                subscribedToQuitting = true;
                Application.quitting += new Action(Disconnect);
            }

            var connectPacket = new ConnectPacket
            {
                PlayerId = PlayerId,
                Username = Main.Username
            };

            SendPacket(connectPacket);

            SrLogger.LogMessage("Connecting to the Server...",
                $"Connecting to {serverIp}:{port} as {PlayerId}...");
        }
        catch (Exception ex)
        {
            transport?.Dispose(); transport = null;
            SrLogger.LogError($"Error connecting to the Server: {ex}");
            connectionAcknowledged = false;
            isConnected = false;
            isConnecting = false;
            throw;
        }
    }

    private void CheckConnectionTimeout(object? state)
    {
        if(state is not long generation || generation!=SessionEpoch) return;
        if (connectionAcknowledged || !isConnected)
            return;

        SrLogger.LogError("Connection timeout: Server did not respond within 10 seconds");

        isConnecting = false;
        DisconnectSession(generation);
    }

    internal void UpdateConnectionStatus(bool state) => isConnected = state;

    private void ReceiveLoop(IDatagramTransport session,long generation)
    {
        if (session == null)
        {
            SrLogger.LogError("UDP client is null in ReceiveLoop!");
            return;
        }

        SrLogger.LogMessage("Client ReceiveLoop started!");

        EndPoint remoteEp = NetworkEndpoints.ReceiveEndpoint(session.AddressFamily);

        var receiveBuffer = ArrayPool<byte>.Shared.Rent(2048);

        try
        {
            while (isConnected && ReferenceEquals(transport, session))
            {
                try
                {
                    var receivedBytes = session.Receive(receiveBuffer, ref remoteEp);
                    if (generation != SessionEpoch) break;

                    if (receivedBytes == 0)
                        continue;

                    Interlocked.Exchange(ref lastHostPacket, Environment.TickCount64);
                    packetManager.HandlePacket(receiveBuffer, receivedBytes, (IPEndPoint)remoteEp,generation);
                    SrLogger.LogPacketSize($"Received {receivedBytes} bytes",
                        $"Received {receivedBytes} bytes from {remoteEp}");
                }
                catch (SocketException ex)
                {
                    if (!ReferenceEquals(transport, session) || !isConnected) break;
                    // This prevents WSAEINTR from logging, this is correct
                    if (ex.ErrorCode is not 10004 and not 10054)
                    {
                        SrLogger.LogError($"ReceiveLoop error: Socket Exception:{ex.ErrorCode}\n{ex}");
                    }

                    if (ex.ErrorCode == 10054 && !shownConnectionError)
                    {
                        SrLogger.LogError("The server is not running!\n" +
                                          "If the server is running, there is something wrong with PlayIt or your tunnel service.\n" +
                                          "If this is not the case, check your firewall settings");
                        shownConnectionError = true;
                    }

                    MultiplayerUI.Instance.RegisterSystemMessage("Could not join the world, check the MelonLoader console for details", $"SYSTEM_JOIN_10054_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", MultiplayerUI.SystemMessageClose);
                    DisconnectSession(generation);
                }
                catch (IOException) when (session is RelayDatagramTransport)
                {
                    if (!isConnected || !ReferenceEquals(transport, session)) break;
                    SrLogger.LogMessage("Playground relay connection ended.");
                    DisconnectSession(generation);
                    break;
                }
                catch (Exception ex)
                {
                    if (!ReferenceEquals(transport, session) || !isConnected) break;
                    SrLogger.LogError($"ReceiveLoop error: {ex}");
                    DisconnectSession(generation);
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(receiveBuffer);
        }

        SrLogger.LogMessage("Client ReceiveLoop ended!");
    }

    internal void SendPacket<T>(T packet) where T : IPacket
        => PrepareAndSend(packet, packet.Reliability, (byte)packet.Type, packet.Channel, SerialiseInternalPacket<T>.Serialiser);

    /// <summary>
    /// Sends a packet over the network.
    /// </summary>
    /// <typeparam name="T">The type of the packet to send.</typeparam>
    /// <param name="data">The packet data to send.</param>
    [PublicApi]
    public void SendData<T>(T data) where T : ICustomPacket
    {
        if (!ApiHandlers.CurrentNetIdMapping2.TryGetValue(data.GetType(), out var modId))
        {
            SrLogger.LogWarning($"Cannot send API packet: No ModId registered for custom packet type {data.GetType().FullName}.");
            return;
        }

        var apiHeader = new ApiPacket(data.Reliability, data.Channel, modId);
        PrepareAndSend((apiHeader, data), apiHeader.Reliability, (byte)apiHeader.Type, apiHeader.Channel, SerialiseApiPacket<T>.Serialiser);
    }

    private void PrepareAndSend<T>(T state, PacketReliability reliability, byte packetType,
        NetworkChannel channel, PacketWriterDelegate<T> writeAction)
    {
        if (transport == null || serverEndPoint == null || !isConnected)
        {
            SrLogger.LogWarning("Cannot send packet: Not connected to a Server!");
            return;
        }

        using var writer = PacketWriter.Borrow();

        try
        {
            writeAction(writer, state);
            var data = writer.ToSpan();
            SrLogger.LogPacketSize($"Sending {data.Length} bytes to Server...");

            ushort sequenceNumber = 0;

            if (reliability.HasFlag(PacketReliability.Ordered))
                sequenceNumber = reliabilityManager?.GetNextSequenceNumber(channel, packetType, serverEndPoint) ?? 0;

            var splitResult = PacketChunkManager.SplitPacket(data, reliability, channel, sequenceNumber, out var packetId);

            if (reliability.HasFlag(PacketReliability.Reliable))
                reliabilityManager?.TrackPacket(splitResult, serverEndPoint, packetId, data[0], reliability);

            for (var i = 0; i < splitResult.Count; i++)
                SendRaw(splitResult.Chunks[i], serverEndPoint);

            if (!reliability.HasFlag(PacketReliability.Reliable))
                splitResult.Dispose();

            SrLogger.LogPacketSize($"Sent {data.Length} bytes to Server in {splitResult.Count} chunk(s) (ID={packetId}).");
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Failed to send packet: {ex}");
        }
    }

    // Sends raw data without reliability tracking (used for resends)
    private void SendRaw(ArraySegment<byte> data, IPEndPoint endPoint)
    {
        if (data.Array != null)
            transport?.Send(data, endPoint);
    }

    // Handle acknowledgement from server, used in client packet manager
    internal void HandleAck(IPEndPoint sender, ushort packetId, byte packetType)
        => reliabilityManager?.HandleAck(sender, packetId, packetType);

    internal bool ShouldProcessOrderedPacket(IPEndPoint sender, ushort sequenceNumber, byte packetType, NetworkChannel channel, PacketReliability reliability,
        Action? processAction = null)
            => reliabilityManager?.ShouldProcessOrderedPacket(sender, sequenceNumber, packetType, channel, reliability, processAction) ?? true;

    private void DisconnectSession(long generation)
    {
        if(MainThreadDispatcher.IsMainThread) {if(generation==SessionEpoch) Disconnect();}
        else MainThreadDispatcher.Instance?.Enqueue(()=>{if(generation==SessionEpoch) Disconnect();});
    }

    internal void Disconnect()
    {
        if (!MainThreadDispatcher.IsMainThread && MainThreadDispatcher.Instance is not null)
        { MainThreadDispatcher.Instance.Enqueue(Disconnect); return; }
        if (!isConnected)
            return;

        try
        {
            try { CoopSaveManager.SavePlayer(); }
            catch (Exception ex) { SrLogger.LogError("Could not checkpoint the guest profile before disconnect; retaining the previous snapshot: " + ex); }
            MultiplayerUI.Instance.ClearChatMessages();

            if (!shownConnectionError)
            {
                MultiplayerUI.Instance.RegisterSystemMessage(
                    "You disconnected from the world!",
                    $"SYSTEM_DISCONNECT_LOCAL_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    MultiplayerUI.SystemMessageDisconnect);
            }
            try
            {
                var leavePacket = new PlayerLeavePacket
                {
                    Type = PacketType.PlayerLeave,
                    PlayerId = PlayerId
                };

                SendPacket(leavePacket);
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Could not send leave packet: {ex.Message}");
            }

            PacketDeduplication.Clear();

            isConnected = false;
            Interlocked.Increment(ref epoch);
            isConnecting = false;
            connectionAcknowledged = false;

            connectionTimeoutTimer?.Dispose();
            connectionTimeoutTimer = null;

            reliabilityManager?.Stop();

            transport?.Close();
            transport = null;

            if (receiveThread is { IsAlive: true } && receiveThread != System.Threading.Thread.CurrentThread && !receiveThread.Join(250))
                SrLogger.LogWarning("Receive thread did not stop gracefully");

            receiveThread = null;

            foreach (var player in GetAllRemotePlayers())
            {
                var playerId = player.PlayerId;

                if (!PlayerObjects.TryGetValue(playerId, out var playerObject))
                    continue;

                if (playerObject)
                {
                    Object.Destroy(playerObject);
                    SrLogger.LogPacketSize($"Destroyed player object for {playerId}");
                }

                PlayerObjects.Remove(playerId);
            }

            PlayerManager.Clear();
            NetworkStringPool.Clear();
            ApiHandlers.ClearNetIds();

            SrLogger.LogMessage("Disconnected from server");
            InvokeDisconnected();
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Error during disconnect: {ex}");
        }
    }

    internal void NotifyConnected()
    {
        isConnecting = false;
        isConnected = true;
        connectionAcknowledged = true;

        connectionTimeoutTimer?.Dispose();
        connectionTimeoutTimer = null;

        InvokeConnected();
    }
    
    private void InvokeConnected()
    {
        var subscribers = OnConnected;

        if (subscribers == null)
            return;

        foreach (var subscriber in subscribers.GetInvocationList())
        {
            try
            {
                ((Action<string>)subscriber).Invoke(PlayerId);
            }
            catch (Exception ex)
            {
                var method = subscriber.Method;
                SrLogger.LogError($"OnConnected subscriber {method.DeclaringType?.FullName}.{method.Name} failed: {ex}");
            }
        }
    }
    
    private void InvokeDisconnected()
    {
        var subscribers = OnDisconnected;

        if (subscribers == null)
            return;

        foreach (var subscriber in subscribers.GetInvocationList())
        {
            try
            {
                ((Action)subscriber).Invoke();
            }
            catch (Exception ex)
            {
                var method = subscriber.Method;
                SrLogger.LogError($"OnDisconnected subscriber {method.DeclaringType?.FullName}.{method.Name} failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Retrieves a specific remote player by their unique identifier.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player to retrieve.</param>
    /// <returns>The <see cref="RemotePlayer"/> instance if found; otherwise, <c>null</c>.</returns>
    public static RemotePlayer? GetRemotePlayer(string playerId) => PlayerManager.GetPlayer(playerId);

    /// <summary>
    /// Retrieves a list of all currently connected remote players.
    /// </summary>
    /// <returns>A <see cref="List{RemotePlayer}"/> containing all active remote players in the session.</returns>
    private static List<RemotePlayer> GetAllRemotePlayers() => PlayerManager.GetAllPlayers();
}
