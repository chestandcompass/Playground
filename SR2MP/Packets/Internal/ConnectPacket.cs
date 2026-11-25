using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Internal;

internal sealed class ConnectPacket : IPacket
{
    public string Protocol = SR2MP.Shared.Managers.CoopSaveManager.Protocol;
    public string PlayerId;
    public string Username;

    public PacketType Type => PacketType.Connect;
    public PacketReliability Reliability => PacketReliability.Reliable;
    public NetworkChannel Channel => NetworkChannel.Important;

    public void Serialise(PacketWriter writer)
    {
        writer.WriteString(Protocol);
        writer.WriteStringWithoutSize(PlayerId);
        writer.WriteString(Username);
    }

    public void Deserialise(PacketReader reader)
    {
        Protocol = reader.ReadPooledString()!;
        PlayerId = reader.ReadPooledStringOfSize(16)!;
        Username = reader.ReadPooledString()!;
    }
}
