using System.Net;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Ammo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Handlers.Ammo;

[PacketHandler((byte)PacketType.AmmoAddToSlot)]
internal sealed class AmmoAddToSlotHandler : BasePacketHandler<AmmoAddToSlotPacket>
{
    protected override bool Handle(AmmoAddToSlotPacket packet, IPEndPoint? _)
    {
        StorageSynchronization.RunHostEdit(packet.ID, ammo =>
        {
            if (packet.Count <= 0 || packet.SlotIndex < 0 || packet.SlotIndex >= ammo.Slots.Count) return;
            if (!ActorManager.ActorTypes.TryGetValue(packet.Identifiable, out var ident) || ident == null) return;
            ammo.MaybeAddToSpecificSlot(new AmmoSlot.AmmoMetadata(ident), packet.SlotIndex, packet.Count, false);
        });
        return false;
    }
}
