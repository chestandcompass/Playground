using HarmonyLib;

namespace SR2MP.Runtime;

[HarmonyPatch(typeof(GameContext), nameof(GameContext.Start))]
internal static class GameContextReady
{
    internal static void Postfix(GameContext __instance)
        => MelonLoader.Melon<Main>.Instance.AfterGameContext(__instance);
}
