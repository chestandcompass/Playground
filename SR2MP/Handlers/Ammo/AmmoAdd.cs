using System.Net;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Ammo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;

namespace SR2MP.Handlers.Ammo;

[PacketHandler((byte)PacketType.AmmoAdd)]
internal sealed class AmmoAddHandler : BasePacketHandler<AmmoAddPacket>
{
    protected override bool Handle(AmmoAddPacket packet, IPEndPoint? _)
    {
        StorageSynchronization.RunHostEdit(packet.ID, ammo =>
        {
            if (packet.Count <= 0 || !ActorManager.ActorTypes.TryGetValue(packet.Identifiable, out var ident) || ident == null) return;
            var slot = ammo.GetNextSlot(ident);
            if (slot >= 0) ammo.MaybeAddToSpecificSlot(new AmmoSlot.AmmoMetadata(ident), slot, packet.Count, false);
        });
        return false;
    }
}
