using System.Net;
using System.Net.Sockets;

namespace SR2MP.Shared.Utils;

internal static class NetworkEndpoints
{
    internal static EndPoint ReceiveEndpoint(AddressFamily family) => family switch
    {
        AddressFamily.InterNetwork => new IPEndPoint(IPAddress.Any, 0),
        AddressFamily.InterNetworkV6 => new IPEndPoint(IPAddress.IPv6Any, 0),
        _ => throw new NotSupportedException("Unsupported datagram address family.")
    };
}
