using SR2MP.Shared.Managers;
internal static class WorldSlotChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var directory=Path.Combine(Path.GetTempPath(),"Playground-slot-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var root=Path.Combine(directory,"Saves");Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"old_0_0.sav"),"original");
            File.WriteAllText(Path.Combine(root,"other_1_0.sav"),"other world");
            File.WriteAllText(Path.Combine(root,"settings.cfg"),"settings");
            var fingerprint=WorldSlotFiles.Fingerprint(root);
            var failed=false;
            try{WorldSlotFiles.Publish(root,fingerprint,"new_0_0.sav",new byte[]{1,2},new[]{"old_0_0.sav"},()=>throw new IOException("Injected"));}
            catch(IOException){failed=true;}
            check(failed && File.ReadAllText(Path.Combine(root,"old_0_0.sav"))=="original","slot transfer rolls back when publishing fails");
            var backup=WorldSlotFiles.Publish(root,fingerprint,"new_0_0.sav",new byte[]{1,2},new[]{"old_0_0.sav"});
            check(File.ReadAllText(Path.Combine(backup,"old_0_0.sav"))=="original","overwritten world retained in complete backup");
            check(!File.Exists(Path.Combine(root,"old_0_0.sav")) && File.ReadAllBytes(Path.Combine(root,"new_0_0.sav")).SequenceEqual(new byte[]{1,2}),"slot publishes new save and removes only replaced autosaves");
            check(File.ReadAllText(Path.Combine(root,"other_1_0.sav"))=="other world" && File.ReadAllText(Path.Combine(root,"settings.cfg"))=="settings","other slots and settings remain unchanged");
            var stale=false;try{WorldSlotFiles.Publish(root,fingerprint,"bad.sav",new byte[]{0},Array.Empty<string>());}catch(IOException){stale=true;}
            check(stale && !File.Exists(Path.Combine(root,"bad.sav")),"stale confirmation cannot overwrite changed saves");
            var unsafeName=false;try{WorldSlotFiles.Publish(root,WorldSlotFiles.Fingerprint(root),"../bad.sav",new byte[]{0},Array.Empty<string>());}catch(InvalidDataException){unsafeName=true;}
            check(unsafeName,"slot transaction refuses path traversal");
            var settingsFingerprint=WorldSlotFiles.Fingerprint(root);
            File.WriteAllText(Path.Combine(root,"settings.cfg"),"updated settings");
            check(WorldSlotFiles.Fingerprint(root)!=settingsFingerprint,"confirmation detects changed profile settings too");
            var beforeCrash=WorldSlotFiles.Fingerprint(root);
            var crashBackup=root+".backup.crash";
            var crashStage=root+".stage.crash";
            Directory.CreateDirectory(crashStage);
            File.WriteAllText(Path.Combine(crashStage,"partial.sav"),"incomplete");
            Directory.Move(root,crashBackup);
            File.WriteAllText(root+".playground-transfer.json",System.Text.Json.JsonSerializer.Serialize(new {Root=root,Stage=crashStage,Backup=crashBackup}));
            WorldSlotFiles.Recover(root);
            check(WorldSlotFiles.Fingerprint(root)==beforeCrash && Directory.Exists(crashStage),"crash recovery restores original and retains interrupted stage");
        }
        finally{Directory.Delete(directory,true);}
    }
}
