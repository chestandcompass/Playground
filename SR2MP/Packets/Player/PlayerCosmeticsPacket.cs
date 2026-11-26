using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Player;

internal sealed class PlayerCosmeticsPacket : IPacket
{
    internal string PlayerId = "", PaletteId = "";
    internal byte Hair;
    public PacketType Type => PacketType.PlayerCosmetics;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;
    public NetworkChannel Channel => NetworkChannel.Important;
    public void Serialise(PacketWriter writer) { writer.WriteString(PlayerId); writer.WriteString(PaletteId); writer.WriteByte(Hair); }
    public void Deserialise(PacketReader reader) { PlayerId = reader.ReadPooledString()!; PaletteId = reader.ReadPooledString()!; Hair = reader.ReadByte(); }
}
