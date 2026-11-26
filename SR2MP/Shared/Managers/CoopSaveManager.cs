using System.Text;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Persist;
using MelonLoader.Utils;

namespace SR2MP.Shared.Managers;

internal static class CoopSaveManager
{
    internal static readonly string Protocol = $"playground/1/{BuildInfo.Version}/{BuildInfo.UpdateBuild}";
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playground", "SlimeRancher2");
    internal static string Role { get; private set; } = "singleplayer";
    internal static string Profile { get; private set; } = "default";
    internal static bool Enabled => Role is "host" or "guest";
    internal static string? GuestWorld { get; private set; }
    internal static string WorldLabel { get; private set; } = "Not connected";
    private static bool guestWasBound;
    private static bool restorePending;
    private static bool sessionActive;
    private static string? localAccount;
    private static string? originalDirectory;

    internal static void Initialize()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--compass-role" && i + 1 < args.Length) Role = args[++i];
            else if (args[i] == "--compass-profile" && i + 1 < args.Length) Profile = args[++i];
        }
        if (Role != "singleplayer" && !Enabled) throw new InvalidDataException("Invalid co-op role.");
        if (Profile.Length is < 1 or > 48 || Profile.Any(c => !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) && c != '-'))
            throw new InvalidDataException("Invalid co-op profile.");
        // IL2CPP constructors cannot be reliably detoured. Guard native storage entry points
        // instead, before any profile/settings/save IO or initialization takes place.
        var harmony = new HarmonyLib.Harmony("compass.coop.storage");
        foreach (var name in new[] { "GetAvailableGames", "GetFullFilePath", "StoreDataStream", "LoadDataStream", "FileExists", "ToPath", "DeleteGameData", "DeleteGamesData" })
        {
            harmony.Patch(AccessTools.Method(typeof(FileStorageProvider), name),
                prefix: new HarmonyMethod(typeof(CoopSaveManager), nameof(EnsureStorage)));
        }
    }

    private static void EnsureStorage(FileStorageProvider __instance)
    {
        if (string.IsNullOrEmpty(__instance.savePath))
            throw new InvalidOperationException("Storage has not been initialized; refusing file IO.");
        var original = Path.GetFullPath(__instance.savePath);
        if (original.StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        originalDirectory ??= original;
        if (!Enabled) return;
        // Hash the original path so different store accounts do not share local guest profiles.
        localAccount = CoopFiles.Key(original);
        var redirected = Path.Combine(Root, Role, Profile, localAccount, "Saves");
        Directory.CreateDirectory(redirected);
        __instance.savePath = redirected;
        SrLogger.LogMessage($"Playground storage: {redirected}");
    }

    internal static void SelectRole(string role)
    {
        if (GameHelpers.inGame || Main.Server.IsRunning || Main.Client.IsConnected)
            throw new InvalidOperationException("Return to the main menu before selecting a multiplayer world.");
        if (role is not ("host" or "guest" or "singleplayer")) throw new ArgumentException(nameof(role));
        originalDirectory ??= Storage.savePath;
        Role = role;
        GuestWorld = null;
        guestWasBound = false;
        restorePending = false;
        Storage.savePath = originalDirectory;
        EnsureStorage(Storage);
        GameContext.Instance.AutoSaveDirector.LoadProfile();
    }

    internal static void EndSession()
    {
        // StartNewGame/BeginLoad may clear an earlier session before a world has
        // actually started. That must not switch new multiplayer IO to singleplayer.
        if (!sessionActive || !Enabled || originalDirectory == null) return;
        sessionActive = false;
        Role = "singleplayer";
        GuestWorld = null;
        guestWasBound = false;
        restorePending = false;
        WorldLabel = "Not connected";
        Storage.savePath = originalDirectory;
        GameContext.Instance.AutoSaveDirector.LoadProfile();
    }

    internal static void MarkSessionStarted() => sessionActive = Enabled;

    internal static void DeleteHostWorld(string gameName)
    {
        if (Role != "host" || GameHelpers.inGame || Main.Server.IsRunning || Main.Client.IsConnected || localAccount == null)
            throw new InvalidOperationException("Worlds can only be removed from the host world menu.");
        var expected = Path.GetFullPath(Path.Combine(Root, "host", Profile, localAccount, "Saves"));
        if (!string.Equals(Path.GetFullPath(Storage.savePath).TrimEnd(Path.DirectorySeparatorChar), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete outside multiplayer host storage.");
        if (string.IsNullOrWhiteSpace(gameName)) throw new ArgumentException(nameof(gameName));
        var director = GameContext.Instance.AutoSaveDirector;
        var exists = false;
        foreach (var group in director.AvailableGamesByGameName())
            if (group.Key == gameName) { exists = true; break; }
        if (!exists) throw new InvalidOperationException("The multiplayer world no longer exists.");
        // Let the game remove every autosave belonging to this world, including its
        // native backups. Never enumerate/delete files by a display-name prefix.
        director.DeleteGame(gameName);
    }

    internal static bool ImportSingleplayer(Summary save)
    {
        SelectRole("singleplayer");
        // Resolve through the game's storage provider rather than guessing extensions.
        var source = Path.GetFullPath(Storage.GetFullFilePath(save.SaveName));
        var sourceRoot = Path.GetFullPath(Storage.savePath);
        if (!string.Equals(Path.GetDirectoryName(source), sourceRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Save is outside the singleplayer directory.");
        var gameName = save.SaveIdentifier.GameName;
        SelectRole("host");
        // The native metadata keeps its world identity. A second import must never
        // merge with, roll back, or overwrite a previously played multiplayer copy.
        foreach (var group in GameContext.Instance.AutoSaveDirector.AvailableGamesByGameName())
            if (group.Key == gameName) return false;
        CoopFiles.CopySave(sourceRoot, Storage.savePath, Path.GetFileName(source));
        return true;
    }

    internal static bool CanNetwork(string role)
    {
        if (Role == role && (role != "guest" || !guestWasBound)) return true;
        SrLogger.LogWarning("Use the Compass co-op launcher with the correct role. Restart before joining another session.");
        return false;
    }

    internal static (string id, string name) HostWorld()
    {
        if (Role != "host") throw new InvalidOperationException("Not in a host profile.");
        var summary = GameContext.Instance.AutoSaveDirector.TryGetCurrentGameSummary();
        var metadata = GameContext.Instance.AutoSaveDirector._currentGameMetadata;
        if (!metadata.HasValue) throw new InvalidOperationException("Host world metadata is not ready.");
        var saveName = metadata.Value.GameName;
        if (string.IsNullOrEmpty(saveName)) throw new InvalidOperationException("Load a host world first.");
        var id = CoopFiles.Key(SR2MP.Shared.Utils.PlayerIdGenerator.GeneratePersistentPlayerId() + "|" + Profile + "|" + saveName);
        return (id, summary == null ? saveName : summary.DisplayName);
    }

    internal static void BindGuest(string id, string name)
    {
        if (Role != "guest" || guestWasBound || localAccount == null)
            throw new InvalidOperationException("Guest profile is not ready.");
        var folder = CoopFiles.Child(Path.Combine(Root, "guest", Profile, localAccount, "Worlds"), id);
        Directory.CreateDirectory(folder);
        // The name is metadata only, never a path supplied by another machine.
        CoopFiles.WriteAtomic(Path.Combine(folder, "world-name.txt"), Encoding.UTF8.GetBytes(name));
        Storage.savePath = folder;
        GuestWorld = folder;
        WorldLabel = name;
        guestWasBound = true;
        restorePending = true;
        RestorePlayer();
    }

    private static FileStorageProvider Storage
        => GameContext.Instance.AutoSaveDirector._storageProvider.Cast<FileStorageProvider>();

    // Player snapshots use the game's own serializer, preserving slime appearance/emotion metadata.
    internal static void SavePlayer()
    {
        if (GuestWorld == null || restorePending || !Main.Client.IsJoined) return;
        var player = new PlayerV12();
        var director = GameContext.Instance.AutoSaveDirector;
        GameModelPullHelpers.PullPlayer(GameState, player, director._saveReferenceTranslation.Cast<ISaveReferenceTranslation>(), director._savedGameInfoProvider);
        var stream = new Il2CppSystem.IO.MemoryStream();
        var writer = new Il2CppSystem.IO.BinaryWriter(stream);
        player.WriteData(writer);
        writer.Flush();
        CoopFiles.WriteAtomic(Path.Combine(GuestWorld, "player.bin"), stream.ToArray());
        writer.Dispose();
        stream.Close();
    }

    internal static System.Collections.IEnumerator SavePeriodically()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(15);
            try { SavePlayer(); }
            catch (Exception ex) { SrLogger.LogError($"Guest profile checkpoint failed: {ex}"); }
        }
    }

    private static void RestorePlayer()
    {
        var path = Path.Combine(GuestWorld!, "player.bin");
        if (File.Exists(path))
        {
            // Never silently replace a corrupt guest profile with a fresh one.
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length is < 1 or > 16777216) throw new InvalidDataException("Invalid guest profile size.");
            var stream = new Il2CppSystem.IO.MemoryStream(bytes);
            var reader = new Il2CppSystem.IO.BinaryReader(stream);
            var player = new PlayerV12();
            player.LoadData(reader);
            reader.Close();
            stream.Close();
            // Shared progression/currencies come from this session and the host, never
            // from an old guest snapshot. Only carry the guest's personal vacuum contents.
            var current = new PlayerV12();
            var director = GameContext.Instance.AutoSaveDirector;
            GameModelPullHelpers.PullPlayer(GameState, current,
                director._saveReferenceTranslation.Cast<ISaveReferenceTranslation>(), director._savedGameInfoProvider);
            current.Ammo = player.Ammo;
            var wasHandling = HandlingPacket;
            try
            {
                HandlingPacket = true;
                GameModelPushHelpers.PushPlayer(GameState, current,
                    director._saveReferenceTranslation.Cast<ILoadReferenceTranslation>());
                SceneContext.Instance.PlayerState.Ammo.SetModel(GameState.player.ammoModel);
            }
            finally { HandlingPacket = wasHandling; }
        }
        else
        {
            // The staging world's inventory must never migrate between host worlds.
            var wasHandling = HandlingPacket;
            try
            {
                HandlingPacket = true;
                SceneContext.Instance.PlayerState.Ammo.Clear();
            }
            finally { HandlingPacket = wasHandling; }
        }
        restorePending = false;
    }
}
