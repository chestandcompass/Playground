using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class UpdateManager
{
    internal const string FeedUrl = "https://raw.githubusercontent.com/robertbrania-code/Playground/main/update.json";
    private const string ReleasePrefix = "https://github.com/robertbrania-code/Playground/releases/download/";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static bool busy;
    private static bool scheduled;
    private static string state = "check";
    private static UpdateFeed? available;

    internal sealed class UpdateFeed
    {
        public int Schema { get; set; }
        public string Version { get; set; } = "";
        public int Build { get; set; }
        public string GameVersion { get; set; } = "";
        public string DllUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string SetupUrl { get; set; } = "";
        public string SetupSha256 { get; set; } = "";
    }

    internal static string Caption => state switch
    {
        "checking" => NativeMenu.Polish ? "Sprawdzam aktualizacje..." : "Checking for updates...",
        "download" => NativeMenu.Polish ? "Pobieram aktualizację..." : "Downloading update...",
        "available" => NativeMenu.Polish ? "Zainstaluj aktualizację po wyjściu" : "Update after closing the game",
        "scheduled" => NativeMenu.Polish ? "Aktualizacja po wyjściu z gry" : "Update scheduled after exit",
        "latest" => NativeMenu.Polish ? "Masz aktualną wersję" : "You're up to date",
        "incompatible" => NativeMenu.Polish ? "Aktualizacja wymaga innej wersji gry" : "Update needs a different game version",
        "error" => NativeMenu.Polish ? "Ponów sprawdzanie aktualizacji" : "Try checking for updates again",
        _ => NativeMenu.Polish ? "Sprawdź aktualizacje" : "Check for updates"
    };

    internal static void Activate()
    {
        if (busy || scheduled) return;
        if (available != null) Schedule();
        else Check();
    }

    internal static void Check()
    {
        if (busy || scheduled) return;
        busy = true;
        state = "checking";
        var gameVersion = Application.version;
        _ = Task.Run(async () =>
        {
            UpdateFeed? feed = null;
            var result = "error";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
                request.Headers.UserAgent.ParseAdd("Playground/0.1.0");
                using var response = await Client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsByteArrayAsync();
                if (json.Length > 65536) throw new InvalidDataException("Update feed is too large.");
                feed = JsonSerializer.Deserialize<UpdateFeed>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (feed == null || feed.Schema != 1 || !Version.TryParse(feed.Version, out var version) || feed.Build < 1 ||
                    !feed.DllUrl.StartsWith(ReleasePrefix, StringComparison.Ordinal) || feed.Sha256.Length != 64)
                    throw new InvalidDataException("Invalid update feed.");
                if (!gameVersion.StartsWith(feed.GameVersion + " ", StringComparison.Ordinal) && gameVersion != feed.GameVersion)
                    result = "incompatible";
                else
                    result = version > new Version(BuildInfo.Version) ||
                        (version == new Version(BuildInfo.Version) && feed.Build > BuildInfo.UpdateBuild) ? "available" : "latest";
            }
            catch (Exception ex) { SrLogger.LogDebug($"Update check: {ex.Message}"); }
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                available = result == "available" ? feed : null;
                state = result;
                busy = false;
                NativeMenu.RefreshUpdateStatus();
            });
        });
    }

    private static void Schedule()
    {
        var feed = available!;
        busy = true;
        state = "download";
        var process = Process.GetCurrentProcess();
        var game = Path.GetDirectoryName(process.MainModule!.FileName)!;
        var processId = process.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                if (!feed.SetupUrl.StartsWith(ReleasePrefix, StringComparison.Ordinal) || feed.SetupSha256.Length != 64 ||
                    feed.SetupSha256.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid installer update.");
                var directory = Path.Combine(game, "PlaygroundUpdates", feed.Version + "-" + feed.Build);
                Directory.CreateDirectory(directory);
                var installer = Path.Combine(directory, "PlaygroundSetup.exe");
                var bytes = await Client.GetByteArrayAsync(feed.SetupUrl);
                if (bytes.Length > 104857600 || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(feed.SetupSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer checksum mismatch.");
                File.WriteAllBytes(installer, bytes);
                Process.Start(new ProcessStartInfo(installer)
                {
                    Arguments = "--apply-update --game \"" + game + "\" --wait-pid " + processId,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                MainThreadDispatcher.Instance.Enqueue(() =>
                {
                    state = "scheduled";
                    scheduled = true;
                    busy = false;
                    NativeMenu.RefreshUpdateStatus();
                });
            }
            catch (Exception ex)
            {
                SrLogger.LogWarning($"Update download failed: {ex.Message}");
                MainThreadDispatcher.Instance.Enqueue(() => { busy = false; state = "error"; NativeMenu.RefreshUpdateStatus(); });
            }
        });
    }
}
