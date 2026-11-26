using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Packets.Ammo;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Ammo;

[HarmonyPatch(typeof(AmmoSlot), nameof(AmmoSlot.DecrementAmmo))]
internal static class OnAmmoSlotDecrement
{
    public static void Prefix(AmmoSlot __instance, out int __state) => __state = __instance.Count;
    public static void Postfix(AmmoSlot __instance, int __state)
    {
        if ((!Main.Client.IsConnected && !Main.Server.IsRunning) || HandlingPacket) return;
        if (OnAmmoDecrement.Depth > 0) return;
        var removed = __state - __instance.Count;
        if (removed <= 0) return;

        if (__instance._count <= 0) __instance._id = null;

        var index = __instance.GetSlotIndex();
        if (index == null) return;

        var packet = new AmmoDecrementPacket()
        {
            SlotIndex = (int)index,
            Count = removed,
            ID = __instance.GetPlotID()!
        };

        if (packet.ID == null) return;

        Main.SendToAllOrServer(packet);
    }
}
