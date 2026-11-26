using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher.Script.UI.Pause;

namespace SR2MP.Patches.General;

[HarmonyPatch(typeof(PauseMenuDirector), nameof(PauseMenuDirector.Quit))]
internal static class OnQuitSave
{
    public static void Prefix()
    {
        SessionGuard.NativeQuit();
        try { SR2MP.Shared.Managers.CoopSaveManager.SavePlayer(); }
        catch (Exception ex) { SrLogger.LogError("Guest checkpoint failed while leaving; retaining the previous snapshot: " + ex); }
    }
    public static void Postfix()
    {
        if (Main.Server.IsRunning)
            Main.Server.Close();
        if (Main.Client.IsConnected)
            Main.Client.Disconnect();
    }
}
