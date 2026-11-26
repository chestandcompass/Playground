using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppMonomiPark.SlimeRancher.UI.ButtonBehavior;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu;
using Il2CppMonomiPark.SlimeRancher.UI.Framework.Data;
using Il2CppMonomiPark.SlimeRancher.UI.Pause;
using Il2CppMonomiPark.SlimeRancher.UI;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace SR2MP.Runtime;

[MelonLoader.RegisterTypeInIl2Cpp]
public sealed class PlaygroundMenuItem : ButtonBehaviorModel
{
    private LocalizedString caption = null!;
    private Action action = null!;
    public PlaygroundMenuItem(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    public PlaygroundMenuItem(ButtonBehaviorDefinition definition, string text, Action onClick)
        : base(ClassInjector.DerivedConstructorPointer<PlaygroundMenuItem>())
    {
        ClassInjector.DerivedConstructorBody(this);
        _Definition_k__BackingField = definition;
        caption = NativeMenu.Text(definition.Label, text);
        action = onClick;
    }
    public override LocalizedString Label => caption;
    public override void InvokeBehavior()
    {
        NativeMenu.Navigate(action);
    }
}

[MelonLoader.RegisterTypeInIl2Cpp]
public sealed class PlaygroundPauseItem : PauseItemModel
{
    private Action action = null!;
    public PlaygroundPauseItem(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    public PlaygroundPauseItem(LocalizedString template, string text, Action onClick)
        : base(ClassInjector.DerivedConstructorPointer<PlaygroundPauseItem>())
    {
        ClassInjector.DerivedConstructorBody(this);
        label = NativeMenu.Text(template, text);
        shouldPlayCueOnSubmit = true;
        action = onClick;
    }
    public override bool DisplayItem() => true;
    public override void InvokeBehavior()
    {
        try { action(); }
        catch (Exception ex) { SrLogger.LogError($"Playground pause action: {ex}"); }
    }
}

internal static class NativeMenu
{
    private static MainMenuLandingRootUI? root;
    private static readonly List<ButtonBehaviorModel> originals = new();
    private static readonly List<PlaygroundMenuItem> keepAlive = new();
    private static bool customPage;
    private static bool homeVisible;
    private static string? pendingJoin;
    private static bool pendingHost;
    private static bool navigationPending;
    private static int pageRevision;
    internal static bool Polish => LocalizationSettings.SelectedLocale?.Identifier.Code.StartsWith("pl") == true;
    private static string L(string pl, string en) => Polish ? pl : en;

    internal static LocalizedString Text(LocalizedString template, string text)
    {
        var table = LocalizationSettings.StringDatabase.GetTable(template.TableReference);
        if (table == null) throw new InvalidOperationException("The game's menu localization table is unavailable.");
        var key = "playground." + CoopFiles.Key(text);
        table.AddEntry(key, text);
        return new LocalizedString(template.TableReference, key);
    }

    internal static void AddEntry(MainMenuLandingRootUI menu)
    {
        root = menu;
        originals.Clear();
        keepAlive.Clear();
        customPage = false;
        homeVisible = false;
        pageRevision++;
        foreach (var model in menu._models)
        {
            originals.Add(model);
            if (MenuSmokeTest.Enabled) SrLogger.LogMessage($"Native menu item: {model.Definition.name} / {model.Label.TableEntryReference.Key}");
        }
        if (originals.Count == 0) return;
        var entry = Item(L("Graj ze znajomym", "Play with a friend"), ShowHome);
        menu._models.Insert(Math.Min(1, menu._models.Count), entry);
        SrLogger.LogMessage("Playground: installed native main-menu entry.");
    }

    private static PlaygroundMenuItem Item(string text, Action action)
    {
        var revision = pageRevision;
        var item = new PlaygroundMenuItem(originals[0].Definition, text, () =>
        {
            if (revision == pageRevision) action();
        });
        keepAlive.Add(item);
        return item;
    }

    private static void Page(params (string text, Action action)[] options)
    {
        if (root == null) return;
        homeVisible = false;
        customPage = true;
        pageRevision++;
        var models = new Il2CppSystem.Collections.Generic.List<ButtonBehaviorModel>();
        foreach (var (text, action) in options) models.Add(Item(text, action));
        var provider = new GenericListViewDataProvider<ButtonBehaviorModel>(
            models.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<ButtonBehaviorModel>>());
        root._models = models;
        root._listView.Interface.DataProvider = provider.Cast<IListViewDataProviderBase>();
        root._listView.Interface.FocusedIndex = 0;
        root._subListContainer.SetActive(false);
    }

    internal static void ShowHome()
    {
        Page(
            (L("Utwórz świat", "Create a world"), CreateHost),
            (L("Moje światy", "My worlds"), ShowWorlds),
            (L("Skopiuj świat singleplayer", "Copy a singleplayer world"), ShowImportWorlds),
            (L("Dołącz do znajomego", "Join a friend"), ShowJoin),
            (UpdateManager.Caption, () => { UpdateManager.Activate(); ShowHome(); }),
            (L("Wróć", "Back"), Back));
        homeVisible = true;
    }

    internal static void RefreshUpdateStatus()
    {
        // An asynchronous update check must not replace a page the child just opened,
        // or move keyboard/controller focus back to the first button.
        var revision = pageRevision;
        Navigate(() =>
        {
            if (!homeVisible || revision != pageRevision || root == null || !root.gameObject.activeInHierarchy) return;
            var focus = root._listView.Interface.FocusedIndex;
            ShowHome();
            root._listView.Interface.FocusedIndex = focus;
        }, false);
    }

    internal static void Navigate(Action action, bool userAction = true)
    {
        if (userAction && navigationPending) return;
        if (userAction) navigationPending = true;
        MelonLoader.MelonCoroutines.Start(AfterSubmit());
        IEnumerator AfterSubmit()
        {
            // Let the native list finish its submit/selection callbacks before rebinding.
            yield return null;
            try { action(); }
            catch (Exception ex) { SrLogger.LogError($"Playground menu: {ex}"); ShowError(); }
            finally { if (userAction) navigationPending = false; }
        }
    }

    internal static void ShowError() => Page(
        (L("Nie udało się. Spróbuj ponownie", "That didn't work. Try again"), ShowHome),
        (L("Wróć", "Back"), Back));

    private static ButtonBehaviorModel NewGameButton()
        => originals.FirstOrDefault(m => m.Definition.name.Contains("NewGame", StringComparison.OrdinalIgnoreCase)
            || m.Definition.Label.TableEntryReference.Key?.Contains("new_game", StringComparison.OrdinalIgnoreCase) == true)
            ?? Resources.FindObjectsOfTypeAll<ButtonBehaviorDefinition>()
                .First(d => d.name == "BeginNewGameDisplay").CreateButtonBehaviorModel();

    private static void CreateHost()
    {
        CoopSaveManager.SelectRole("host");
        pendingHost = true;
        NewGameButton().InvokeBehavior();
    }

    private static void ShowWorlds()
    {
        CoopSaveManager.SelectRole("host");
        var options = new List<(string, Action)>();
        foreach (var group in GameContext.Instance.AutoSaveDirector.AvailableGamesByGameName())
        {
            if (group.Value.Count == 0) continue;
            var save = group.Value[0];
            for (var i = 1; i < group.Value.Count; i++)
                if (group.Value[i].SaveNumber > save.SaveNumber) save = group.Value[i];
            if (save.IsInvalid) continue;
            options.Add((save.DisplayName, () =>
            {
                pendingHost = true;
                GameContext.Instance.AutoSaveDirector.BeginLoad(save.SaveIdentifier, null);
            }));
        }
        if (options.Count == 0) options.Add((L("Utwórz pierwszy świat", "Create your first world"), CreateHost));
        options.Add((L("Wróć", "Back"), ShowHome));
        Page(options.ToArray());
    }

    private static void ShowJoin() => Page(
        (L("Wklej kod i dołącz", "Paste code and join"), JoinCopiedCode),
        (L("Skąd wziąć kod?", "How do I get a code?"), ShowJoinHelp),
        (L("Wróć", "Back"), ShowHome));

    private static void ShowImportWorlds()
    {
        CoopSaveManager.SelectRole("singleplayer");
        var options = new List<(string, Action)>();
        foreach (var group in GameContext.Instance.AutoSaveDirector.AvailableGamesByGameName())
        {
            Il2CppMonomiPark.SlimeRancher.Persist.Summary? latest = null;
            foreach (var candidate in group.Value)
                if (!candidate.IsInvalid && (latest == null || candidate.SaveNumber > latest.SaveNumber)) latest = candidate;
            if (latest == null) continue;
            var selected = latest;
            options.Add((selected.DisplayName, () => Page(
                (L("Skopiuj: ", "Copy: ") + selected.DisplayName, () =>
                {
                    var copied = CoopSaveManager.ImportSingleplayer(selected);
                    Page((copied ? L("Gotowe! Otwórz Moje światy", "Copied! Open My worlds")
                        : L("Ten świat ma już kopię multiplayer", "This world already has a multiplayer copy"), ShowWorlds),
                        (L("Wróć", "Back"), ShowHome));
                }),
                (L("Oryginał pozostaje bez zmian — wróć", "Original stays unchanged — go back"), ShowImportWorlds))));
        }
        if (options.Count == 0)
            options.Add((L("Brak zapisów singleplayer", "No singleplayer saves found"), ShowHome));
        options.Add((L("Wróć", "Back"), ShowHome));
        Page(options.ToArray());
    }

    private static void ShowJoinHelp() => Page(
        (L("Host: pauza → Skopiuj kod zaproszenia", "Host: pause → Copy invitation code"), ShowJoinHelp),
        (L("Skopiuj otrzymany kod (Ctrl+C)", "Copy the received code (Ctrl+C)"), ShowJoinHelp),
        (L("Wklej kod i dołącz (ta sama sieć Wi-Fi/LAN)", "Paste code and join (same Wi-Fi/LAN)"), JoinCopiedCode),
        (L("Wróć", "Back"), ShowJoin));

    private static void JoinCopiedCode()
    {
        var code = GUIUtility.systemCopyBuffer.Trim();
        if (!JoinCode.TryDecode(code, out _, out _, out _))
        {
            Page((L("Brak poprawnego kodu — skopiuj go (Ctrl+C)", "No valid code — copy it first (Ctrl+C)"), ShowJoinHelp),
                (L("Wklej ponownie i dołącz", "Paste again and join"), JoinCopiedCode),
                (L("Wróć", "Back"), ShowJoin));
            return;
        }
        CoopSaveManager.SelectRole("guest");
        pendingJoin = code;
        // A staging world is created once; the child never selects or manages a save file.
        var staging = GameContext.Instance.AutoSaveDirector.GetSaveToContinue();
        if (staging != null)
            GameContext.Instance.AutoSaveDirector.BeginLoad(staging.SaveIdentifier, null);
        else
            NewGameButton().InvokeBehavior();
    }

    internal static IEnumerator EnterWorld()
    {
        if (!pendingHost && pendingJoin == null) yield break;
        yield return new WaitForSceneGroupLoad();
        yield return new WaitFrames(5);
        var intro = Resources.FindObjectsOfTypeAll<Il2CppMonomiPark.SlimeRancher.UI.IntroSequence.IntroSequenceUIRoot>()
            .FirstOrDefault(p => p.gameObject.activeInHierarchy);
        while (intro != null && intro.gameObject.activeInHierarchy) yield return null;
        yield return new WaitForSceneGroupLoad();
        while (!GameHelpers.inGame) yield return null;
        CoopSaveManager.MarkSessionStarted();
        if (pendingHost)
        {
            pendingHost = false;
            Main.Server.Start(1919, false);
        }
        else if (pendingJoin != null)
        {
            var code = pendingJoin;
            pendingJoin = null;
            if (JoinCode.TryDecode(code, out var ip, out var port, out _)) Main.Client.Connect(ip.ToString(), port);
        }
    }

    private static void Back()
    {
        pendingHost = false;
        pendingJoin = null;
        homeVisible = false;
        CoopSaveManager.SelectRole("singleplayer");
        customPage = false;
        root!.CreateModels();
        root.Init();
    }

    internal static bool HandleCancel()
    {
        if (!customPage) return true;
        Navigate(Back);
        return false;
    }

    internal static void TestAction(string command)
    {
        if (!MenuSmokeTest.Enabled) return;
        switch (command)
        {
            case "engage":
                var prompt = Resources.FindObjectsOfTypeAll<StandaloneEngagementPrompt>().First(p => p.gameObject.activeInHierarchy);
                prompt.OnInteract(new UnityEngine.InputSystem.InputAction.CallbackContext());
                break;
            case "open":
                root!._models[1].InvokeBehavior();
                break;
            case "worlds": ShowWorlds(); break;
            case "join": ShowJoin(); break;
            case "import": ShowImportWorlds(); break;
            case "join-paste": JoinCopiedCode(); break;
            case "join-help": ShowJoinHelp(); break;
            case "update-refresh": RefreshUpdateStatus(); break;
            case "load":
                ShowWorlds();
                root!._models[0].InvokeBehavior();
                break;
            case "create": CreateHost(); break;
            case "start":
                var options = Resources.FindObjectsOfTypeAll<NewGameOptionsUIRoot>().First(p => p.gameObject.activeInHierarchy);
                options.OnSubmit(new Il2CppMonomiPark.SlimeRancher.Input.InputEventData { Value = 1 });
                break;
            case "pause":
                Resources.FindObjectsOfTypeAll<Il2CppMonomiPark.SlimeRancher.Script.UI.Pause.PauseMenuDirector>()
                    .First(p => p.gameObject.activeInHierarchy).menuToggle.Perform();
                break;
            case "skip":
                Resources.FindObjectsOfTypeAll<Il2CppMonomiPark.SlimeRancher.UI.IntroSequence.IntroSequenceUIRoot>()
                    .First(p => p.gameObject.activeInHierarchy).NextOnPerformed(
                        new Il2CppMonomiPark.SlimeRancher.Input.InputEventData { Value = 1 });
                break;
            case "leave":
                Resources.FindObjectsOfTypeAll<Il2CppMonomiPark.SlimeRancher.Script.UI.Pause.PauseMenuDirector>()
                    .First(p => p.gameObject.activeInHierarchy).Quit();
                break;
            case "save": GameContext.Instance.AutoSaveDirector.SaveGameAndFlush(); break;
            case "status":
                SrLogger.LogMessage($"SMOKE: host={Main.Server.IsRunning}; guest={Main.Client.IsJoined}; role={CoopSaveManager.Role}; actors={ActorManager.ActorTypes.Count}; save={GameContext.Instance.AutoSaveDirector._storageProvider.Cast<FileStorageProvider>().savePath}");
                break;
            case "back": Back(); break;
            case "home": ShowHome(); break;
            case "quit": Application.Quit(); break;
            default: throw new ArgumentException("Unknown menu integration test action.");
        }
    }

    internal static void AddPause(PauseMenuRoot pause)
    {
        if (!CoopSaveManager.Enabled || pause._pausedItems.Count == 0) return;
        var caption = Main.Server.IsRunning ? L("Skopiuj kod zaproszenia", "Copy invitation code")
            : L("Zapisz i rozłącz", "Save and disconnect");
        var item = new PlaygroundPauseItem(pause._pausedItems[0].Label, caption, () =>
        {
            if (Main.Server.IsRunning)
            {
                var address = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (address == null) throw new InvalidOperationException("No local network address.");
                GUIUtility.systemCopyBuffer = JoinCode.Encode(address, (ushort)Main.Server.Port);
            }
            else
            {
                CoopSaveManager.SavePlayer();
                Main.Client.Disconnect();
            }
        });
        pause._pausedItems.Add(item);
        // The current pause button binds IMenuLabelItem, while the main-menu
        // button binds ButtonBehaviorModel. Using the legacy model as the binder
        // type creates unbound "label" placeholders for the whole pause menu.
        var items = new Il2CppSystem.Collections.Generic.List<Il2CppMonomiPark.SlimeRancher.UI.Pause.Model.IMenuLabelItem>();
        foreach (var model in pause._pausedItems)
            items.Add(model.Cast<Il2CppMonomiPark.SlimeRancher.UI.Pause.Model.IMenuLabelItem>());
        var provider = new GenericListViewDataProvider<Il2CppMonomiPark.SlimeRancher.UI.Pause.Model.IMenuLabelItem>(
            items.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<Il2CppMonomiPark.SlimeRancher.UI.Pause.Model.IMenuLabelItem>>());
        pause._pauseItemList.Interface.DataProvider = provider.Cast<IListViewDataProviderBase>();
    }
}

[HarmonyPatch(typeof(MainMenuLandingRootUI), nameof(MainMenuLandingRootUI.CreateModels))]
internal static class MainMenuEntryPatch
{
    internal static void Postfix(MainMenuLandingRootUI __instance) => NativeMenu.AddEntry(__instance);
}

[HarmonyPatch(typeof(MainMenuLandingRootUI), nameof(MainMenuLandingRootUI.UnityEngine_EventSystems_ICancelHandler_OnCancel))]
internal static class MainMenuCancelPatch
{
    internal static bool Prefix() => NativeMenu.HandleCancel();
}

[HarmonyPatch(typeof(PauseMenuRoot), nameof(PauseMenuRoot.Awake))]
internal static class PauseMenuEntryPatch
{
    internal static void Postfix(PauseMenuRoot __instance) => NativeMenu.AddPause(__instance);
}

[HarmonyPatch(typeof(SceneContext), nameof(SceneContext.Start))]
internal static class PendingSessionPatch
{
    internal static void Postfix()
    {
        GameHelpers.StartCoroutine(NativeMenu.EnterWorld());
    }
}
