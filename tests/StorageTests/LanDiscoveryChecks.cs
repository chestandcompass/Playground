using System.Net;
using System.Net.Sockets;
using System.Text;
using SR2MP.Shared.Utils;

internal static class LanDiscoveryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const string protocol = "playground/test/9";
        var id = new string('a', 64);
        using var host = new LanDiscoveryTransport(0);
        using var guest = new LanDiscoveryTransport();
        host.StartHost(protocol, id, "<b>Łąka 世界</b>", 1919);
        var destination = new IPEndPoint(IPAddress.Loopback, host.BoundHostPort);
        guest.StartBrowse(protocol, new[] { destination }, 10);
        check(guest.Searching(10), "LAN search starts without blocking the menu");
        Pump(host, guest, 10);
        check(guest.Worlds.Count == 1, "real UDP host and browser discover a world without a join code");
        var world = guest.Worlds.Single();
        check(world.Address.Equals(IPAddress.Loopback) && world.Port == 1919 && world.Id == id,
            "LAN join uses the actual replying host IP and advertised game port");
        check(world.Name == "＜b＞Łąka 世界＜/b＞", "LAN world names preserve Unicode and escape TMP markup");
        Pump(host, guest, 11);
        check(guest.Worlds.Count == 1, "repeated LAN responses update one world instead of duplicating it");
        host.StopHost();
        guest.Tick(16);
        check(guest.Worlds.Count == 0 && !host.IsHosting, "closed hosts expire from the browser after four seconds");
        check(!guest.Searching(16), "empty search changes from searching to no games found");
        host.StartHost(protocol, id, "Garden", 1919);
        destination = new IPEndPoint(IPAddress.Loopback, host.BoundHostPort);
        guest.StartBrowse("playground/different-build", new[] { destination }, 20);
        Pump(host, guest, 20);
        check(guest.Worlds.Count == 0, "LAN hides hosts using a different Playground build");
        guest.StartBrowse(protocol, new[] { destination }, 30);
        using (var noise = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            noise.SendTo(new byte[4096], destination);
            noise.SendTo(new byte[] { 255, 254, 253 }, destination);
            noise.SendTo(Encoding.UTF8.GetBytes("PLAYGROUND-LAN/1\nQ\nbad-nonce\n" + protocol), destination);
            var guestEndpoint = new IPEndPoint(IPAddress.Loopback, guest.BoundBrowserPort);
            noise.SendTo(new byte[4096], guestEndpoint);
            noise.SendTo(Encoding.UTF8.GetBytes("PLAYGROUND-LAN/1\nA\n" + new string('b', 32) + "\n" + protocol + "\n" + id + "\n1919\nR2FyZGVu"), guestEndpoint);
            guest.Tick(30);
            check(guest.Worlds.Count == 0 && guest.IsBrowsing, "oversized and unsolicited LAN replies cannot populate or stop the browser");
        }
        Pump(host, guest, 30.1);
        check(host.IsHosting && guest.Worlds.Count == 1, "malformed and oversized LAN probes do not disable a running host");
        guest.StopBrowse();
        check(!guest.IsBrowsing && guest.Worlds.Count == 0, "leaving LAN search releases its socket and clears stale worlds");
    }

    private static void Pump(LanDiscoveryTransport host, LanDiscoveryTransport guest, double now)
    {
        for (var i = 0; i < 50; i++)
        {
            guest.Tick(now + i * 0.001);
            host.Tick(now + i * 0.001);
            System.Threading.Thread.Sleep(1);
        }
    }
}
