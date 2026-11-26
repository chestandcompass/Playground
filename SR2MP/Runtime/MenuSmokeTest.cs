using Il2CppTMPro;
using UnityEngine.SceneManagement;

namespace SR2MP.Runtime;

// Explicit developer-only switch; never runs for normal players.
internal static class MenuSmokeTest
{
    internal static readonly bool Enabled = Environment.GetCommandLineArgs().Contains("--playground-menu-test");
    private static float next;
    private static int phase;
    private static float nextAction;
    internal static void Update()
    {
        if (Enabled && UnityEngine.Time.realtimeSinceStartup >= nextAction)
        {
            nextAction = UnityEngine.Time.realtimeSinceStartup + 1;
            var commandPath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Playground", "menu-test-command.txt");
            if (File.Exists(commandPath))
            {
                var command = File.ReadAllText(commandPath).Trim();
                File.Delete(commandPath);
                try { NativeMenu.TestAction(command); }
                catch (Exception ex) { SrLogger.LogError($"Menu integration test '{command}': {ex}"); }
                next = UnityEngine.Time.realtimeSinceStartup + 2;
            }
        }
        if (!Enabled || UnityEngine.Time.realtimeSinceStartup < next) return;
        next = UnityEngine.Time.realtimeSinceStartup + 10;
        var lines = new List<string>();
        for (var i = 0; i < SceneManager.sceneCount; i++) lines.Add("SCENE " + SceneManager.GetSceneAt(i).name);
        foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>())
            if (label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                lines.Add(label.gameObject.name + ": " + label.text);
        foreach (var sizing in Resources.FindObjectsOfTypeAll<PlaygroundButtonSizing>())
            if (sizing.gameObject.activeInHierarchy)
                foreach (var label in sizing.GetComponentsInChildren<TMP_Text>())
                    lines.Add($"BUTTON LAYOUT {label.text}: button={sizing.transform.Cast<RectTransform>().rect.size}; label={label.rectTransform.rect.size}; parent={sizing.transform.parent.name}; truncated={label.isTextTruncated}; overflow={label.isTextOverflowing}");
        var dir = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Playground", "MenuTest");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, $"screen-{phase}.txt"), lines);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"screen-{phase}.png"));
        phase++;
        if (phase > 100) next = float.MaxValue;
    }
}
