using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Ammo;

internal sealed class StorageStatePacket : IPacket
{
    internal string ID = "";
    internal uint Revision;
    internal NetworkAmmo Ammo = new();
    public PacketType Type => PacketType.StorageState;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;
    public NetworkChannel Channel => NetworkChannel.Ammo;
    public void Serialise(PacketWriter writer)
    { writer.WriteString(ID); writer.WriteUInt(Revision); Ammo.Serialise(writer); }
    public void Deserialise(PacketReader reader)
    { ID = reader.ReadPooledString()!; Revision = reader.ReadUInt(); Ammo = new NetworkAmmo(); Ammo.Deserialise(reader); }
}
