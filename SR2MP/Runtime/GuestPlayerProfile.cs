using Il2CppMonomiPark.SlimeRancher.Persist;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class GuestPlayerProfile
{
    private static GameV11 References()
    {
        var game = new GameV11();
        GameContext.Instance.AutoSaveDirector._saveReferenceTranslation.PrepareNewGameForPull(game);
        return game;
    }

    private static byte[] Serialize(Action<Il2CppSystem.IO.BinaryWriter> write)
    {
        var stream = new Il2CppSystem.IO.MemoryStream();
        var writer = new Il2CppSystem.IO.BinaryWriter(stream);
        try { write(writer); writer.Flush(); return stream.ToArray(); }
        finally { writer.Dispose(); stream.Close(); }
    }

    internal static byte[] Capture()
    {
        var references = References();
        var player = new PlayerV12();
        var director = GameContext.Instance.AutoSaveDirector;
        GameModelPullHelpers.PullPlayer(GameState, player, director._saveReferenceTranslation.Cast<ISaveReferenceTranslation>(), director._savedGameInfoProvider);
        var tables = Serialize(writer => { references.SummaryIndexTableLookup.WriteData(writer); references.MainIndexTableLookup.WriteData(writer); });
        return GuestProfileEnvelope.Write(tables, Serialize(player.WriteData));
    }

    internal static void Restore(byte[] bytes)
    {
        var data = GuestProfileEnvelope.Read(bytes);
        var references = References();
        void Read(byte[] payload, Action<Il2CppSystem.IO.BinaryReader> read)
        {
            var stream = new Il2CppSystem.IO.MemoryStream(payload);
            var reader = new Il2CppSystem.IO.BinaryReader(stream);
            try { read(reader); }
            finally { reader.Close(); stream.Close(); }
        }
        if (data.references != null)
            Read(data.references, reader => { references.SummaryIndexTableLookup.LoadData(reader); references.MainIndexTableLookup.LoadData(reader); });
        var player = new PlayerV12();
        Read(data.player, player.LoadData);
        if (player.Ammo == null || player.Ammo.Count > 64 || player.Ammo.ToArray().Any(a => a == null || a.Count < 0 || a.Count > 1000000))
            throw new InvalidDataException("Invalid guest inventory.");
        // SaveReferenceTranslation implements the SAVE interface only. Its native
        // factory builds the matching LOAD translator from persistence tables.
        var load = GameContext.Instance.AutoSaveDirector._saveReferenceTranslation.CreateLoadReferenceTranslation(references);
        var decoded = GameModelPushHelpers.AmmoDataToSlots(player.Ammo, load);
        var ammo = SceneContext.Instance.PlayerState.Ammo;
        if (decoded.Length > ammo.Slots.Length || decoded.Any(s => s == null || (s.Count > 0 && s.Id == null)))
            throw new InvalidDataException("Guest inventory cannot be restored to the current slots.");
        // Keep host progression, currencies, position, health, slot definitions,
        // capacity and unlock state. Only restore personal vacuum contents.
        var previous = HandlingPacket;
        try
        {
            HandlingPacket = true;
            for (var i = 0; i < ammo.Slots.Length; i++)
            {
                var target = ammo.Slots[i];
                if (i >= decoded.Length) { target.Clear(); continue; }
                var source = decoded[i];
                target.Emotions = source.Emotions; target.Appearance = source.Appearance; target.Radiant = source.Radiant;
                target._id = source.Count == 0 ? null : source.Id;
                target.Count = source.Count;
            }
        }
        finally { HandlingPacket = previous; }
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("GUEST PROFILE SMOKE FAILED: " + message);
            SrLogger.LogMessage("GUEST PROFILE SMOKE PASS: " + message);
        }
        PlayerV12 Pull()
        {
            var player = new PlayerV12(); var director = GameContext.Instance.AutoSaveDirector;
            GameModelPullHelpers.PullPlayer(GameState, player, director._saveReferenceTranslation.Cast<ISaveReferenceTranslation>(), director._savedGameInfoProvider);
            return player;
        }
        var original = Capture();
        var before = Pull();
        var beforeBytes = Serialize(before.WriteData);
        try
        {
            var path = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Playground", "MenuTest", "guest-profile-fixture.bin");
            var fixture = File.Exists(path) ? File.ReadAllBytes(path) : Serialize(before.WriteData);
            Restore(fixture);
            Check(true, "the reported legacy guest profile restores using the native load translator without InvalidCastException");
            var after = Pull(); after.Ammo = before.Ammo;
            Check(Serialize(after.WriteData).SequenceEqual(beforeBytes), "guest restore leaves currencies, health, position and shared progression unchanged");
            var type = ActorManager.ActorTypes.Values.First(t => t != null && t.name.Contains("Plort"));
            var slot = SceneContext.Instance.PlayerState.Ammo.Slots[0];
            slot._id = type; slot.Count = 3;
            var saved = Capture();
            Check(GuestProfileEnvelope.Read(saved).references != null, "new profile contains self-contained native reference tables");
            slot.Clear(); Restore(saved);
            Check(slot.Id.Pointer == type.Pointer && slot.Count == 3, "non-empty inventory round-trips through native serialization and matching reference tables");
            var corrupt = saved.ToArray(); corrupt[corrupt.Length - 1] ^= 1;
            var rejected = false;
            try { Restore(corrupt); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && slot.Count == 3 && slot.Id.Pointer == type.Pointer, "corrupt profile is rejected before changing inventory");
        }
        finally { Restore(original); }
    }
}
