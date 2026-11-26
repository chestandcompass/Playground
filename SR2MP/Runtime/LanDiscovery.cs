using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class LanDiscovery
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly LanDiscoveryTransport Transport = new();
    internal static double Now => Clock.Elapsed.TotalSeconds;
    internal static bool Browsing => Transport.IsBrowsing;
    internal static bool Searching => Transport.Searching(Now);
    internal static bool Failed { get; private set; }
    internal static IReadOnlyCollection<LanWorld> Worlds => Transport.Worlds;

    internal static void StartHosting()
    {
        try
        {
            var world = CoopSaveManager.HostWorld();
            Transport.StartHost(CoopSaveManager.Protocol, world.id, world.name, (ushort)Main.Server.Port);
            SrLogger.LogMessage("Playground LAN discovery is available on UDP 19190.");
        }
        catch (Exception ex) { SrLogger.LogWarning($"LAN discovery could not start; invitation codes still work: {ex.Message}"); }
    }

    internal static void StopHosting() => Transport.StopHost();
    internal static void StopBrowsing() => Transport.StopBrowse();
    internal static void StartBrowsing()
    {
        Failed = false;
        try
        {
            var addresses = new HashSet<IPAddress> { IPAddress.Broadcast };
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null) continue;
                    var ip = address.Address.GetAddressBytes();
                    var mask = address.IPv4Mask.GetAddressBytes();
                    addresses.Add(new IPAddress(ip.Select((b, i) => (byte)(b | ~mask[i])).ToArray()));
                }
            }
            Transport.StartBrowse(CoopSaveManager.Protocol,
                addresses.Select(a => new IPEndPoint(a, LanDiscoveryTransport.DefaultPort)), Now);
        }
        catch (Exception ex) { Failed = true; SrLogger.LogWarning($"LAN search failed: {ex.Message}"); }
    }

    internal static void Update()
    {
        if (Transport.IsHosting && !Main.Server.IsRunning) Transport.StopHost();
        Transport.Tick(Now);
        if (Transport.Error != null) Failed = true;
    }
}
