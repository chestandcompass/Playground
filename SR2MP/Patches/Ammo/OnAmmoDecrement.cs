using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Packets.Ammo;
using SR2MP.Shared.Managers;

namespace SR2MP.Patches.Ammo;

[HarmonyPatch(typeof(AmmoSlotManager), nameof(AmmoSlotManager.Decrement), typeof(int), typeof(int))]
internal static class OnAmmoDecrement
{
    internal static int Depth;
    public static void Prefix(AmmoSlotManager __instance, int index, out int __state)
    {
        __state = index >= 0 && index < __instance.Slots.Count ? __instance.Slots[index].Count : 0;
        Depth++;
    }
    public static Exception? Finalizer(Exception? __exception) { Depth = Math.Max(0, Depth - 1); return __exception; }
    public static void Postfix(AmmoSlotManager __instance, int index, int __state)
    {
        if ((!Main.Client.IsConnected && !Main.Server.IsRunning) || HandlingPacket) return;
        if (index < 0 || index >= __instance.Slots.Count) return;
        var removed = __state - __instance.Slots[index].Count;
        if (removed <= 0) return;

        if (__instance.Slots[index]!._count <= 0) __instance.Slots[index]!._id = null;

        var packet = new AmmoDecrementPacket()
        {
            SlotIndex = index,
            Count = removed,
            ID = __instance.GetPlotID()
        };

        if (packet.ID == null) return;

        Main.SendToAllOrServer(packet);
    }
}
