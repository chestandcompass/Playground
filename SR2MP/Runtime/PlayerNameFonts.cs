using Il2CppTMPro;
using UnityEngine.TextCore.LowLevel;

namespace SR2MP.Runtime;

internal static class PlayerNameFonts
{
    private static TMP_FontAsset? fallback;
    private static bool attempted;

    internal static void Ensure(TMP_FontAsset font)
    {
        if (!attempted)
        {
            attempted = true;
            try
            {
                // The game's Latin menu font omits Polish letters. Use a dynamic
                // fallback from Windows; no font files are bundled or installed.
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
                if (File.Exists(path))
                    fallback = TMP_FontAsset.CreateFontAsset(path, 0, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            }
            catch (Exception ex) { SrLogger.LogWarning($"Player name font fallback: {ex.Message}"); }
        }
        if (fallback == null || font == null || font == fallback) return;
        font.fallbackFontAssetTable ??= new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
    }
}
