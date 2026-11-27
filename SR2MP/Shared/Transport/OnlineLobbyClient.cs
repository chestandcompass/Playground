using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SR2MP.Shared.Transport;

internal sealed class OnlineLobbyTicket
{
    internal string HostToken { get; init; } = "";
    internal string GuestCode { get; init; } = "";
    internal long Expires { get; init; }
    public override string ToString() => "Playground online lobby";
}
internal static class OnlineLobbyClient
{
    internal static readonly Uri Origin = new("https://playground-relay.chestandcompass.workers.dev");
    internal static readonly Uri SocketEndpoint = new("wss://playground-relay.chestandcompass.workers.dev/connect");
    private static readonly HttpClient http = new() {Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=16384};
    internal static bool TryToken(string code, out string token)
    {
        token="";
        code=code.Trim();
        if(!code.StartsWith("PGO1-",StringComparison.Ordinal) || code.Length!=37) return false;
        var candidate=code.Substring(5);
        if(candidate.Any(c=>!((c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='_'||c=='-'))) return false;
        token=candidate;return true;
    }
    internal static async Task<OnlineLobbyTicket> CreateAsync(string protocol)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(Origin,"/rooms"));
        request.Headers.Add("X-Playground-Protocol",protocol);
        using var response=await http.SendAsync(request).ConfigureAwait(false);
        RequireSuccess(response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var ticket=new OnlineLobbyTicket
        {
            HostToken=json.RootElement.GetProperty("host").GetString()!,
            GuestCode=json.RootElement.GetProperty("code").GetString()!,
            Expires=json.RootElement.GetProperty("expires").GetInt64()
        };
        if(!TryToken(ticket.GuestCode,out _) || ticket.HostToken.Length!=32) throw new InvalidDataException("Invalid lobby response.");
        return ticket;
    }
    internal static async Task<RelayDatagramTransport> JoinAsync(string code,string protocol)
    {
        if(!TryToken(code,out var token)) throw new InvalidDataException("Invalid online code.");
        using var request=new HttpRequestMessage(HttpMethod.Post,new Uri(Origin,"/check"));
        request.Headers.Add("X-Playground-Protocol",protocol);
        request.Headers.Add("X-Room-Role","guest");
        request.Headers.Add("X-Room-Token",token);
        using var response=await http.SendAsync(request).ConfigureAwait(false);
        RequireSuccess(response.StatusCode);
        return await RelayDatagramTransport.ConnectAsync(SocketEndpoint,token,role:"guest",protocol:protocol).ConfigureAwait(false);
    }
    private static void RequireSuccess(HttpStatusCode status)
    {
        if(status==HttpStatusCode.OK) return;
        throw new IOException(status switch
        {
            HttpStatusCode.TooManyRequests=>"The free online relay is busy. Try again later.",
            HttpStatusCode.Conflict=>"The host is unavailable or has a different Playground version.",
            HttpStatusCode.Forbidden or HttpStatusCode.Gone=>"The online code is invalid or expired.",
            _=>"The online relay is unavailable."
        });
    }
}
