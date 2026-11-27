using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace SR2MP.Shared.Transport;

// One room currently represents one host and one guest. This endpoint is only
// an internal peer key for packet reliability; it is never used for UDP routing.
internal sealed class RelayDatagramTransport : IDatagramTransport
{
    internal static IPEndPoint Peer => new(IPAddress.Parse("192.0.2.1"), 1919);
    private const int QueueLimit = 4 * 1024 * 1024;
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<byte[]> outgoing = new();
    private readonly BlockingCollection<byte[]> incoming = new(new ConcurrentQueue<byte[]>(), 8192);
    private int outgoingBytes, incomingBytes, closed;
    private Exception? failure;
    private long sentFrames, receivedFrames, sentBytes, receivedBytes, sentDatagrams, receivedDatagrams;
    internal (long framesOut, long framesIn, long bytesOut, long bytesIn, long datagramsOut, long datagramsIn) Counters
        => (Interlocked.Read(ref sentFrames), Interlocked.Read(ref receivedFrames), Interlocked.Read(ref sentBytes),
            Interlocked.Read(ref receivedBytes), Interlocked.Read(ref sentDatagrams), Interlocked.Read(ref receivedDatagrams));
    public AddressFamily AddressFamily => AddressFamily.InterNetwork;

    internal static async Task<RelayDatagramTransport> ConnectAsync(Uri endpoint, string roomToken, string? probeKey = null, string? role = null, string? protocol = null)
    {
        if (endpoint.Scheme != "wss" || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query))
            throw new ArgumentException("A secure relay endpoint without URL credentials is required.");
        var transport = new RelayDatagramTransport();
        transport.socket.Options.SetRequestHeader("X-Room-Token", roomToken);
        if (probeKey != null) transport.socket.Options.SetRequestHeader("Authorization", "Bearer " + probeKey);
        if (role != null) transport.socket.Options.SetRequestHeader("X-Room-Role", role);
        if (protocol != null) transport.socket.Options.SetRequestHeader("X-Playground-Protocol", protocol);
        transport.socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await transport.socket.ConnectAsync(endpoint, deadline.Token).ConfigureAwait(false);
            _ = transport.SendLoop(); _ = transport.ReceiveLoop();
            return transport;
        }
        catch { transport.Dispose(); throw; }
    }
    public void Send(ArraySegment<byte> data, IPEndPoint remote)
    {
        if (!remote.Equals(Peer) || data.Count is < 1 or > DatagramBatch.MaxDatagram)
            throw new InvalidDataException("Invalid relay datagram or peer.");
        if (Volatile.Read(ref closed) != 0) throw new IOException("Relay disconnected.", failure);
        var bytes = data.AsSpan().ToArray();
        if (Interlocked.Add(ref outgoingBytes, bytes.Length) > QueueLimit)
        {
            Interlocked.Add(ref outgoingBytes, -bytes.Length);
            Fail(new IOException("Relay send queue exceeded its limit."));
            throw new IOException("Relay send queue exceeded its limit.");
        }
        outgoing.Enqueue(bytes);
    }
    public int Receive(byte[] buffer, ref EndPoint remote)
    {
        byte[] packet;
        try { packet = incoming.Take(stop.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        { throw new IOException("Relay disconnected.", failure); }
        Interlocked.Add(ref incomingBytes, -packet.Length);
        if (packet.Length > buffer.Length) throw new InvalidDataException("Receive buffer too small.");
        packet.CopyTo(buffer, 0); remote = Peer;
        return packet.Length;
    }
    private async Task SendLoop()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(50, stop.Token).ConfigureAwait(false);
                var batch = new List<byte[]>(); var size = 6;
                while (batch.Count < DatagramBatch.MaxCount && outgoing.TryPeek(out var next) && size + 2 + next.Length <= DatagramBatch.MaxFrame)
                {
                    if (!outgoing.TryDequeue(out next)) continue;
                    Interlocked.Add(ref outgoingBytes, -next.Length);
                    batch.Add(next); size += 2 + next.Length;
                }
                if (batch.Count == 0) continue;
                var frame = DatagramBatch.Encode(batch);
                await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, stop.Token).ConfigureAwait(false);
                Interlocked.Increment(ref sentFrames); Interlocked.Add(ref sentBytes, frame.Length); Interlocked.Add(ref sentDatagrams, batch.Count);
            }
        }
        catch (Exception ex) { if (!stop.IsCancellationRequested) Fail(ex); }
    }
    private async Task ReceiveLoop()
    {
        var buffer = new byte[DatagramBatch.MaxFrame + 1];
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var length = 0; WebSocketReceiveResult result;
                do
                {
                    if (length >= buffer.Length) throw new InvalidDataException("Relay message too large.");
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), stop.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Relay closed the connection.");
                    if (result.MessageType != WebSocketMessageType.Binary) throw new InvalidDataException("Invalid relay message type.");
                    length += result.Count;
                    if (length > DatagramBatch.MaxFrame) throw new InvalidDataException("Relay message too large.");
                } while (!result.EndOfMessage);
                var packets = DatagramBatch.Decode(buffer.AsSpan(0, length));
                foreach (var packet in packets)
                {
                    if (Interlocked.Add(ref incomingBytes, packet.Length) > QueueLimit || !incoming.TryAdd(packet))
                        throw new IOException("Relay receive queue exceeded its limit.");
                }
                Interlocked.Increment(ref receivedFrames); Interlocked.Add(ref receivedBytes, length); Interlocked.Add(ref receivedDatagrams, packets.Length);
            }
        }
        catch (Exception ex) { if (!stop.IsCancellationRequested) Fail(ex); }
    }
    private void Fail(Exception ex) { failure = ex; Close(); }
    public void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;
        stop.Cancel(); socket.Abort(); incoming.CompleteAdding();
    }
    // Abort interrupts both IO tasks and synchronous Receive. Their managed
    // queues/cancellation objects are reclaimed with this session, never reused.
    public void Dispose() { Close(); socket.Dispose(); }
}
