using SR2MP.Shared.Utils;
using Il2CppInterop.Runtime.Attributes;

namespace SR2MP.Runtime;

internal static class PlayerHair
{
    private static readonly Dictionary<(IntPtr texture, byte choice), Texture2D> textures = new();
    internal static void Reset()
    {
        foreach (var texture in textures.Values) Object.Destroy(texture);
        textures.Clear();
    }
    internal static void Apply(GameObject root, byte choice)
        => root.GetOrAddComponent<PlayerHairAppearance>().Apply(choice);

    internal static bool IsHair(Material? material) => material != null && material.name.StartsWith("beatrix_SR2_head_hair", StringComparison.Ordinal);

    internal static Texture2D Texture(Texture source, byte choice)
    {
        if (textures.TryGetValue((source.Pointer, choice), out var cached) && cached != null) return cached;
        // The atlas also contains face pixels, but this copy is assigned only to
        // the dedicated hair material. Bound resolution and cache per color.
        var scale = Math.Min(1f, 512f / Math.Max(source.width, source.height));
        var width = Math.Max(1, (int)(source.width * scale));
        var height = Math.Max(1, (int)(source.height * scale));
        var previous = RenderTexture.active;
        var buffer = RenderTexture.GetTemporary(width, height, 0);
        Texture2D? result = null;
        try
        {
            Graphics.Blit(source, buffer);
            RenderTexture.active = buffer;
            result = new Texture2D(width, height, TextureFormat.RGBA32, true);
            result.name = "Playground hair " + HairColors.Names[choice];
            result.hideFlags = HideFlags.HideAndDontSave;
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            var pixels = result.GetPixels();
            ColorUtility.TryParseHtmlString("#" + HairColors.Hex[choice], out var tint);
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                var shade = Mathf.Clamp01(Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
                pixels[i] = new Color(tint.r * shade, tint.g * shade, tint.b * shade, pixel.a);
            }
            result.SetPixels(pixels); result.Apply(true, !MenuSmokeTest.Enabled);
            textures[(source.Pointer, choice)] = result;
            return result;
        }
        catch { if (result != null) Object.Destroy(result); throw; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); }
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || PlayerPrefab == null) throw new InvalidOperationException("Wait for the main menu.");
        void Check(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("HAIR SMOKE FAILED: " + message);
            SrLogger.LogMessage("HAIR SMOKE PASS: " + message);
        }
        var first = Object.Instantiate(PlayerPrefab);
        var second = Object.Instantiate(PlayerPrefab);
        first.GetComponent<SR2MP.Components.Player.NetworkPlayer>().IsLocal = second.GetComponent<SR2MP.Components.Player.NetworkPlayer>().IsLocal = true;
        Material Hair(GameObject root) => root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).First(IsHair);
        var original = Hair(first);
        var originalTexture = original.GetTexture("_MainTex");
        var skin = first.GetComponentsInChildren<Renderer>(true).First(r => r.name == "SR2_bea_meshes_body_arm_low");
        var skinMaterial = skin.sharedMaterial;
        try
        {
            Apply(first, 0);
            Check(Hair(first).Pointer == original.Pointer, "original choice preserves the exact native material and texture");
            Apply(second, 5);
            var other = Hair(second).Pointer;
            for (byte choice = 1; choice < HairColors.Names.Length; choice++)
            {
                Apply(first, choice);
                var texture = Hair(first).GetTexture("_MainTex").Cast<Texture2D>();
                var pixel = texture.GetPixels().First(p => p.maxColorComponent > .03f && p.a > .5f);
                ColorUtility.TryParseHtmlString("#" + HairColors.Hex[choice], out var tint);
                var actual = new Vector3(pixel.r, pixel.g, pixel.b).normalized;
                var expected = new Vector3(tint.r, tint.g, tint.b).normalized;
                Check(texture.Pointer != originalTexture.Pointer && Vector3.Distance(actual, expected) < .08f,
                    "recolored texture has the selected hue: " + HairColors.Names[choice]);
            }
            Check(Hair(second).Pointer == other && skin.sharedMaterial.Pointer == skinMaterial.Pointer
                && original.GetTexture("_MainTex").Pointer == originalTexture.Pointer,
                "changing one player's hair preserves the other player, skin and shared native assets");
            Apply(first, 0);
            Check(Hair(first).Pointer == original.Pointer, "returning to original restores native hair exactly");
            Check(textures.Count <= HairColors.Names.Length - 1, "generated textures are cached per color within bounded resolution");
        }
        finally { Object.Destroy(first); Object.Destroy(second); }
    }
}

[MelonLoader.RegisterTypeInIl2Cpp]
internal sealed class PlayerHairAppearance : MonoBehaviour
{
    private readonly List<(Renderer renderer, int slot, Material original)> slots = new();
    private readonly List<Material> copies = new();
    private int current = -1;

    [HideFromIl2Cpp]
    internal void Apply(byte choice)
    {
        if (!HairColors.IsValid(choice)) throw new InvalidDataException("Unknown hair color.");
        if (current == choice) return;
        if (slots.Count == 0)
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    if (PlayerHair.IsHair(materials[i])) slots.Add((renderer, i, materials[i]));
            }
        foreach (var (renderer, slot, original) in slots)
        {
            var materials = renderer.sharedMaterials;
            materials[slot] = original;
            if (choice != 0)
            {
                var copy = new Material(original);
                copies.Add(copy);
                copy.SetTexture("_MainTex", PlayerHair.Texture(original.GetTexture("_MainTex"), choice));
                materials[slot] = copy;
            }
            renderer.sharedMaterials = materials;
        }
        // Old copies are released only after every renderer has switched away.
        foreach (var copy in copies.Take(Math.Max(0, copies.Count - (choice == 0 ? 0 : slots.Count))).ToArray())
        { copies.Remove(copy); Object.Destroy(copy); }
        current = choice;
    }

    public void OnDestroy()
    {
        foreach (var material in copies) Object.Destroy(material);
        copies.Clear();
    }
}
