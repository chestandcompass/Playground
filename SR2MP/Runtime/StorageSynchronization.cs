using System.Net;
using Il2CppMonomiPark.SlimeRancher.Player;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Ammo;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class StorageSynchronization
{
    private static readonly LatestStorageState<NetworkAmmo> latest = new();
    private static readonly Dictionary<string, Queue<Action>> waiting = new();
    private static readonly Dictionary<string, (string signature, uint revision)> published = new();
    private static float nextPoll, nextFull;
    private static int queued;
    private static bool smokeGuest;
    private static Action<IPacket>? smokeObserver;
    private static bool IsGuest => CoopSaveManager.Role == "guest" || (MenuSmokeTest.Enabled && smokeGuest);
    internal static bool ObserveSmokePacket(IPacket packet)
    {
        if (!MenuSmokeTest.Enabled || smokeObserver == null || packet is not (AmmoDecrementPacket or AmmoAddPacket or AmmoAddToSlotPacket)) return false;
        smokeObserver(packet); return true;
    }
    internal static void Reset() { latest.Clear(); waiting.Clear(); published.Clear(); queued = 0; nextPoll = nextFull = 0; }

    internal static NetworkAmmo Capture(AmmoSlotManager ammo)
    {
        var state = new NetworkAmmo();
        for (var i = 0; i < ammo.Slots.Count; i++)
        {
            var slot = ammo.Slots[i];
            if (slot == null || slot.Definition == null) continue;
            state.AmmoSlots[i] = new NetworkAmmoSlot { Count = slot.Count, MaxCount = slot.MaxCount,
                Identifiable = slot.Count == 0 || slot._id == null ? 0 : NetworkActorManager.GetPersistentID(slot._id),
                SlotDefinition = NetworkAmmoManager.GetId(slot.Definition) };
        }
        return state;
    }

    internal static bool Apply(AmmoSlotManager ammo, NetworkAmmo state)
    {
        if (state.AmmoSlots.Count > 64) throw new InvalidDataException("Too many storage slots.");
        // Validate every slot before touching the live model.
        foreach (var (index, slot) in state.AmmoSlots)
        {
            if (index < 0 || index >= ammo.Slots.Count || slot.Count < 0 || slot.MaxCount < slot.Count)
                return false;
            if (slot.Count > 0 && !ActorManager.ActorTypes.ContainsKey(slot.Identifiable)) return false;
        }
        var previous = HandlingPacket;
        try
        {
            HandlingPacket = true;
            foreach (var (index, data) in state.AmmoSlots)
            {
                var ident = data.Count == 0 ? null : ActorManager.ActorTypes[data.Identifiable];
                var slot = ammo.Slots[index];
                slot._id = ident; slot.MaxCount = data.MaxCount; slot.Count = data.Count;
                var modelSlot = ammo._ammoModel?.Slots[index];
                if (modelSlot != null) { modelSlot._id = ident; modelSlot.MaxCount = data.MaxCount; modelSlot.Count = data.Count; }
            }
            return true;
        }
        finally { HandlingPacket = previous; }
    }

    internal static void Receive(StorageStatePacket packet)
    {
        if (!IsGuest) return;
        if (latest.Store(packet.ID, packet.Revision, packet.Ammo)) OnRegistered(packet.ID);
    }

    internal static void OnRegistered(string id)
    {
        if (Main.Server.IsRunning && waiting.Remove(id, out var actions))
            while (actions.TryDequeue(out var action)) { queued--; action(); }
        if (IsGuest && latest.TryGet(id, out var state))
        {
            var ammo = NetworkAmmoManager.GetAmmo(id);
            if (ammo != null) Apply(ammo, state);
        }
    }

    internal static void RunHostEdit(string? id, Action<AmmoSlotManager> edit)
    {
        // Guests receive absolute host states, never replay additive notifications.
        if (!Main.Server.IsRunning || string.IsNullOrEmpty(id) || id.Length > 256) return;
        var ammo = NetworkAmmoManager.GetAmmo(id);
        if (ammo == null)
        {
            if (queued >= 256) { SrLogger.LogWarning("Storage request queue is full; waiting for a resync."); return; }
            if (!waiting.TryGetValue(id, out var pending)) waiting[id] = pending = new();
            pending.Enqueue(() => RunHostEdit(id, edit)); queued++;
            return;
        }
        var previous = HandlingPacket;
        try { HandlingPacket = true; edit(ammo); }
        finally { HandlingPacket = previous; }
        Publish(id, ammo, true);
    }

    private static void Publish(string id, AmmoSlotManager ammo, bool force)
    {
        var state = Capture(ammo);
        var signature = string.Join(";", state.AmmoSlots.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value.Identifiable}:{x.Value.Count}:{x.Value.MaxCount}"));
        published.TryGetValue(id, out var old);
        var changed = old.signature != signature;
        var revision = old.revision == 0 ? 1 : changed ? checked(old.revision + 1) : old.revision;
        published[id] = (signature, revision);
        if (force || changed) Main.Server.SendToAll(new StorageStatePacket { ID = id, Revision = revision, Ammo = state });
    }

    internal static void Update()
    {
        if (!GameHelpers.inGame || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + .5f;
        if (Main.Server.IsRunning)
        {
            var force = Time.realtimeSinceStartup >= nextFull;
            if (force) nextFull = Time.realtimeSinceStartup + 2;
            foreach (var (id, ammo) in NetworkAmmoManager.RegisteredStorages().ToArray())
                try { Publish(id, ammo, force); } catch (Exception ex) { SrLogger.LogWarning($"Storage '{id}' snapshot: {ex.Message}"); }
        }
        else if (Main.Client.IsJoined)
            foreach (var id in latest.Keys.ToArray()) OnRegistered(id);
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        const string id = "playground-storage-smoke";
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("STORAGE SMOKE FAILED: " + message);
            SrLogger.LogMessage("STORAGE SMOKE PASS: " + message);
        }
        var type = ActorManager.ActorTypes.First(p => p.Value != null && p.Value.name.Contains("Plort", StringComparison.OrdinalIgnoreCase));
        var definition = Resources.FindObjectsOfTypeAll<AmmoSlotDefinition>().First();
        var state = new NetworkAmmo { AmmoSlots = new() { [0] = new NetworkAmmoSlot {
            Count = 5, MaxCount = 30, Identifiable = type.Key, SlotDefinition = NetworkAmmoManager.GetId(definition) } } };
        var first = state.ToGameAmmo();
        var second = state.ToGameAmmo();
        var modelPlot = GameState.landPlots._entries.First(p => p.value != null);
        const string modelKey = "playgroundtest";
        var modelID = modelPlot.key + "_" + modelKey;
        var previous = HandlingPacket;
        try
        {
            first.RegisterAmmoPointer(id);
            second.RegisterAmmoPointer(id);
            Check(first.GetPlotID() == null && first.Slots[0].GetPlotID() == null && NetworkAmmoManager.GetAmmo(id)?.Pointer == second.Pointer,
                "replacing a storage removes stale manager and slot mappings");
            var packets = new List<IPacket>();
            smokeObserver = packets.Add;
            second.Decrement(0, 2);
            Check(second.Slots[0].Count == 3 && packets.Count == 1 && packets[0] is AmmoDecrementPacket { Count: 2 },
                "one native withdrawal produces exactly one update with the actual removed amount");
            smokeObserver = null;
            HandlingPacket = true;
            Check(Apply(second, state) && HandlingPacket && second.Slots[0].Count == 5 && second.Slots[0]._id == type.Value,
                "absolute snapshots restore item type and count while preserving packet scope");
            HandlingPacket = previous;
            smokeGuest = true;
            var newer = new NetworkAmmo { AmmoSlots = new() { [0] = new NetworkAmmoSlot {
                Count = 3, MaxCount = 30, Identifiable = type.Key, SlotDefinition = NetworkAmmoManager.GetId(definition) } } };
            Receive(new StorageStatePacket { ID = id, Revision = 2, Ammo = newer });
            Receive(new StorageStatePacket { ID = id, Revision = 1, Ammo = state });
            Check(second.Slots[0].Count == 3, "late old snapshot cannot overwrite newer collector contents");
            var replacement = state.ToGameAmmo();
            replacement.RegisterAmmoPointer(id);
            Check(replacement.Slots[0].Count == 3, "storage rebound after initial world data receives the latest host contents");
            using var writer = PacketWriter.Borrow();
            var packet = new StorageStatePacket { ID = id, Revision = 3, Ammo = newer };
            packet.Serialise(writer);
            using var reader = PacketReader.Borrow(writer.ToSpan().ToArray());
            var decoded = new StorageStatePacket(); decoded.Deserialise(reader);
            Check(decoded.ID == id && decoded.Revision == 3 && decoded.Ammo.AmmoSlots[0].Count == 3,
                "collector snapshot round-trips through the real packet serializer");
            smokeGuest = false;
            RunHostEdit(id + "-late", ammo => ammo.Decrement(0, 1));
            var delayed = state.ToGameAmmo();
            delayed.RegisterAmmoPointer(id + "-late");
            Check(delayed.Slots[0].Count == 4, "host request received before registration executes after storage becomes available");
            var twoSlots = new NetworkAmmo { AmmoSlots = new() {
                [1] = new NetworkAmmoSlot { Count = 0, MaxCount = 30, SlotDefinition = NetworkAmmoManager.GetId(definition) },
                [0] = new NetworkAmmoSlot { Count = 30, MaxCount = 30, Identifiable = type.Key, SlotDefinition = NetworkAmmoManager.GetId(definition) } } }.ToGameAmmo();
            Check(twoSlots.Slots[0].Count == 30 && twoSlots.GetNextSlot(type.Value) == 1,
                "snapshot slot indexes are preserved and full slots do not block later empty slots");
            modelPlot.value.siloAmmo[modelKey] = state.ToGameAmmo()._ammoModel;
            var modelOnly = NetworkAmmoManager.GetAmmo(modelID);
            Check(modelOnly != null && modelOnly.Slots[0].Count == 5
                && modelOnly._ammoModel.Pointer == modelPlot.value.siloAmmo[modelKey].Pointer,
                "host resolves storage from world models without a registered scene component");
        }
        finally
        {
            smokeObserver = null; smokeGuest = false; HandlingPacket = previous;
            NetworkAmmoManager.UnregisterTestStorage(id); NetworkAmmoManager.UnregisterTestStorage(id + "-late"); Reset();
            NetworkAmmoManager.UnregisterTestStorage(modelID); modelPlot.value.siloAmmo.Remove(modelKey);
        }
    }
}

[PacketHandler((byte)PacketType.StorageState, HandlerType.Client)]
internal sealed class StorageStateHandler : BasePacketHandler<StorageStatePacket>
{
    protected override bool Handle(StorageStatePacket packet, IPEndPoint? endpoint)
    { StorageSynchronization.Receive(packet); return false; }
}
