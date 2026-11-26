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
            Main.Client.NotifyConnected();

            SrLogger.LogMessage($"Connection acknowledged by server! (PlayerId: {packet.PlayerId})");

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

        return false;
    }

    private static void SpawnPlayer(string id, string name)
    {
        var playerObject = Object.Instantiate(PlayerPrefab).GetComponent<NetworkPlayer>();
        playerObject.gameObject.SetActive(true);
        playerObject.ID = id;
        playerObject.gameObject.name = id;
        PlayerObjects.Add(id, playerObject.gameObject);
        PlayerManager.AddPlayer(id).Username = name;
        Object.DontDestroyOnLoad(playerObject);
    }
}
