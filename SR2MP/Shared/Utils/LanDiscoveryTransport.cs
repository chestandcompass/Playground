using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SR2MP.Shared.Utils;

// Small, versioned UDP discovery protocol. No Unity calls, background threads,
// dependencies or advertised addresses: the reply's source supplies the IP.
internal sealed class LanDiscoveryTransport : IDisposable
{
    internal const int DefaultPort = 19190;
    private const string Header = "PLAYGROUND-LAN/1";
    private const int MaxPacket = 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly int discoveryPort;
    private readonly byte[] receiveBuffer = new byte[MaxPacket + 1];
    private readonly Dictionary<string, LanWorld> worlds = new();
    private Socket? host, browser;
    private string hostProtocol = "", worldId = "", worldName = "", browseProtocol = "", nonce = "";
    private ushort gamePort;
    private IPEndPoint[] targets = Array.Empty<IPEndPoint>();
    private double nextQuery, nextHostReply, searchStarted;
    internal string? Error { get; private set; }
    internal bool IsBrowsing => browser != null;
    internal bool IsHosting => host != null;
    internal int BoundHostPort => ((IPEndPoint)host!.LocalEndPoint!).Port;
    internal int BoundBrowserPort => ((IPEndPoint)browser!.LocalEndPoint!).Port;
    internal IReadOnlyCollection<LanWorld> Worlds => worlds.Values;
    internal bool Searching(double now) => browser != null && now - searchStarted < 5;

    internal LanDiscoveryTransport(int port = DefaultPort) => discoveryPort = port;

    internal void StartHost(string protocol, string id, string name, ushort port)
    {
        StopHost();
        if (!Token(protocol, 96) || !Hex(id, 64) || port == 0) throw new ArgumentException("Invalid LAN host description.");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try { socket.ExclusiveAddressUse = true; socket.Bind(new IPEndPoint(IPAddress.Any, discoveryPort)); socket.Blocking = false; }
        catch { socket.Dispose(); throw; }
        host = socket; hostProtocol = protocol; worldId = id; gamePort = port;
        worldName = CleanName(name);
        nextHostReply = 0;
        Error = null;
    }

    internal void StartBrowse(string protocol, IEnumerable<IPEndPoint> destinations, double now)
    {
        StopBrowse();
        if (!Token(protocol, 96)) throw new ArgumentException("Invalid LAN protocol.");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try { socket.EnableBroadcast = true; socket.Bind(new IPEndPoint(IPAddress.Any, 0)); socket.Blocking = false; }
        catch { socket.Dispose(); throw; }
        browser = socket; browseProtocol = protocol;
        nonce = Guid.NewGuid().ToString("N");
        targets = destinations.Where(e => e.AddressFamily == AddressFamily.InterNetwork && e.Port > 0).Take(32).ToArray();
        nextQuery = searchStarted = now;
        Error = null;
    }

    internal void Tick(double now)
    {
        if (host != null)
        {
            try { ReadHost(now); }
            catch (SocketException ex) { Error = ex.Message; StopHost(); }
        }
        if (browser == null) return;
        try
        {
            if (now >= nextQuery)
            {
                nextQuery = now + 1;
                var packet = Utf8.GetBytes(Header + "\nQ\n" + nonce + "\n" + browseProtocol);
                foreach (var target in targets)
                {
                    try { browser.SendTo(packet, target); }
                    catch (SocketException) { /* An unplugged adapter must not stop other probes. */ }
                }
            }
            ReadBrowser(now);
            foreach (var key in worlds.Where(p => now - p.Value.LastSeen >= 4).Select(p => p.Key).ToArray()) worlds.Remove(key);
        }
        catch (SocketException ex) { Error = ex.Message; StopBrowse(); }
    }

    private void ReadHost(double now)
    {
        for (var i = 0; i < 16 && host!.Poll(0, SelectMode.SelectRead); i++)
        {
            var count = Receive(host, out var sender);
            if (now < nextHostReply || count == 0 || count > MaxPacket || !IsLan(sender.Address)) continue;
            var fields = Fields(count);
            if (fields == null || fields.Length != 4 || fields[0] != Header || fields[1] != "Q"
                || !Hex(fields[2], 32) || fields[3] != hostProtocol) continue;
            nextHostReply = now + 0.05; // Bound response rate, including malformed/flooding peers.
            var reply = Header + "\nA\n" + fields[2] + "\n" + hostProtocol + "\n" + worldId + "\n"
                + gamePort.ToString(CultureInfo.InvariantCulture) + "\n" + Convert.ToBase64String(Utf8.GetBytes(worldName));
            host.SendTo(Utf8.GetBytes(reply), sender);
        }
    }

    private void ReadBrowser(double now)
    {
        for (var i = 0; i < 32 && browser!.Poll(0, SelectMode.SelectRead); i++)
        {
            var count = Receive(browser, out var endpoint);
            if (count == 0 || count > MaxPacket || !IsLan(endpoint.Address)) continue;
            var fields = Fields(count);
            if (fields == null || fields.Length != 7 || fields[0] != Header || fields[1] != "A" || fields[2] != nonce
                || fields[3] != browseProtocol || !Hex(fields[4], 64)
                || !ushort.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port == 0) continue;
            string name;
            try { name = Utf8.GetString(Convert.FromBase64String(fields[6])); }
            catch (Exception ex) when (ex is FormatException or DecoderFallbackException) { continue; }
            if (name.Length is < 1 or > 80 || name.Any(char.IsControl)) continue;
            if (worlds.Count >= 32 && !worlds.ContainsKey(fields[4])) continue;
            worlds[fields[4]] = new LanWorld(fields[4], CleanName(name), endpoint.Address, port, now);
        }
    }

    private string[]? Fields(int count)
    {
        try { return Utf8.GetString(receiveBuffer, 0, count).Split('\n'); }
        catch (DecoderFallbackException) { return null; }
    }

    private int Receive(Socket socket, out IPEndPoint sender)
    {
        EndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            var count = socket.ReceiveFrom(receiveBuffer, ref endpoint);
            sender = (IPEndPoint)endpoint;
            return count;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.MessageSize or SocketError.WouldBlock or SocketError.ConnectionReset)
        {
            sender = (IPEndPoint)endpoint;
            return 0;
        }
    }

    private static bool Token(string value, int max) => value.Length is > 0 && value.Length <= max && value.All(c => c >= 33 && c <= 126);
    private static bool Hex(string value, int length) => value.Length == length && value.All(Uri.IsHexDigit);
    private static bool IsLan(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || b[0] == 127 || (b[0] == 192 && b[1] == 168)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 169 && b[1] == 254));
    }
    private static string CleanName(string name)
    {
        var clean = new string((name ?? "").Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).Take(80).ToArray())
            .Replace('<', '＜').Replace('>', '＞');
        return string.IsNullOrWhiteSpace(clean) ? "Playground" : clean;
    }

    internal void StopHost() { host?.Dispose(); host = null; }
    internal void StopBrowse() { browser?.Dispose(); browser = null; worlds.Clear(); }
    public void Dispose() { StopHost(); StopBrowse(); }
}

internal sealed class LanWorld
{
    internal string Id { get; }
    internal string Name { get; }
    internal IPAddress Address { get; }
    internal ushort Port { get; }
    internal double LastSeen { get; }
    internal LanWorld(string id, string name, IPAddress address, ushort port, double seen)
    { Id = id; Name = name; Address = address; Port = port; LastSeen = seen; }
}
