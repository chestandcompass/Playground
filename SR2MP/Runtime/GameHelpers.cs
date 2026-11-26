using System.Collections;
using MelonLoader;
using Il2CppInterop.Runtime.InteropTypes;

namespace SR2MP.Runtime;

internal static class GameHelpers
{
    internal static bool inGame => SceneContext.Instance != null && SceneContext.Instance.PlayerState != null;
    internal static SceneContext sceneContext => SceneContext.Instance;
    internal static bool IsGadget(this IdentifiableType type) => type != null && type.prefab != null && type.prefab.GetComponent<Gadget>() != null;
    internal static object StartCoroutine(IEnumerator routine) => MelonCoroutines.Start(routine);
    internal static void StopCoroutine(object token) => MelonCoroutines.Stop(token);
    internal static T AddComponent<T>(this Component component) where T : Component => component.gameObject.AddComponent<T>();
    internal static T GetOrAddComponent<T>(this GameObject obj) where T : Component => obj.GetComponent<T>() ?? obj.AddComponent<T>();
    internal static T GetOrAddComponent<T>(this Component obj) where T : Component => obj.gameObject.GetOrAddComponent<T>();
}

public sealed class Randoms
{
    private System.Random random = new();
    public void Reseed(int seed) => random = new System.Random(seed);
    public int GetInRange(int min, int max) => random.Next(min, max);
}

internal static class EmbeddedResourceEUtil
{
    internal static byte[]? LoadResource(string suffix)
    {
        var assembly = typeof(Main).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
        if (name == null) return null;
        using var source = assembly.GetManifestResourceStream(name)!;
        using var result = new MemoryStream();
        source.CopyTo(result);
        return result.ToArray();
    }
    internal static Sprite LoadSprite(string suffix)
    {
        var bytes = LoadResource(suffix) ?? throw new FileNotFoundException(suffix);
        var texture = new Texture2D(2, 2);
        Il2CppImageConversionManager.LoadImage(texture, bytes);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
    }
}
