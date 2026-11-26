using SR2MP.Packets.Utils;

namespace SR2MP.Packets.Gadget;

internal sealed class GadgetPlacementPacket : IPacket
{
    internal string Request = "";
    internal int TypeID, SceneID;
    internal Vector3 Position;
    internal Quaternion Rotation;
    public PacketType Type => PacketType.GadgetPlacement;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;
    public NetworkChannel Channel => NetworkChannel.ActorCritical;
    public void Serialise(PacketWriter writer)
    { writer.WriteString(Request); writer.WritePackedInt(TypeID); writer.WritePackedInt(SceneID); writer.WriteVector3(Position); writer.WriteQuaternion(Rotation); }
    public void Deserialise(PacketReader reader)
    { Request = reader.ReadPooledString()!; TypeID = reader.ReadPackedInt(); SceneID = reader.ReadPackedInt(); Position = reader.ReadVector3(); Rotation = reader.ReadQuaternion(); }
}

internal sealed class GadgetPlacementResult : IPacket
{
    internal string Request = "";
    internal bool Accepted;
    public PacketType Type => PacketType.GadgetPlacementResult;
    public PacketReliability Reliability => PacketReliability.ReliableOrdered;
    public NetworkChannel Channel => NetworkChannel.ActorCritical;
    public void Serialise(PacketWriter writer) { writer.WriteString(Request); writer.WriteBool(Accepted); }
    public void Deserialise(PacketReader reader) { Request = reader.ReadPooledString()!; Accepted = reader.ReadBool(); }
}
