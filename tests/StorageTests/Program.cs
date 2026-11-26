using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using System.Net;
using System.Net.Sockets;
using SR2MP.Runtime;

if (LanSmoke.Run(args)) return;

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
    LanDiscoveryChecks.Run(Check);
    var steamRoot = Path.Combine(root, "Steam");
    var externalLibrary = Path.Combine(root, "Gry Łąka", "SteamLibrary");
    var externalGame = Path.Combine(externalLibrary, "steamapps", "common", "Custom SR2 Folder");
    Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
    Directory.CreateDirectory(Path.Combine(externalGame, "SlimeRancher2_Data"));
    File.WriteAllBytes(Path.Combine(externalGame, "SlimeRancher2.exe"), new byte[] { 1 });
    File.WriteAllBytes(Path.Combine(externalGame, "GameAssembly.dll"), new byte[] { 2 });
    var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
    var escapedLibrary = externalLibrary.Replace("\\", "\\\\");
    File.WriteAllText(vdfPath, "\"libraryfolders\" { \"1\" { \"path\" \"" + escapedLibrary + "\" } }");
    File.WriteAllText(Path.Combine(externalLibrary, "steamapps", "appmanifest_1657630.acf"), "\"AppState\" { \"appid\" \"1657630\" \"installdir\" \"Custom SR2 Folder\" }");
    Check(PlaygroundSetup.GameDiscovery.Find(new[] { steamRoot }, Array.Empty<string>()).SequenceEqual(new[] { externalGame }), "Steam discovery follows modern external-library VDF and actual manifest folder with Unicode paths");
    File.WriteAllText(vdfPath, "\"libraryfolders\" { \"1\" \"" + escapedLibrary + "\" }");
    Check(PlaygroundSetup.GameDiscovery.Find(new[] { steamRoot }, Array.Empty<string>()).SequenceEqual(new[] { externalGame }), "Steam discovery supports legacy numbered library entries");
    Check(PlaygroundSetup.GameDiscovery.Find(Array.Empty<string>(), new[] { externalGame }).SequenceEqual(new[] { externalGame }), "Steam uninstall registry location works without a Steam root");
    Check(PlaygroundSetup.GameDiscovery.Find(new[] { steamRoot, steamRoot }, new[] { externalGame }).Length == 1, "duplicate Steam registry roots do not trigger a manual selection");
    File.Delete(Path.Combine(externalGame, "GameAssembly.dll"));
    Check(PlaygroundSetup.GameDiscovery.Find(new[] { steamRoot }, new[] { externalGame }).Length == 0, "incomplete game installs are not selected automatically");
    Check(PlayerNames.Normalize("  Ola   Łąka ") == "Ola Łąka", "player names preserve diacritics and normalize spacing");
    Check(PlayerNames.Normalize("e\u0301") == "é", "player names normalize combining accents without losing characters");
    Check(PlayerNames.Normalize(new string('a', 40)).Length == 24, "player names have a bounded display length");
    Check(PlayerNames.Normalize("\n\t\u202e") == "", "empty and control-only player names cannot pass confirmation");
    Check(PlayerNames.Normalize("<b>Ola</b>") == "＜b＞Ola＜/b＞", "player names cannot inject rich text into labels");
    Check(PlayerNames.SanitizeWire("<color=#FFFFFF>Ola</color>") == "<color=#FFFFFF>Ola</color>", "network names preserve only the existing valid color wrapper");
    Check(PlayerNames.SanitizeWire("<color=#FFFFFF><size=99>Ola</size></color>").Contains("＜size=99＞"), "nested network name markup is rendered as plain text");
    var labels = Path.Combine(root, "host", "WorldLabels");
    var nativeSave = Path.Combine(root, "world_1.sav");
    var originalBytes = new byte[] { 1, 20, 255, 42 };
    File.WriteAllBytes(nativeSave, originalBytes);
    Check(WorldLabels.Read(labels, "1", "1") == "1" && !Directory.Exists(labels), "unrenamed worlds use native captions without creating metadata");
    WorldLabels.Write(labels, "1", "  Rodzinna   Łąka  ");
    WorldLabels.Write(labels, "2", "Second world");
    Check(WorldLabels.Read(labels, "1", "1") == "Rodzinna Łąka", "world labels persist Unicode names independently of native save names");
    var labelKey = Path.Combine(labels, CoopFiles.Key("1") + ".txt");
    WorldLabels.Write(labels, "1", "Renamed world");
    Check(File.Exists(labelKey) && WorldLabels.Read(labels, "1", "1") == "Renamed world" && WorldLabels.Read(labels, "2", "2") == "Second world", "renaming updates the same world metadata and preserves other worlds");
    Check(File.ReadAllBytes(nativeSave).SequenceEqual(originalBytes), "renaming never changes native save bytes or filenames");
    var blankRejected = false;
    try { WorldLabels.Write(labels, "1", " \u202e "); }
    catch (ArgumentException) { blankRejected = true; }
    Check(blankRejected && WorldLabels.Read(labels, "1", "1") == "Renamed world", "blank rename preserves the previous valid name");
    WorldLabels.Write(labels, "../outside", new string('a', 60));
    Check(WorldLabels.Read(labels, "../outside", "fallback").Length == 40 && File.Exists(Path.Combine(labels, CoopFiles.Key("../outside") + ".txt")), "world names are bounded and world IDs cannot escape metadata storage");
    File.WriteAllText(labelKey, new string('x', 1025));
    Check(WorldLabels.Read(labels, "1", "native") == "native", "oversized world metadata falls back to the native caption");
    Check(PlaygroundLocalization.Keys.Count() == 48, "all 48 native menu, world name, player name, LAN and update messages are in the translation catalog");
    foreach (var language in PlaygroundLocalization.Languages)
        Check(PlaygroundLocalization.Keys.All(key => !string.IsNullOrWhiteSpace(PlaygroundLocalization.Translate(language, key))), "complete menu translations: " + language);
    Check(PlaygroundLocalization.Translate("pt-BR", "Back") == "Voltar", "regional locale selects its language translation");
    Check(PlaygroundLocalization.Translate("pt_BR", "Back") == "Voltar", "underscore regional locales select their language instead of falling back to English");
    Check(PlaygroundLocalization.Translate("zh-Hans", "Cancel") == "取消", "Simplified Chinese locale uses Chinese translations");
    Check(PlaygroundLocalization.Translate("unknown", "Back") == "Back", "unknown locale safely falls back to English");
    Check(PlaygroundLocalization.Translate("ja", "Yes, delete: My Łąka 世界") == "削除する：My Łąka 世界", "localized deletion confirmation preserves the exact world name");
    Check(PlaygroundLocalization.Translate("fr", "Copy: Back") == "Copier : Back", "world names matching a menu label are not translated");
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
    var singleplayer = Path.Combine(root, "singleplayer");
    var multiplayer = Path.Combine(root, "multiplayer");
    Directory.CreateDirectory(singleplayer);
    var original = Path.Combine(singleplayer, "world_12.sav");
    File.WriteAllBytes(original, new byte[] { 10, 20, 30 });
    CoopFiles.CopySave(singleplayer, multiplayer, "world_12.sav");
    var imported = Path.Combine(multiplayer, "world_12.sav");
    Check(File.ReadAllBytes(imported).SequenceEqual(File.ReadAllBytes(original)), "import preserves the complete native save bytes");
    File.WriteAllBytes(imported, new byte[] { 40 });
    Check(File.ReadAllBytes(original).SequenceEqual(new byte[] { 10, 20, 30 }), "multiplayer progress cannot change the singleplayer original");
    var duplicateBlocked = false;
    try { CoopFiles.CopySave(singleplayer, multiplayer, "world_12.sav"); }
    catch (IOException) { duplicateBlocked = true; }
    Check(duplicateBlocked && File.ReadAllBytes(imported).SequenceEqual(new byte[] { 40 }), "repeated import preserves existing multiplayer progress");
    foreach (var badName in new[] { "../world_12.sav", "settings.cfg", "" })
    {
        var refused = false;
        try { CoopFiles.CopySave(singleplayer, multiplayer, badName); }
        catch (InvalidDataException) { refused = true; }
        Check(refused, "import rejects unsafe or non-save filename: " + badName);
    }
    var lockedImportBlocked = false;
    using (var locked = new FileStream(original, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        try { CoopFiles.CopySave(singleplayer, Path.Combine(root, "locked-import"), "world_12.sav"); }
        catch (IOException) { lockedImportBlocked = true; }
    }
    Check(lockedImportBlocked && !File.Exists(Path.Combine(root, "locked-import", "world_12.sav")), "busy source leaves no partially published multiplayer save");
    Check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "failed imports clean up temporary copies");
    Console.WriteLine($"{passed} storage assertions passed.");
}
finally { Directory.Delete(root, true); }
