using System.Collections;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Transport;

namespace SR2MP.Runtime;

internal static class OnlineSession
{
    internal static string Protocol => $"playground/1/{BuildInfo.Version}/{BuildInfo.UpdateBuild}";
    private static string? code;
    private static bool opening;
    private static long hostEpoch;
    internal static long Generation { get; private set; }
    internal static void Reset() { Generation++; code = null; }
    internal static bool IsOnline => code != null && Main.Server.IsRunning && Main.Server.SessionEpoch == hostEpoch;
    internal static bool CopyCode()
    {
        if (!IsOnline) return false;
        GUIUtility.systemCopyBuffer = code!;
        return true;
    }

    internal static void Open(PlaygroundPauseItem item)
    {
        if (IsOnline) { GUIUtility.systemCopyBuffer = code!; return; }
        if (opening) return;
        if (CoopSaveManager.Role != "host" || !GameHelpers.inGame || Main.Server.ClientManager.ClientCount != 0)
        { item.SetCaption("Open online before friends join"); return; }
        opening = true;
        item.SetCaption("Opening online lobby...");
        MelonLoader.MelonCoroutines.Start(OpenRoutine(item, Main.Server.SessionEpoch));
    }

    private static async Task<(OnlineLobbyTicket ticket, RelayDatagramTransport socket)> Prepare()
    {
        var ticket = await OnlineLobbyClient.CreateAsync(Protocol).ConfigureAwait(false);
        var socket = await RelayDatagramTransport.ConnectAsync(OnlineLobbyClient.SocketEndpoint,
            ticket.HostToken, role: "host", protocol: Protocol).ConfigureAwait(false);
        return (ticket, socket);
    }

    private static IEnumerator OpenRoutine(PlaygroundPauseItem item, long epoch)
    {
        var task = Prepare();
        while (!task.IsCompleted) yield return null;
        opening = false;
        if (task.IsFaulted || task.IsCanceled)
        { _ = task.Exception; item.SetCaption("Online unavailable - try again"); yield break; }
        var result = task.Result;
        if (!GameHelpers.inGame || CoopSaveManager.Role != "host"
            || Main.Server.SessionEpoch != epoch || Main.Server.ClientManager.ClientCount != 0)
        { result.socket.Dispose(); yield break; }
        if (!Main.Server.ReplaceTransport(result.socket))
        { result.socket.Dispose(); item.SetCaption("Online unavailable - try again"); yield break; }
        code = result.ticket.GuestCode;
        hostEpoch = Main.Server.SessionEpoch;
        item.SetCaption("Copy online code");
    }

    internal static IEnumerator TestCycle(PlaygroundPauseItem item)
    {
        if (!MenuSmokeTest.Enabled || !IsOnline) throw new InvalidOperationException("Open an isolated online host first.");
        var previousCode = code;
        var components = Resources.FindObjectsOfTypeAll<SR2MP.Components.Time.NetworkTime>().Length;
        var guest = OnlineLobbyClient.JoinAsync(code!, Protocol);
        while (!guest.IsCompleted) yield return null;
        if (!guest.IsCompletedSuccessfully) { _ = guest.Exception; throw new InvalidOperationException("Synthetic guest could not connect."); }
        guest.Result.Dispose();
        var deadline = Time.realtimeSinceStartup + 15;
        while (Main.Server.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
        if (Main.Server.IsRunning) throw new InvalidOperationException("Ended relay session left host transport running.");
        yield return null;
        Open(item);
        while (opening) yield return null;
        if (!IsOnline || code == previousCode) throw new InvalidOperationException("Host could not open a fresh lobby.");
        if (components != Resources.FindObjectsOfTypeAll<SR2MP.Components.Time.NetworkTime>().Length)
            throw new InvalidOperationException("Transport replacement duplicated world simulation components.");
        SrLogger.LogMessage("ONLINE CYCLE PASS: guest departure ended lobby; host reopened a fresh code without duplicating time components.");
    }
}
