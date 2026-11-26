using Il2CppTMPro;
using Il2CppMonomiPark.SlimeRancher.Player.CharacterController;
using Il2CppMonomiPark.SlimeRancher.Player.PlayerItems;
using Il2CppMonomiPark.SlimeRancher.Script.UI.Pause;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SR2MP.Runtime;

internal static class SessionGuard
{
    private static bool expected, returning;
    private static string? notice;
    private static TMP_Text? label;
    private static GameObject? display;
    private static float nextUi;
    private static bool smokeGuest;
    internal static void Reset() { expected = returning = false; }
    internal static void BeginConnect() { expected = true; notice = null; returning = false; }
    internal static void Failure(string reason) => notice = reason;
    internal static void NativeQuit() { if (CoopSaveManager.Role == "guest") returning = true; }
    internal static string? TakeNotice() { var value = notice; notice = null; return value; }

    internal static void Update()
    {
        if (!GameHelpers.inGame)
        {
            if (display != null) Object.Destroy(display);
            display = null; label = null;
            return;
        }
        var role = MenuSmokeTest.Enabled && smokeGuest ? "guest" : CoopSaveManager.Role;
        var state = SessionPolicy.Evaluate(role, Main.Server.IsRunning, Main.Server.ClientManager.ClientCount,
            expected, Main.Client.IsConnected, Main.Client.IsJoined, Main.Client.HostSilenceSeconds);
        if (state == SessionState.Disconnected && role == "guest" && !returning)
        {
            notice ??= "Connection ended. Rejoin to continue.";
            // Freeze interaction before any asynchronous menu transition. Never
            // leave a disconnected guest playing in the local staging world.
            var player = SceneContext.Instance.player;
            var character = player?.GetComponent<SRCharacterController>();
            var items = player?.GetComponent<PlayerItemController>();
            if (character != null) character.enabled = false;
            if (items != null) items.enabled = false;
            if (Main.Client.IsConnected) Main.Client.Disconnect();
            var pause = Resources.FindObjectsOfTypeAll<PauseMenuDirector>().FirstOrDefault(p => p.gameObject.activeInHierarchy);
            if (pause != null)
            {
                returning = true;
                SrLogger.LogMessage("Playground: disconnected guest is returning to the main menu.");
                pause.Quit();
            }
        }
        if (Time.realtimeSinceStartup < nextUi) return;
        nextUi = Time.realtimeSinceStartup + .25f;
        if (label == null) CreateDisplay();
        if (label == null) return;
        var caption = state switch
        {
            SessionState.Singleplayer => "Singleplayer",
            SessionState.HostingAlone => "Multiplayer: waiting for players",
            SessionState.HostingTogether => "Multiplayer: players connected",
            SessionState.Joining => "Multiplayer: connecting...",
            SessionState.Connected => "Multiplayer: connected",
            _ => "Multiplayer: disconnected"
        };
        label.text = PlaygroundLocalization.Translate(LocalizationSettings.SelectedLocale?.Identifier.Code, caption);
        label.color = state == SessionState.Disconnected ? new Color(1, .45f, .45f)
            : state is SessionState.Connected or SessionState.HostingTogether ? new Color(.6f, 1, .8f) : Color.white;
    }

    internal static void TestLostConnection()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning || Main.Server.ClientManager.ClientCount != 0)
            throw new InvalidOperationException("Use an isolated host test world without peers.");
        smokeGuest = true; expected = true; returning = false;
        notice = "Connection ended. Rejoin to continue.";
        try
        {
            Update();
            // OnSessionEnded can reset `returning` synchronously inside Quit.
            // Closing the isolated host confirms that the native Quit hook ran.
            if (Main.Server.IsRunning) throw new InvalidOperationException("Native return to menu did not start.");
            SrLogger.LogMessage("SESSION SMOKE PASS: guest controls are disabled and the native return to menu has started");
        }
        finally { smokeGuest = false; }
    }

    private static void CreateDisplay()
    {
        var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "Runsell Type - HemispheresCaps2 (Latin)");
        if (font == null) return;
        display = new GameObject("Playground session status");
        var canvas = display.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = display.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var text = new GameObject("Connection status"); text.transform.SetParent(display.transform, false);
        label = text.AddComponent<TextMeshProUGUI>(); label.font = font; PlayerNameFonts.Ensure(font);
        label.fontSize = 24; label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 24;
        label.enableWordWrapping = true; label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false; label.outlineWidth = .18f; label.outlineColor = new Color(0, 0, 0, .65f);
        var rect = label.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(.48f, 1);
        rect.pivot = new Vector2(0, 1); rect.offsetMin = new Vector2(24, -76); rect.offsetMax = new Vector2(-24, -20);
    }
}
