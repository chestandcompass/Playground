using System.Buffers;
using System.Net;
using System.Net.Sockets;
using SR2MP.Packets.Utils;
using SR2MP.Server.Models;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Transport;

namespace SR2MP.Server.Managers;

internal sealed class NetworkManager
{
    private IDatagramTransport? transport;
    private volatile bool isRunning;
    private System.Threading.Thread? receiveThread;
    private ReliabilityManager? reliabilityManager;
    private long epoch;
    internal long SessionEpoch => Interlocked.Read(ref epoch);
    internal bool OwnsTransport(IDatagramTransport value) => ReferenceEquals(transport,value);

    public event Action<byte[], int, IPEndPoint, long>? OnDataReceived;
    internal event Action<IDatagramTransport>? OnTransportFailed;

    public bool IsRunning => isRunning;

    public void Start(int port, bool enableIPv6 = true, IDatagramTransport? prepared = null)
    {
        if (isRunning)
        {
            SrLogger.LogMessage("Server is already running!");
            return;
        }

        try
        {
            if (prepared != null) transport = prepared;
            else
            {
                UdpClient udp;
                if (enableIPv6)
                {
                    udp = new UdpClient(AddressFamily.InterNetworkV6);
                    udp.Client.DualMode = true;
                    udp.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                }
                else udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                transport = new UdpDatagramTransport(udp);
            }

            var session = transport!;
            var generation = Interlocked.Increment(ref epoch);
            reliabilityManager = new ReliabilityManager((data, endpoint) =>
            {
                if (generation == SessionEpoch && data.Array != null) session.Send(data, endpoint);
            });
            reliabilityManager.Start();

            isRunning = true;

            receiveThread = new System.Threading.Thread(()=>ReceiveLoop(session,generation))
            {
                IsBackground = true
            };
            receiveThread.Start();
        }
        catch (Exception ex)
        {
            transport?.Dispose(); transport = null;
            SrLogger.LogError($"Failed to start Server: {ex}");
            throw;
        }
    }

    private void ReceiveLoop(IDatagramTransport session,long generation)
    {
        if (session == null)
        {
            SrLogger.LogError("Server is null in ReceiveLoop!");
            return;
        }

        SrLogger.LogMessage("Server ReceiveLoop started!");

        EndPoint remoteEp = SR2MP.Shared.Utils.NetworkEndpoints.ReceiveEndpoint(session.AddressFamily);
        var receiveBuffer = ArrayPool<byte>.Shared.Rent(2048);

        try
        {
            while (isRunning && ReferenceEquals(transport, session))
            {
                try
                {
                    var receivedBytes = session.Receive(receiveBuffer, ref remoteEp);
                    if (generation != SessionEpoch) break;

                    if (receivedBytes > 0)
                        OnDataReceived?.Invoke(receiveBuffer, receivedBytes, (IPEndPoint)remoteEp,generation);
                }
                catch (SocketException ex)
                {
                    if (!isRunning || !ReferenceEquals(transport, session)) break;
                    if (ex.SocketErrorCode == SocketError.ConnectionReset) continue;
                    SrLogger.LogError($"Server receive socket stopped: {ex.Message}");
                    Stop();
                    OnTransportFailed?.Invoke(session);
                    break;
                }
                catch (IOException) when (session is RelayDatagramTransport)
                {
                    if (!isRunning || !ReferenceEquals(transport, session)) break;
                    SrLogger.LogMessage("Playground relay connection ended.");
                    Stop();
                    OnTransportFailed?.Invoke(session);
                    break;
                }
                catch (Exception ex)
                {
                    if (isRunning && ReferenceEquals(transport, session))
                    {
                        SrLogger.LogError($"ReceiveLoop error: {ex}");
                        Stop();
                        OnTransportFailed?.Invoke(session);
                    }
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(receiveBuffer);
            SrLogger.LogMessage("Server ReceiveLoop stopped");
        }
    }

    public void Send(ReadOnlySpan<byte> data, IPEndPoint endPoint, PacketReliability? reliability = null,
        NetworkChannel channel = NetworkChannel.Default)
    {
        if (transport == null || !isRunning)
        {
            SrLogger.LogWarning("Cannot send: Server not running!");
            return;
        }

        try
        {
            var packetReliability = reliability ?? PacketReliability.Unreliable;
            ushort sequenceNumber = 0;

            if (packetReliability.HasFlag(PacketReliability.Ordered))
                sequenceNumber = reliabilityManager?.GetNextSequenceNumber(channel, data[0], endPoint) ?? 0;

            var splitResult = PacketChunkManager.SplitPacket(data, packetReliability, channel, sequenceNumber, out var packetId);

            if (packetReliability.HasFlag(PacketReliability.Reliable))
                reliabilityManager?.TrackPacket(splitResult, endPoint, packetId, data[0], packetReliability);

            for (var i = 0; i < splitResult.Count; i++)
                SendRaw(splitResult.Chunks[i], endPoint);

            if (!packetReliability.HasFlag(PacketReliability.Reliable))
                splitResult.Dispose();
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Send failed to {endPoint}: {ex}");
        }
    }

    public void Broadcast(ReadOnlySpan<byte> data, ICollection<ClientInfo> endPoints,
        PacketReliability? reliability = null, NetworkChannel channel = NetworkChannel.Important)
    {
        if (transport == null || !isRunning)
        {
            SrLogger.LogWarning("Cannot broadcast: Server not running!");
            return;
        }

        try
        {
            foreach (var endPoint in endPoints)
                Send(data, endPoint.EndPoint, reliability, channel);
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Broadcast failed: {ex}");
        }
    }

    // Sends raw data without reliability tracking (used for resends or internally)
    private void SendRaw(ArraySegment<byte> data, IPEndPoint endPoint)
    {
        if (data.Array != null)
            transport?.Send(data, endPoint);
    }

    public void HandleAck(IPEndPoint sender, ushort packetId, byte packetType)
        => reliabilityManager?.HandleAck(sender, packetId, packetType);

    public bool ShouldProcessOrderedPacket(IPEndPoint sender, ushort sequenceNumber, byte packetType,
        NetworkChannel channel, PacketReliability reliability, Action? processAction = null)
            => reliabilityManager?.ShouldProcessOrderedPacket(sender, sequenceNumber, packetType, channel, reliability, processAction) ?? true;

    public void Stop()
    {
        if (!isRunning)
            return;

        isRunning = false;
        Interlocked.Increment(ref epoch);

        try
        {
            reliabilityManager?.Stop();
            transport?.Close();

            if (receiveThread is { IsAlive: true } && receiveThread != System.Threading.Thread.CurrentThread && !receiveThread.Join(250))
            {
                SrLogger.LogWarning("Receive thread did not stop gracefully");
            }

            SrLogger.LogMessage("Server stopped");
        }
        catch (Exception ex)
        {
            SrLogger.LogError($"Error stopping Server: {ex}");
        }
    }

    public int GetPendingReliablePackets() => reliabilityManager?.GetPendingPacketCount() ?? 0;
}
