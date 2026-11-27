using System.Net;
using System.Net.Sockets;

namespace SR2MP.Shared.Transport;

internal interface IDatagramTransport : IDisposable
{
    AddressFamily AddressFamily { get; }
    int Receive(byte[] buffer, ref EndPoint remote);
    void Send(ArraySegment<byte> data, IPEndPoint remote);
    void Close();
}

internal sealed class UdpDatagramTransport : IDatagramTransport
{
    private readonly UdpClient socket;
    internal UdpDatagramTransport(UdpClient socket)
    {
        this.socket = socket;
        socket.Client.ReceiveBufferSize = 512 * 1024;
        socket.Client.SendBufferSize = 512 * 1024;
        socket.Client.ReceiveTimeout = 0;
    }
    public AddressFamily AddressFamily => socket.Client.AddressFamily;
    internal void Connect(IPEndPoint endpoint) => socket.Connect(endpoint);
    public int Receive(byte[] buffer, ref EndPoint remote) => socket.Client.ReceiveFrom(buffer, ref remote);
    public void Send(ArraySegment<byte> data, IPEndPoint remote)
    {
        if (data.Array != null) socket.Client.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, remote);
    }
    public void Close() => socket.Close();
    public void Dispose() => socket.Dispose();
}
