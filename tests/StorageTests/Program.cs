using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using System.Net;
using System.Net.Sockets;

var root = Path.Combine(Path.GetTempPath(), "CompassCoopTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS " + message);
    passed++;
}
try
{
    using (var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
    using (var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
    {
        receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        receiver.ReceiveTimeout = 1000;
        sender.SendTo(new byte[] { 42 }, receiver.LocalEndPoint!);
        var endpoint = NetworkEndpoints.ReceiveEndpoint(receiver.AddressFamily);
        var buffer = new byte[8];
        Check(receiver.ReceiveFrom(buffer, ref endpoint) == 1 && buffer[0] == 42,
            "IPv4 receiver accepts a real datagram using the shared endpoint factory");
    }
    Check(NetworkEndpoints.ReceiveEndpoint(AddressFamily.InterNetworkV6).AddressFamily == AddressFamily.InterNetworkV6,
        "IPv6 receive endpoint keeps the socket address family");
    var firstWorld = CoopFiles.Key("host-a|slot-1");
    var secondWorld = CoopFiles.Key("host-b|slot-1");
    Check(firstWorld != secondWorld, "identical save names on different hosts stay separate");
    Check(firstWorld == CoopFiles.Key("host-a|slot-1"), "world identity is stable on reconnect");
    var firstPath = CoopFiles.Child(root, firstWorld);
    var secondPath = CoopFiles.Child(root, secondWorld);
    foreach (var unsafeKey in new[] { "../outside", new string('x', 64), "", firstWorld + "/../" })
    {
        var rejected = false;
        try { CoopFiles.Child(root, unsafeKey); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "reject unsafe world identifier: " + unsafeKey);
    }
    var profile = Path.Combine(firstPath, "player.bin");
    CoopFiles.WriteAtomic(profile, new byte[] { 1, 2, 3 });
    CoopFiles.WriteAtomic(profile, new byte[] { 4, 5 });
    Check(File.ReadAllBytes(profile).SequenceEqual(new byte[] { 4, 5 }), "replacement publishes complete new snapshot");
    Check(File.ReadAllBytes(profile + ".previous").SequenceEqual(new byte[] { 1, 2, 3 }), "previous snapshot remains recoverable");
    CoopFiles.WriteAtomic(Path.Combine(secondPath, "player.bin"), new byte[] { 9 });
    Check(File.ReadAllBytes(profile).SequenceEqual(new byte[] { 4, 5 }), "second world cannot overwrite first world");
    var blocked = false;
    using (var locked = new FileStream(profile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        try { CoopFiles.WriteAtomic(profile, new byte[] { 0 }); }
        catch (IOException) { blocked = true; }
    }
    Check(blocked && File.ReadAllBytes(profile).SequenceEqual(new byte[] { 4, 5 }), "failed replacement preserves last successful snapshot");
    Check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "failed writes leave no temporary files");
    Console.WriteLine($"{passed} storage assertions passed.");
}
finally { Directory.Delete(root, true); }
