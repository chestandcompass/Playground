using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SR2MP.Shared.Transport;

var passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    passed++; Console.WriteLine("PASS " + label);
}
void Refused(Action action, string label)
{
    var refused = false;
    try { action(); } catch (InvalidDataException) { refused = true; }
    Check(refused, label);
}
var packets = new[] { new byte[] { 1 }, Enumerable.Range(0,2048).Select(i=>(byte)i).ToArray(), new byte[] { 9,8,7 } };
var frame = DatagramBatch.Encode(packets);
var decoded = DatagramBatch.Decode(frame);
Check(decoded.Length == packets.Length && decoded.Zip(packets).All(p=>p.First.SequenceEqual(p.Second)), "batch retains datagram order, lengths and contents");
Refused(()=>DatagramBatch.Encode(Array.Empty<byte[]>()),"empty batch refused");
Refused(()=>DatagramBatch.Encode(new[]{Array.Empty<byte>()}),"zero-length datagram refused");
Refused(()=>DatagramBatch.Encode(new[]{new byte[2049]}),"oversized datagram refused");
Refused(()=>DatagramBatch.Encode(Enumerable.Range(0,513).Select(_=>new byte[]{1}).ToArray()),"excessive datagram count refused");
Refused(()=>DatagramBatch.Encode(Enumerable.Range(0,9).Select(_=>new byte[2048]).ToArray()),"oversized frame refused");
Refused(()=>DatagramBatch.Decode(frame[..^1]),"truncated frame refused");
Refused(()=>DatagramBatch.Decode(frame.Concat(new byte[]{0}).ToArray()),"trailing bytes refused");
var bad=(byte[])frame.Clone(); bad[3]=2; Refused(()=>DatagramBatch.Decode(bad),"unknown version refused");
var random = new Random(5149);
for(var i=0;i<500;i++)
{
    var bytes=new byte[random.Next(0,18000)]; random.NextBytes(bytes);
    try { DatagramBatch.Decode(bytes); throw new Exception("Random frame accepted"); } catch(InvalidDataException) { }
}
Check(true,"500 malformed random frames rejected without partial output");
using(var serverSocket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
using(var guestSocket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
using(var server=new UdpDatagramTransport(serverSocket))
using(var guest=new UdpDatagramTransport(guestSocket))
{
    serverSocket.Client.ReceiveTimeout=2000; guestSocket.Client.ReceiveTimeout=2000;
    var endpoint=(IPEndPoint)serverSocket.Client.LocalEndPoint!;
    guest.Connect(endpoint); guest.Send(new ArraySegment<byte>(packets[1]),endpoint);
    EndPoint sender=new IPEndPoint(IPAddress.Any,0); var buffer=new byte[2048];
    var count=server.Receive(buffer,ref sender);
    Check(count==2048 && buffer.SequenceEqual(packets[1]),"LAN transport retains a full datagram");
    server.Send(new ArraySegment<byte>(packets[2]),(IPEndPoint)sender);
    count=guest.Receive(buffer,ref sender);
    Check(count==3 && buffer.Take(count).SequenceEqual(packets[2]) && sender.Equals(endpoint),"connected LAN socket retains reply peer and data");
}
if(args.Length==3 && args[0]=="--probe")
{
    var origin=new Uri(args[1]);
    if(origin.Scheme!="https") throw new Exception("HTTPS required");
    var key=File.ReadAllText(args[2]).Trim();
    using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(15)};
    http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
    using var response=await http.PostAsync(new Uri(origin,"/rooms"),null);
    response.EnsureSuccessStatusCode();
    using var room=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var endpoint=new UriBuilder(origin){Scheme="wss",Path="/connect",Port=-1}.Uri;
    using var host=await RelayDatagramTransport.ConnectAsync(endpoint,room.RootElement.GetProperty("host").GetString()!,key);
    using var guest=await RelayDatagramTransport.ConnectAsync(endpoint,room.RootElement.GetProperty("guest").GetString()!,key);
    var burst=Enumerable.Range(0,240).Select(i=>
    {
        var packet=new byte[8+(i%13)*110]; random.NextBytes(packet); BitConverter.GetBytes(i).CopyTo(packet,0); return packet;
    }).ToArray();
    var echo=Task.Run(()=>
    {
        var buffer=new byte[2048]; EndPoint peer=RelayDatagramTransport.Peer;
        for(var i=0;i<burst.Length;i++)
        {
            var length=guest.Receive(buffer,ref peer);
            if(!buffer.Take(length).SequenceEqual(burst[i])) throw new Exception("Relayed datagram differs");
            guest.Send(new ArraySegment<byte>(buffer,0,length),RelayDatagramTransport.Peer);
        }
    });
    var returned=Task.Run(()=>
    {
        var buffer=new byte[2048]; EndPoint peer=RelayDatagramTransport.Peer;
        for(var i=0;i<burst.Length;i++)
        {
            var length=host.Receive(buffer,ref peer);
            if(!buffer.Take(length).SequenceEqual(burst[i])) throw new Exception("Echo differs");
        }
    });
    var started=System.Diagnostics.Stopwatch.StartNew();
    foreach(var packet in burst) host.Send(new ArraySegment<byte>(packet),RelayDatagramTransport.Peer);
    await Task.WhenAll(echo,returned).WaitAsync(TimeSpan.FromSeconds(30));
    Check(true,"real relay preserves 240 mixed-size datagrams in both directions");
    var counters=host.Counters;
    Check(counters.framesOut<burst.Length/4,"batching reduces outgoing WebSocket message count");
    Console.WriteLine($"Relay burst roundtrip: {started.ElapsedMilliseconds} ms; host frames={counters.framesOut}; bytes={counters.bytesOut}");
    host.Close();
    var closed=Task.Run(()=>
    {
        EndPoint peer=RelayDatagramTransport.Peer;
        try { guest.Receive(new byte[2048],ref peer); return false; } catch(IOException) { return true; }
    });
    Check(await closed.WaitAsync(TimeSpan.FromSeconds(15)),"host loss interrupts a blocked guest receive");
}
if(args.Length==1 && (args[0]=="--online" || args[0]=="--online-guest-close"))
{
    const string protocol="playground/1/0.1.0/22";
    var ticket=await OnlineLobbyClient.CreateAsync(protocol);
    Check(OnlineLobbyClient.TryToken(ticket.GuestCode,out _),"online lobby returns a bounded share code");
    var absent=false;
    try {using var invalid=await OnlineLobbyClient.JoinAsync(ticket.GuestCode,protocol);} catch(IOException) {absent=true;}
    Check(absent,"guest cannot join before the host socket opens");
    using var host=await RelayDatagramTransport.ConnectAsync(OnlineLobbyClient.SocketEndpoint,ticket.HostToken,role:"host",protocol:protocol);
    var mismatch=false;
    try {using var invalid=await OnlineLobbyClient.JoinAsync(ticket.GuestCode,"playground/1/0.1.0/999999");} catch(IOException) {mismatch=true;}
    Check(mismatch,"online lobby rejects a different mod protocol before gameplay");
    using var guest=await OnlineLobbyClient.JoinAsync(ticket.GuestCode,protocol);
    var burst=Enumerable.Range(0,128).Select(i=>new byte[100+i*7]).ToArray();
    foreach(var packet in burst) random.NextBytes(packet);
    var echo=Task.Run(()=>
    {
        EndPoint peer=RelayDatagramTransport.Peer;var buffer=new byte[2048];
        foreach(var expected in burst)
        {
            var size=guest.Receive(buffer,ref peer);
            if(!buffer.Take(size).SequenceEqual(expected)) throw new Exception("Online datagram differs");
            guest.Send(new ArraySegment<byte>(buffer,0,size),RelayDatagramTransport.Peer);
        }
    });
    var replies=Task.Run(()=>
    {
        EndPoint peer=RelayDatagramTransport.Peer;var buffer=new byte[2048];
        foreach(var expected in burst)
        {
            var size=host.Receive(buffer,ref peer);
            if(!buffer.Take(size).SequenceEqual(expected)) throw new Exception("Online echo differs");
        }
    });
    foreach(var packet in burst) host.Send(new ArraySegment<byte>(packet),RelayDatagramTransport.Peer);
    await Task.WhenAll(echo,replies).WaitAsync(TimeSpan.FromSeconds(20));
    Check(true,"public capability-based lobby relays 128 intact datagrams each way without a probe key");
    var guestLeaves=args[0]=="--online-guest-close";
    if(guestLeaves) guest.Close(); else host.Close();
    var closed=Task.Run(()=>
    {
        EndPoint peer=RelayDatagramTransport.Peer;
        try {(guestLeaves?host:guest).Receive(new byte[2048],ref peer);return false;} catch(IOException){return true;}
    });
    Check(await closed.WaitAsync(TimeSpan.FromSeconds(15)),guestLeaves?"guest departure ends the old host lobby":"online host departure closes the guest connection");
    var ended=false;
    try {using var invalid=await OnlineLobbyClient.JoinAsync(ticket.GuestCode,protocol);} catch(Exception ex) when(ex is IOException or System.Net.WebSockets.WebSocketException){ended=true;}
    Check(ended,"ended lobby code cannot attach a new guest to an old packet session");
}
Console.WriteLine($"{passed} transport checks passed.");
