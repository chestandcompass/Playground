using HarmonyLib;
using SR2MP.Packets.Ammo;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Ammo;

[HarmonyPatch(typeof(SiloStorage), nameof(SiloStorage.MaybeAddAsResource),
    typeof(IdentifiableType), typeof(int), typeof(int), typeof(bool))]
internal static class OnSiloStorageAddResource
{
    // For Drones
    // MaybeAddAsResource routes through MaybeAddToXSlot, so we need this to prevent duplication
    private static int resourceDepth;
    internal static bool isDroneResource => resourceDepth > 0;

    public static void Prefix() => resourceDepth++;

    public static void Postfix(SiloStorage __instance, bool __result, IdentifiableType id, int slotIdx, int count)
    {
        if ((!Main.Client.IsConnected && !Main.Server.IsRunning) || HandlingPacket) return;

        if (!__result)
            return;

        var packet = new AmmoAddToSlotPacket
        {
            Identifiable = NetworkActorManager.GetPersistentID(id),
            SlotIndex = slotIdx,
            Count = count,
            ID = __instance.GetRelevantAmmo()?.GetPlotID()
        };

        if (packet.ID == null) return;

        Main.SendToAllOrServer(packet);
    }
    
    public static Exception? Finalizer(Exception? __exception)
    {
        resourceDepth = Math.Max(0, resourceDepth - 1);
        return __exception;
    }
}
