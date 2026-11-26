using Il2CppMonomiPark.SlimeRancher.Ranch;
using System.Net;
using HarmonyLib;
using SR2MP.Components.Player;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class PlayerCosmetics
{
    private static readonly Dictionary<string, (string palette, byte hair)> states = new();
    private static readonly Dictionary<string, (int instance, string palette, byte hair)> applied = new();
    private static Dictionary<string, VacPalette>? palettes;
    private static float nextPoll, nextFull;
    private static string? guestFolder;
    private static string lastLocal = "";
    private static int lastHair = -1;

    internal static void Reset()
    {
        states.Clear(); applied.Clear(); palettes = null; PlayerHair.Reset(); lastHair = -1;
        guestFolder = null; lastLocal = ""; nextPoll = nextFull = 0;
    }

    internal static void Forget(string id) { states.Remove(id); applied.Remove(id); }

    private static Dictionary<string, VacPalette> Palettes() => palettes ??= Resources.FindObjectsOfTypeAll<VacPalette>()
        .Where(p => !string.IsNullOrEmpty(p.ReferenceId)).GroupBy(p => p.ReferenceId).ToDictionary(p => p.Key, p => p.First());

    internal static bool Receive(PlayerCosmeticsPacket packet)
    {
        if (string.IsNullOrEmpty(packet.PlayerId) || packet.PlayerId.Length > 64
            || string.IsNullOrEmpty(packet.PaletteId) || packet.PaletteId.Length > 128 || !Palettes().ContainsKey(packet.PaletteId)) return false;
        if (!HairColors.IsValid(packet.Hair)) return false;
        if (!states.ContainsKey(packet.PlayerId) && states.Count >= 64) return false;
        var changed = !states.TryGetValue(packet.PlayerId, out var old) || old != (packet.PaletteId, packet.Hair);
        states[packet.PlayerId] = (packet.PaletteId, packet.Hair);
        return changed;
    }

    private static void ApplyRemote(GameObject root, VacPalette palette)
    {
        // Apply only to this player's assignments. Never register remote objects with
        // the global PaletteDirector, or a local style change would recolor everyone.
        var applicator = new VacPaletteApplicator();
        foreach (var target in root.GetComponentsInChildren<ApplyPalette>(true))
            if (target.Target != null && target.Target.TryCast<VacPaletteTarget>() != null)
                applicator.Apply(target, palette);
    }

    internal static void Update()
    {
        if (!GameHelpers.inGame || (!Main.Server.IsRunning && !Main.Client.IsJoined) || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + .5f;
        try
        {
            var director = Resources.FindObjectsOfTypeAll<PaletteDirector>().FirstOrDefault(p => p.gameObject.activeInHierarchy);
            var target = Resources.FindObjectsOfTypeAll<VacPaletteTarget>().FirstOrDefault();
            if (director == null || target == null) return;
            if (CoopSaveManager.GuestWorld != null && guestFolder != CoopSaveManager.GuestWorld)
            {
                guestFolder = CoopSaveManager.GuestWorld;
                var path = Path.Combine(guestFolder, "vac-style.txt");
                if (File.Exists(path) && new FileInfo(path).Length <= 512 && Palettes().TryGetValue(File.ReadAllText(path).Trim(), out var saved))
                    director.SetPalette(target, saved);
            }
            var local = director.GetPaletteForTarget(target)?.TryCast<VacPalette>();
            if (local == null) return;
            var full = Time.realtimeSinceStartup >= nextFull;
            if (full) nextFull = Time.realtimeSinceStartup + 2;
            var changed = lastLocal != local.ReferenceId || lastHair != Main.HairColor;
            if (changed || full)
            {
                var packet = new PlayerCosmeticsPacket { PlayerId = LocalID, PaletteId = local.ReferenceId, Hair = Main.HairColor };
                Receive(packet);
                if (Main.Server.IsRunning) { if (changed) Main.Server.SendToAll(packet); }
                else PacketSender.SendPacket(packet);
                if (changed && guestFolder != null)
                    CoopFiles.WriteAtomic(Path.Combine(guestFolder, "vac-style.txt"), System.Text.Encoding.UTF8.GetBytes(local.ReferenceId));
                lastLocal = local.ReferenceId; lastHair = Main.HairColor;
            }
            foreach (var pair in states.ToArray())
            {
                if (Main.Server.IsRunning && full)
                    Main.Server.SendToAll(new PlayerCosmeticsPacket { PlayerId = pair.Key, PaletteId = pair.Value.palette, Hair = pair.Value.hair });
                if (pair.Key == LocalID || !PlayerObjects.TryGetValue(pair.Key, out var obj) || obj == null) continue;
                if (applied.TryGetValue(pair.Key, out var old) && old.instance == obj.GetInstanceID() && old.palette == pair.Value.palette && old.hair == pair.Value.hair) continue;
                ApplyRemote(obj, Palettes()[pair.Value.palette]);
                PlayerHair.Apply(obj, pair.Value.hair);
                applied[pair.Key] = (obj.GetInstanceID(), pair.Value.palette, pair.Value.hair);
            }
        }
        catch (Exception ex) { SrLogger.LogWarning("Vacpack style synchronization: " + ex.Message); }
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("COSMETICS SMOKE FAILED: " + message);
            SrLogger.LogMessage("COSMETICS SMOKE PASS: " + message);
        }
        var first = Object.Instantiate(PlayerPrefab);
        var second = Object.Instantiate(PlayerPrefab);
        first.GetComponent<NetworkPlayer>().IsLocal = second.GetComponent<NetworkPlayer>().IsLocal = true;
        var blue = Palettes().Values.First(p => p.name == "VacPalette_Blue");
        var red = Palettes().Values.First(p => p.name == "VacPalette_Red");
        var director = Resources.FindObjectsOfTypeAll<PaletteDirector>().First(p => p.gameObject.activeInHierarchy);
        var target = Resources.FindObjectsOfTypeAll<VacPaletteTarget>().First();
        var original = director.GetPaletteForTarget(target);
        Material Body(GameObject root) => root.GetComponentsInChildren<ApplyPalette>(true)
            .Where(a => a.Target != null && a.Target.TryCast<VacPaletteTarget>() != null)
            .SelectMany(a => a.PaletteSlotAssignments)
            .First(a => a.paletteSlotIdentifier.name == "VacBodySlot").renderers[0].sharedMaterial;
        Material Expected(VacPalette palette) => palette._materialSlots.First(s => s.Slot.name == "VacBodySlot").Material;
        try
        {
            ApplyRemote(first, blue); ApplyRemote(second, red);
            Check(Body(first).Pointer == Expected(blue).Pointer && Body(second).Pointer == Expected(red).Pointer,
                "two real remote models keep independent Vacpack materials");
            director.SetPalette(target, red);
            Check(Body(first).Pointer == Expected(blue).Pointer,
                "changing the local native style does not recolor the remote model");
            foreach (var palette in Palettes().Values)
            {
                ApplyRemote(second, palette);
                Check(Body(second).Pointer == Expected(palette).Pointer, "native style applies: " + palette.name);
            }
            ApplyRemote(second, blue);
            Check(Body(second).Pointer == Expected(blue).Pointer, "switching back from decorated styles restores the selected material");
            var packet = new PlayerCosmeticsPacket { PlayerId = "cosmetic-test", PaletteId = blue.ReferenceId, Hair = 5 };
            using var writer = PacketWriter.Borrow(); packet.Serialise(writer);
            using var reader = PacketReader.Borrow(writer.ToSpan().ToArray());
            var decoded = new PlayerCosmeticsPacket(); decoded.Deserialise(reader);
            Check(decoded.PlayerId == packet.PlayerId && decoded.PaletteId == packet.PaletteId && decoded.Hair == 5, "style packets round-trip through the real serializer including hair color");
            Check(Receive(decoded) && states[packet.PlayerId].palette == blue.ReferenceId && !Receive(decoded),
                "style received before a player model is cached; repeated state is idempotent");
            Check(!Receive(new PlayerCosmeticsPacket { PlayerId = packet.PlayerId, PaletteId = "../unknown" })
                && states[packet.PlayerId].palette == blue.ReferenceId, "unknown assets cannot overwrite the selected style");
            Check(!Receive(new PlayerCosmeticsPacket { PlayerId = packet.PlayerId, PaletteId = blue.ReferenceId, Hair = 255 })
                && states[packet.PlayerId].hair == 5, "invalid hair choices cannot overwrite cached cosmetics");
        }
        finally
        {
            director.SetPalette(target, original); Forget("cosmetic-test");
            Object.Destroy(first); Object.Destroy(second);
        }
    }
}

[HarmonyPatch(typeof(ApplyPalette), "Start")]
internal static class IsolateRemoteVacStyle
{
    internal static bool Prefix(ApplyPalette __instance)
    {
        var player = __instance.GetComponentInParent<NetworkPlayer>();
        return player == null || player.IsLocal;
    }
}

[HarmonyPatch(typeof(ApplyPalette), "OnDestroy")]
internal static class IsolateRemoteVacStyleCleanup
{
    internal static bool Prefix(ApplyPalette __instance) => IsolateRemoteVacStyle.Prefix(__instance);
}

[PacketHandler((byte)PacketType.PlayerCosmetics, HandlerType.Server)]
internal sealed class ServerPlayerCosmeticsHandler : BasePacketHandler<PlayerCosmeticsPacket>
{
    protected override bool Handle(PlayerCosmeticsPacket packet, IPEndPoint? endpoint)
    {
        if (endpoint == null || !Main.Server.ClientManager.TryGetClient(endpoint, out var client) || client == null) return false;
        packet.PlayerId = client.PlayerId;
        if (PlayerCosmetics.Receive(packet)) Main.Server.SendToAll(packet);
        return false;
    }
}

[PacketHandler((byte)PacketType.PlayerCosmetics, HandlerType.Client)]
internal sealed class ClientPlayerCosmeticsHandler : BasePacketHandler<PlayerCosmeticsPacket>
{
    protected override bool Handle(PlayerCosmeticsPacket packet, IPEndPoint? endpoint)
    { PlayerCosmetics.Receive(packet); return false; }
}
