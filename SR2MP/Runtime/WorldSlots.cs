using Il2CppMonomiPark.SlimeRancher.Persist;
using SR2MP.Shared.Managers;
using static SR2MP.Shared.Managers.CoopSaveManager;

namespace SR2MP.Runtime;

internal sealed record WorldSlot(string Role, int Index, Summary? Save, string Directory, string Fingerprint, string Label);
internal static class WorldSlots
{
    private static string Labels(string role,string directory) => role == "host"
        ? Path.Combine(Path.GetDirectoryName(directory)!,"WorldLabels")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playground","SlimeRancher2","WorldLabels","singleplayer",CoopFiles.Key(directory));
    internal static string DisplayLabel(string role,Summary save)
        => WorldLabels.Read(Labels(role,Storage.savePath),save.SaveIdentifier.GameName,"");
    internal static void Rename(WorldSlot slot,string label)
    {
        if(slot.Save==null || GameHelpers.inGame || Main.Server.IsRunning || Main.Client.IsConnected
            || WorldSlotFiles.Fingerprint(slot.Directory)!=slot.Fingerprint) throw new IOException("Select the world again before renaming.");
        WorldLabels.Write(Labels(slot.Role,slot.Directory),slot.Save.SaveIdentifier.GameName,label);
    }
    internal static WorldSlot[] Read(string role)
    {
        CoopSaveManager.SelectRole(role,false);
        var directory = Path.GetFullPath(Storage.savePath);
        WorldSlotFiles.Recover(directory);
        var fingerprint = WorldSlotFiles.Fingerprint(directory);
        var slots = new WorldSlot[6];
        var saves = new Summary?[6];
        foreach (var group in GameContext.Instance.AutoSaveDirector.AvailableGamesByGameName())
            foreach (var save in group.Value)
            {
                if (save.IsInvalid || save.SaveSlotIndex < 0 || save.SaveSlotIndex >= 6) continue;
                var old = saves[save.SaveSlotIndex];
                if (old == null || save.SaveTimestamp.CompareTo(old.SaveTimestamp) > 0) saves[save.SaveSlotIndex] = save;
            }
        for (var i = 0; i < 6; i++)
        {
            var save = saves[i];
            slots[i] = new WorldSlot(role,i,save,directory,fingerprint,save == null ? "Empty" :
                WorldLabels.Read(Labels(role,directory),save.SaveIdentifier.GameName,save.DisplayName));
        }
        return slots;
    }
    internal static void Copy(WorldSlot source, WorldSlot target)
    {
        if (source.Save == null || source.Role == target.Role) throw new InvalidOperationException("Choose an occupied source and an opposite destination.");
        if (GameHelpers.inGame || Main.Server.IsRunning || Main.Client.IsConnected) throw new InvalidOperationException("Return to the menu first.");
        if (WorldSlotFiles.Fingerprint(source.Directory) != source.Fingerprint || WorldSlotFiles.Fingerprint(target.Directory) != target.Fingerprint)
            throw new IOException("The saves changed. Select the slots again.");
        CoopSaveManager.SelectRole(source.Role,false);
        var input = new Il2CppSystem.IO.MemoryStream();
        var game = new GameV11();
        try { Storage.GetGameData(source.Save.SaveName,input); input.Position = 0; game.Load(input); }
        finally { input.Close(); }
        // Older native formats omit tables for features introduced later. Keep
        // every existing index unchanged; an absent table has zero entries.
        // Never rebuild these from today's catalog: saved numeric IDs use the
        // original ordering.
        game.IdentTypeIndex ??= Array.Empty<string>();
        game.ResourceGrowerIndex ??= Array.Empty<string>();
        game.GameIconIndex ??= Array.Empty<string>();
        game.ZoneIndex ??= Array.Empty<string>();
        game.SceneGroupIndex ??= Array.Empty<string>();
        game.WeatherPatternIndex ??= Array.Empty<string>();
        game.WeatherStateIndex ??= Array.Empty<string>();
        game.ComponentIndex ??= Array.Empty<string>();
        game.StatusEffectIndex ??= Array.Empty<string>();
        game.DisruptionAreaIndex ??= Array.Empty<string>();
        game.ThemeIndex ??= Array.Empty<string>();
        game.PlayerMusicIndex ??= Array.Empty<string>();
        game.PaletteIndex ??= Array.Empty<string>();
        // A copied world is a new world, even when reusing a destination slot.
        // Old guest inventories remain associated with their original world ID.
        var name = DateTime.UtcNow;
        string identity;
        do { identity = name.ToString("yyyyMMddHHmmss"); name = name.AddSeconds(1); }
        while (identity == source.Save.SaveIdentifier.GameName ||
            Directory.Exists(target.Directory) && Directory.EnumerateFiles(target.Directory,identity + "_*").Any());
        game.GameName = identity; game.SaveSlotIndex = target.Index;
        var output = new Il2CppSystem.IO.MemoryStream();
        byte[] bytes;
        try
        {
            game.Write(output);
            output.Position = 0;
            var verify = new GameV11(); verify.Load(output);
            if (verify.GameName != identity || verify.SaveSlotIndex != target.Index) throw new InvalidDataException("Copied save failed validation.");
            GameV11.AssertAreEqual(game,verify,true);
            bytes = output.ToArray();
        }
        finally { output.Close(); }
        CoopSaveManager.SelectRole(target.Role,false);
        var replaced = new List<string>();
        foreach (var group in GameContext.Instance.AutoSaveDirector.AvailableGamesByGameName())
        foreach (var save in group.Value)
            if (save.SaveSlotIndex == target.Index)
                replaced.Add(Path.GetFileName(Storage.GetFullFilePath(save.SaveName)));
        var fileName = identity + "_" + target.Index + "_0.sav";
        var backup = WorldSlotFiles.Publish(target.Directory,target.Fingerprint,fileName,bytes,replaced);
        try { WorldLabels.Write(Labels(target.Role,target.Directory),identity,source.Label); }
        catch (IOException ex) { SrLogger.LogWarning("World copied, but display label could not be saved: " + ex.Message); }
        SrLogger.LogMessage("Playground world copied. Destination backup: " + backup);

    }
    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || CoopSaveManager.Profile != "worldmanager-smoke")
            throw new InvalidOperationException("Use the isolated worldmanager-smoke profile.");
        var source=Read("singleplayer").First(s=>s.Save!=null);
        if(!source.Directory.Contains(Path.DirectorySeparatorChar + "tests" + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Test singleplayer storage was not isolated.");
        var original=WorldSlotFiles.Fingerprint(source.Directory);
        var target=Read("host")[5];
        Copy(source,target);
        var copied=Read("host")[5];
        if(copied.Save==null || copied.Save.SaveIdentifier.GameName==source.Save!.SaveIdentifier.GameName
            || WorldSlotFiles.Fingerprint(source.Directory)!=original)
            throw new InvalidDataException("Copy isolation verification failed.");
        Rename(copied,"Playground test world");
        copied=Read("host")[5];
        if(copied.Label!="Playground test world") throw new InvalidDataException("Sidecar rename failed.");
        var reverse=Read("singleplayer")[4];
        Copy(copied,reverse);
        var back=Read("singleplayer")[4];
        if(back.Save==null || back.Label!=copied.Label || back.Save.SaveIdentifier.GameName==copied.Save!.SaveIdentifier.GameName)
            throw new InvalidDataException("Reverse transfer failed.");
        Copy(copied,back);
        if(Read("singleplayer")[4].Save==null) throw new InvalidDataException("Overwrite failed.");
        SrLogger.LogMessage("WORLD SLOT SMOKE PASS: native roundtrip, both directions, overwrite, fresh identities and sidecar rename.");
    }
}
