using System.Net;
using SR2MP.Packets.Utils;

namespace SR2MP.Shared;

internal readonly struct ServerHandleCache
{
    public readonly PacketReader Reader;
    public readonly IServerPacketHandler Handler;
    public readonly IPEndPoint ClientEp;
    public readonly long Session;

    public ServerHandleCache(PacketReader reader, IServerPacketHandler handler, IPEndPoint clientEp, long session)
    {
        Reader = reader;
        Handler = handler;
        ClientEp = clientEp;
        Session = session;
    }
}

internal readonly struct ClientHandleCache
{
    public readonly PacketReader Reader;
    public readonly IClientPacketHandler Handler;
    public readonly long Session;

    public ClientHandleCache(PacketReader reader, IClientPacketHandler handler, long session)
    {
        Reader = reader;
        Handler = handler;
        Session = session;
    }
}
