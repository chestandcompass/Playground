using System.Net;
using Il2CppMonomiPark.SlimeRancher.Economy;
using SR2MP.Api;
using SR2MP.Components.Player;
using SR2MP.Packets.Internal;
using SR2MP.Packets.Player;
using SR2MP.Packets.Utils;

namespace SR2MP.Handlers.Internal;

[PacketHandler((byte)PacketType.ConnectionApprove, HandlerType.Client)]
internal sealed class ConnectionApproveHandler : BasePacketHandler<ConnectionApprovePacket>
{
    protected override bool Handle(ConnectionApprovePacket packet, IPEndPoint? _)
    {
        try { HostWorldRules.Apply(packet.Rules); }
        catch (Exception ex)
        {
            SrLogger.LogError($"Host world rules could not be applied; disconnecting: {ex}");
            Main.Client.Disconnect();
            return false;
        }
        if (packet.InitialJoin)
        {
            try
            {
                SR2MP.Shared.Managers.CoopSaveManager.BindGuest(packet.WorldId, packet.WorldName);
            }
            catch (Exception ex)
            {
                SrLogger.LogError($"Guest save could not be loaded; connection stopped: {ex}");
                SessionGuard.Failure("Could not load your guest profile.");
                Main.Client.Disconnect();
                return false;
            }
            var joinPacket = new PlayerJoinPacket
            {
                Type = PacketType.PlayerJoin,
                PlayerId = packet.PlayerId,
                PlayerName = Main.Username
            };

            PacketSender.SendPacket(joinPacket);

            // SR2MPClient.StartHeartbeat();
            CheatsEnabled = packet.AllowCheats;

            // todo: resync players too
            foreach (var (id, username) in packet.OtherPlayers)
                SpawnPlayer(id, username);
        }

        HandlingPacket = true;
        SceneContext.Instance.PlayerState._model.SetCurrency(GameContext.Instance.LookupDirector._currencyList[0].Cast<ICurrency>(), packet.Money);
        SceneContext.Instance.PlayerState._model.SetCurrency(GameContext.Instance.LookupDirector._currencyList[1].Cast<ICurrency>(), packet.RainbowMoney);
        HandlingPacket = false;

        foreach (var data in packet.NetData)
        {
            ApiHandlers.SetNetId(data.ModId, data.NetId);
        }

        ApiHandlers.RefreshPacketMapping();

        if (packet.InitialJoin)
        {
            Main.Client.NotifyConnected();
            SrLogger.LogMessage($"Connection acknowledged by server! (PlayerId: {packet.PlayerId})");
        }

        return false;
    }

    private static void SpawnPlayer(string id, string name)
    {
        var playerObject = Object.Instantiate(PlayerPrefab).GetComponent<NetworkPlayer>();
        playerObject.ID = id;
        playerObject.gameObject.name = id;
        PlayerObjects.Add(id, playerObject.gameObject);
        PlayerManager.AddPlayer(id).Username = name;
        playerObject.gameObject.SetActive(true);
        Object.DontDestroyOnLoad(playerObject);
    }

    internal static System.Collections.IEnumerator SpawnSmoke()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        const string id = "PLAYER_SMOKE0001";
        SpawnPlayer(id, "Test player");
        var player = PlayerManager.GetPlayer(id)!;
        player.Position = SceneContext.Instance.player.transform.position + Vector3.forward * 4;
        player.SceneGroup = SR2MP.Shared.Managers.NetworkSceneManager.GetPersistentID(SystemContext.Instance.SceneLoader.CurrentSceneGroup);
        try
        {
            yield return new WaitForSecondsRealtime(1);
            var obj = PlayerObjects[id];
            var network = obj.GetComponent<NetworkPlayer>();
            if (!obj.activeInHierarchy || !obj.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null))
                throw new InvalidOperationException("Remote player has no active model.");
            SrLogger.LogMessage("REMOTE PLAYER SMOKE PASS: connection-approval path creates an active remote model");
            var marker = obj.GetComponent<Il2CppMonomiPark.SlimeRancher.UI.RadarTrackedPointOfInterest>();
            if (marker == null || !marker.enabled || network.UsernamePanel.text != "Test player")
                throw new InvalidOperationException("Remote player marker/name was not initialized.");
            SrLogger.LogMessage("REMOTE PLAYER SMOKE PASS: native compass marker and player name initialize after spawning");
        }
        finally
        {
            PlayerManager.RemovePlayer(id);
            if (PlayerObjects.Remove(id, out var obj)) Object.Destroy(obj);
        }
    }
}
