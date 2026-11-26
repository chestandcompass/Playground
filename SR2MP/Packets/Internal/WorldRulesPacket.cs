using SR2MP.Packets.Utils;
using SR2MP.Shared.Utils;

namespace SR2MP.Packets.Internal;

internal sealed class WorldRulesPacket : IPacket
{
    internal WorldRuleState Rules = null!;
    public PacketType Type => PacketType.WorldRules;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;
    public NetworkChannel Channel => NetworkChannel.Important;
    public void Serialise(PacketWriter writer) => Write(writer, Rules);
    public void Deserialise(PacketReader reader) => Rules = Read(reader);
    internal static void Write(PacketWriter writer, WorldRuleState rules)
    {
        writer.WriteUInt(rules.Revision);
        foreach (var value in rules.Values) writer.WriteString(value);
    }
    internal static WorldRuleState Read(PacketReader reader)
    {
        var revision = reader.ReadUInt();
        var values = new string[WorldRuleState.Count];
        for (var i = 0; i < values.Length; i++) values[i] = reader.ReadPooledString()!;
        return new WorldRuleState(revision, values);
    }
}
