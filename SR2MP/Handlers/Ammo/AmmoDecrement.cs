using System.Net;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Ammo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Handlers.Ammo;

[PacketHandler((byte)PacketType.AmmoDecrement)]
internal sealed class AmmoDecrementHandler : BasePacketHandler<AmmoDecrementPacket>
{
    protected override bool Handle(AmmoDecrementPacket packet, IPEndPoint? _)
    {
        StorageSynchronization.RunHostEdit(packet.ID, ammo =>
        {
            if (packet.Count <= 0 || packet.SlotIndex < 0 || packet.SlotIndex >= ammo.Slots.Count) return;
            if (packet.Count <= ammo.Slots[packet.SlotIndex].Count) ammo.Decrement(packet.SlotIndex, packet.Count);
        });
        return false;
    }
}
