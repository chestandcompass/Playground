using System.Net;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Input;
using Il2CppMonomiPark.SlimeRancher.Player.PlayerItems;
using SR2MP.Handlers.Internal;
using SR2MP.Packets.Actor;
using SR2MP.Packets.Gadget;
using SR2MP.Packets.Utils;
using SR2MP.Packets.World;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class HostGadgetPlacement
{
    private static readonly AtomicStockGate stock = new();
    private static string? pending;
    private static float pendingAt;
    private static List<Il2CppMonomiPark.SlimeRancher.DataModel.ActorId>? smokeCreated;
    internal static void Reset() { stock.Clear(); pending = null; }

    internal static bool PlaceFromMenu(GadgetItem item, InputEventData data)
    {
        if (HandlingPacket) return true;
        if (CoopSaveManager.Role == "guest" && !Main.Client.IsJoined) return false;
        if (!Main.Client.IsJoined && !Main.Server.IsRunning) return true;
        // Moving an already held object follows the native move path; it does not
        // consume a new item from shared stock.
        if (item._holdingPickedUpGadget) return true;
        if (data.Value <= 0 || !item.IsPlaceholderVisible || !item._isPlacementValid || item._isPlacementBlocked || item._heldGadget == null) return false;
        if (pending != null && Time.realtimeSinceStartup - pendingAt < 5) return false;
        var placeholder = item.GetPlaceholderGadget();
        if (placeholder == null) return false;
        var definition = item.GetGadgetDefinitionToPlace(item._heldGadget);
        var request = new GadgetPlacementPacket { Request = Guid.NewGuid().ToString("N"),
            TypeID = NetworkActorManager.GetPersistentID(definition),
            SceneID = NetworkSceneManager.GetPersistentID(SystemContext.Instance.SceneLoader.CurrentSceneGroup),
            Position = placeholder.transform.position, Rotation = placeholder.transform.rotation };
        if (Main.Server.IsRunning) Execute(request, "host");
        else { pending = request.Request; pendingAt = Time.realtimeSinceStartup; Main.Client.SendPacket(request); }
        return false;
    }

    internal static bool Execute(GadgetPlacementPacket request, string owner)
    {
        if (!Main.Server.IsRunning || !Guid.TryParseExact(request.Request, "N", out _)) return false;
        if (!float.IsFinite(request.Position.x) || !float.IsFinite(request.Position.y) || !float.IsFinite(request.Position.z)
            || !float.IsFinite(request.Rotation.x) || !float.IsFinite(request.Rotation.y) || !float.IsFinite(request.Rotation.z) || !float.IsFinite(request.Rotation.w)
            || request.Position.sqrMagnitude > 1e12f || request.Rotation == default) return false;
        if (!ActorManager.ActorTypes.TryGetValue(request.TypeID, out var type)) return false;
        var definition = type.TryCast<GadgetDefinition>();
        if (definition == null) return false;
        if (request.SceneID < 0 || request.SceneID > byte.MaxValue) return false;
        _ = NetworkSceneManager.GetSceneGroup(request.SceneID); // Reject unknown scenes before reserving stock.
        var stockType = definition.BaseGadget ?? definition;
        var director = SceneContext.Instance.GadgetDirector;
        var accepted = stock.Execute(owner + "|" + request.Request,
            () => director.GetItemCount(stockType), count => director._model.SetCount(stockType, count), () =>
            {
                var actorId = GameState._actorIdProvider.GetNewId();
                var prefab = definition.prefab?.GetComponent<Il2CppMonomiPark.SlimeRancher.World.Gadget>();
                var chargeup = prefab != null && prefab.CooldownTimeGameHrs > 0
                    ? SceneContext.Instance.TimeDirector.HoursFromNow(prefab.CooldownTimeGameHrs) : 0;
                try
                {
                    if (!ActorManager.TrySpawnNetworkActor(actorId, request.Position, request.Rotation, request.TypeID, request.SceneID, out var actor, chargeupTime: chargeup) || actor == null) return false;
                    smokeCreated?.Add(actorId);
                    try { Main.Server.SendToAll(new ActorSpawnPacket { ActorId = actorId, ActorType = request.TypeID,
                        SceneGroup = checked((byte)request.SceneID), Position = request.Position, Rotation = request.Rotation,
                        SpawnType = (byte)ActorSpawnType.Gadget, OwnerId = LocalID, ChargeupTime = chargeup }); }
                    catch (Exception ex) { SrLogger.LogWarning($"Gadget spawn notification: {ex.Message}"); }
                    try { NetworkGadgetManager.BroadcastGadgetLink(actor.Cast<Il2CppMonomiPark.SlimeRancher.DataModel.GadgetModel>()); }
                    catch (Exception ex) { SrLogger.LogWarning($"Gadget link notification: {ex.Message}"); }
                    try { Resources.FindObjectsOfTypeAll<GadgetItemMetadata>().FirstOrDefault()?.GadgetPlaceEvent?.RaiseEventForData(definition); }
                    catch (Exception ex) { SrLogger.LogWarning($"Gadget placement event: {ex.Message}"); }
                    return true;
                }
                catch
                {
                    if (GameState.TryGetIdentifiableModel(actorId, out var partial))
                        GameState.DestroyGadgetModel(partial.Cast<Il2CppMonomiPark.SlimeRancher.DataModel.GadgetModel>());
                    throw;
                }
            });
        // Always reconcile stock, including the losing client's stale view.
        Main.Server.SendToAll(new RefineryUpdatePacket { ItemID = NetworkActorManager.GetPersistentID(stockType),
            ItemCount = checked((ushort)director.GetItemCount(stockType)) });
        return accepted;
    }

    internal static void Complete(GadgetPlacementResult result)
    {
        if (pending != result.Request) return;
        pending = null;
        if (!result.Accepted) SrLogger.LogMessage("Gadget placement was not accepted: another player may have used the last item.");
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("GADGET SMOKE FAILED: " + message);
            SrLogger.LogMessage("GADGET SMOKE PASS: " + message);
        }
        var director = SceneContext.Instance.GadgetDirector;
        var pair = ActorManager.ActorTypes.First(p => p.Value != null && p.Value.TryCast<GadgetDefinition>() is { } def
            && def.prefab != null && !def.BuyInPairs && def.LinkedDefinition == null && director.IsDecor(def));
        var definition = pair.Value.Cast<GadgetDefinition>();
        var stockType = definition.BaseGadget ?? definition;
        var original = director.GetItemCount(stockType);
        var previous = HandlingPacket;
        smokeCreated = new();
        try
        {
            director._model.SetCount(stockType, 1);
            var first = new GadgetPlacementPacket { Request = Guid.NewGuid().ToString("N"), TypeID = pair.Key,
                SceneID = NetworkSceneManager.GetPersistentID(SystemContext.Instance.SceneLoader.CurrentSceneGroup),
                Position = SceneContext.Instance.PlayerState.transform.position + Vector3.up * 2, Rotation = Quaternion.identity };
            var second = new GadgetPlacementPacket { Request = Guid.NewGuid().ToString("N"), TypeID = first.TypeID,
                SceneID = first.SceneID, Position = first.Position + Vector3.right * 2, Rotation = first.Rotation };
            Check(Execute(first, "test-host") && !Execute(second, "test-guest") && director.GetItemCount(stockType) == 0 && smokeCreated.Count == 1,
                "two players competing for the last gadget create exactly one real gadget and consume one item");
            Check(Execute(first, "test-host") && smokeCreated.Count == 1 && director.GetItemCount(stockType) == 0,
                "retrying an accepted request cannot create or charge a second gadget");
            using var writer = PacketWriter.Borrow(); first.Serialise(writer);
            using var reader = PacketReader.Borrow(writer.ToSpan().ToArray());
            var decoded = new GadgetPlacementPacket(); decoded.Deserialise(reader);
            Check(decoded.Request == first.Request && decoded.TypeID == first.TypeID && decoded.Position == first.Position,
                "placement requests round-trip through the real packet serializer");
        }
        finally
        {
            HandlingPacket = true;
            foreach (var actorId in smokeCreated)
                if (GameState.TryGetIdentifiableModel(actorId, out var actor))
                {
                    var obj = actor.GetGameObject();
                    GameState.DestroyGadgetModel(actor.Cast<Il2CppMonomiPark.SlimeRancher.DataModel.GadgetModel>());
                    ActorManager.Actors.Remove(actorId.Value);
                    if (obj != null) Object.Destroy(obj);
                }
            director._model.SetCount(stockType, original);
            HandlingPacket = previous; smokeCreated = null; stock.Clear();
        }
    }
}

[HarmonyPatch(typeof(GadgetItem), "PlaceGadgetEvent")]
internal static class RequestGadgetPlacement
{
    internal static bool Prefix(GadgetItem __instance, InputEventData __0) => HostGadgetPlacement.PlaceFromMenu(__instance, __0);
}

[PacketHandler((byte)PacketType.GadgetPlacement, HandlerType.Server)]
internal sealed class GadgetPlacementHandler : BasePacketHandler<GadgetPlacementPacket>
{
    protected override bool Handle(GadgetPlacementPacket packet, IPEndPoint? endpoint)
    {
        if (endpoint == null || !Main.Server.ClientManager.TryGetClient(endpoint, out var client) || client == null) return false;
        var accepted = HostGadgetPlacement.Execute(packet, client.PlayerId);
        Main.Server.SendToClient(new GadgetPlacementResult { Request = packet.Request, Accepted = accepted }, endpoint);
        return false;
    }
}

[PacketHandler((byte)PacketType.GadgetPlacementResult, HandlerType.Client)]
internal sealed class GadgetPlacementResultHandler : BasePacketHandler<GadgetPlacementResult>
{
    protected override bool Handle(GadgetPlacementResult packet, IPEndPoint? endpoint)
    { HostGadgetPlacement.Complete(packet); return false; }
}
