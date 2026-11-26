using System.Diagnostics;
using SR2MP.Shared.Utils;

internal static class LanSmoke
{
    internal static bool Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--lan-fixture") return false;
        using var transport = new LanDiscoveryTransport();
        transport.StartHost(args[1], new string('f', 64), "TEST LAN discovery", 1919);
        Console.WriteLine("Started discovery-only fixture; no game session or game files.");
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < 240 && !File.Exists(args[2]))
        {
            transport.Tick(clock.Elapsed.TotalSeconds);
            System.Threading.Thread.Sleep(10);
        }
        Console.WriteLine("Stopped discovery-only fixture.");
        return true;
    }
}
