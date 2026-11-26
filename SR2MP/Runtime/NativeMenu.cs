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
    [HideFromIl2Cpp]
    internal void SetCaption(string text)
    {
        var updated = NativeMenu.Text(caption, text);
        caption.TableEntryReference = updated.TableEntryReference;
        caption.RefreshString();
    }
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
    private static bool joinLoading;
    private static bool enteringWorld;
    private static bool pendingHost;
    private static bool navigationPending;
    private static int pageRevision;
    private static bool lanVisible, lanRefreshPending;
    private static int lanOffset;
    private static string lanSignature = "";
    private static string? smokeOriginalName;
    private static bool smokeOriginalConfirmed;
    private static readonly Dictionary<string, string> localizedSources = new();

    internal static LocalizedString Text(LocalizedString template, string text)
    {
        var table = LocalizationSettings.StringDatabase.GetTable(template.TableReference);
        if (table == null) throw new InvalidOperationException("The game's menu localization table is unavailable.");
        var key = "playground." + CoopFiles.Key(text);
        localizedSources[key] = text;
        // Register one stable entry in every locale, so existing native buttons
        // also update when the player changes language without restarting.
        table.AddEntry(key, PlaygroundLocalization.Translate(LocalizationSettings.SelectedLocale?.Identifier.Code, text));
        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            var localizedTable = LocalizationSettings.StringDatabase.GetTable(template.TableReference, locale);
            if (localizedTable != null)
                localizedTable.AddEntry(key, PlaygroundLocalization.Translate(locale.Identifier.Code, text));
        }
        return new LocalizedString(template.TableReference, key);
    }

    internal static string RefreshCaption(LocalizedString caption)
    {
        var key = caption.TableEntryReference.Key;
        if (key == null || !localizedSources.TryGetValue(key, out var source)) return "";
        var translated = PlaygroundLocalization.Translate(LocalizationSettings.SelectedLocale?.Identifier.Code, source);
        // Unity may unload runtime table entries when switching locales. Restore
        // the entry in the newly loaded table before refreshing the native label.
        var table = LocalizationSettings.StringDatabase.GetTable(caption.TableReference);
        if (table != null) { table.AddEntry(key, translated); caption.RefreshString(); }
        return translated;
    }

    internal static void AddEntry(MainMenuLandingRootUI menu)
    {
        root = menu;
        originals.Clear();
        keepAlive.Clear();
        customPage = false;
        lanVisible = false;
        homeVisible = false;
        pageRevision++;
        foreach (var model in menu._models)
        {
            originals.Add(model);
            if (MenuSmokeTest.Enabled) SrLogger.LogMessage($"Native menu item: {model.Definition.name} / {model.Label.TableEntryReference.Key}");
        }
        if (originals.Count == 0) return;
        var entry = Item("Play with a friend", ShowHome);
        menu._models.Insert(Math.Min(1, menu._models.Count), entry);
        SrLogger.LogMessage("Playground: installed native main-menu entry.");
        var notice = SessionGuard.TakeNotice();
        if (notice != null) Navigate(() => Page((notice, ShowHome), ("Back", ShowHome)), false);
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
        PlayerNameEntry.Close();
        homeVisible = false;
        customPage = true;
        lanVisible = false;
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
            ("Create a world", CreateHost),
            ("My worlds", ShowWorlds),
            ("Copy a singleplayer world", ShowImportWorlds),
            ("Join a friend", ShowJoin),
            (UpdateManager.Caption, () => { UpdateManager.Activate(); ShowHome(); }),
            ("Back", Back));
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
        ("That didn't work. Try again", ShowHome),
        ("Back", Back));

    private static ButtonBehaviorModel NewGameButton()
        => originals.FirstOrDefault(m => m.Definition.name.Contains("NewGame", StringComparison.OrdinalIgnoreCase)
            || m.Definition.Label.TableEntryReference.Key?.Contains("new_game", StringComparison.OrdinalIgnoreCase) == true)
            ?? Resources.FindObjectsOfTypeAll<ButtonBehaviorDefinition>()
                .First(d => d.name == "BeginNewGameDisplay").CreateButtonBehaviorModel();

    private static void CreateHost()
        => AskPlayerName(CreateHostWithName, ShowHome);

    private static void AskPlayerName(Action continueAction, Action backAction)
    {
        void Confirm()
        {
            if (PlayerNameEntry.Save()) continueAction();
        }
        Page(("Your name", PlayerNameEntry.Focus), (HairColors.Caption(Main.HairColor), PlayerNameEntry.CycleHair),
            ("Continue", Confirm), ("Back", PlayerNameEntry.Cancel));
        PlayerNameEntry.Open(Confirm, backAction);
    }

    internal static void RefreshHairCaption(int choice)
    {
        if (root != null && root._models.Count == 4)
            root._models[1].Cast<PlaygroundMenuItem>().SetCaption(HairColors.Caption(choice));
    }

    private static void CreateHostWithName()
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
            options.Add((WorldName(save), () => ShowWorldActions(save)));
        }
        if (options.Count == 0) options.Add(("Create your first world", CreateHost));
        options.Add(("Back", ShowHome));
        Page(options.ToArray());
    }

    private static void ShowWorldActions(Il2CppMonomiPark.SlimeRancher.Persist.Summary save) => Page(
        ("Play: " + WorldName(save), () => AskPlayerName(() =>
        {
            pendingHost = true;
            GameContext.Instance.AutoSaveDirector.BeginLoad(save.SaveIdentifier, null);
        }, () => ShowWorldActions(save))),
        ("Rename multiplayer world", () => RenameWorld(save)),
        ("Delete multiplayer world", () => Page(
            // Cancel gets initial keyboard/controller focus. Removing a world always
            // requires a separate submit on the button carrying its actual name.
            ("Cancel", () => ShowWorldActions(save)),
            ("Yes, delete: " + WorldName(save), () =>
            {
                CoopSaveManager.DeleteHostWorld(save.SaveIdentifier.GameName);
                ShowWorlds();
            }))),
        ("Back", ShowWorlds));

    private static string WorldName(Il2CppMonomiPark.SlimeRancher.Persist.Summary save)
        => CoopSaveManager.HostDisplayName(save.SaveIdentifier.GameName, save.DisplayName);

    private static void RenameWorld(Il2CppMonomiPark.SlimeRancher.Persist.Summary save)
    {
        var current = WorldName(save);
        void Confirm()
        {
            if (!PlayerNameEntry.Read(out var name)) return;
            CoopSaveManager.RenameHostWorld(save.SaveIdentifier.GameName, name);
            PlayerNameEntry.Close();
            ShowWorldActions(save);
        }
        Page(("World name", PlayerNameEntry.Focus), ("Continue", Confirm), ("Back", PlayerNameEntry.Cancel));
        PlayerNameEntry.OpenWorld(current, Confirm, () => ShowWorldActions(save));
    }

    private static void ShowJoin() => Page(
        ("Find games on LAN", ShowLanGames),
        ("Paste code and join", JoinCopiedCode),
        ("How do I get a code?", ShowJoinHelp),
        ("Back", ShowHome));

    private static void ShowLanGames()
    {
        lanOffset = 0;
        LanDiscovery.StartBrowsing();
        RenderLanGames();
    }

    private static string LanSignature() => (LanDiscovery.Searching ? "search" : "done") + (LanDiscovery.Failed ? "error" : "")
        + string.Join("|", LanDiscovery.Worlds.OrderBy(w => w.Id).Select(w => w.Id + ":" + w.Name + ":" + w.Address + ":" + w.Port));

    private static void RenderLanGames()
    {
        var options = new List<(string, Action)>();
        var worlds = LanDiscovery.Worlds.OrderBy(w => w.Name, StringComparer.CurrentCulture).ThenBy(w => w.Id).ToArray();
        if (lanOffset >= worlds.Length) lanOffset = 0;
        foreach (var world in worlds.Skip(lanOffset).Take(3))
            options.Add(("Join: " + world.Name, () =>
            {
                // Do not load a staging world for an entry that has already expired.
                var current = LanDiscovery.Worlds.FirstOrDefault(w => w.Id == world.Id && LanDiscovery.Now - w.LastSeen < 4);
                if (current == null) { RenderLanGames(); return; }
                StartJoin(JoinCode.Encode(current.Address, current.Port));
            }));
        if (worlds.Length == 0) options.Add((LanDiscovery.Failed ? "LAN search unavailable — try again"
            : LanDiscovery.Searching ? "Searching for LAN games..." : "No games found — same Wi-Fi and version?", ShowLanGames));
        if (worlds.Length > 3) options.Add(("Next games", () => { lanOffset = (lanOffset + 3) % worlds.Length; RenderLanGames(); }));
        options.Add(("Refresh LAN games", ShowLanGames));
        options.Add(("Back", ShowJoin));
        Page(options.ToArray());
        lanVisible = true;
        lanSignature = LanSignature();
    }

    internal static void UpdateLanBrowser()
    {
        if (!lanVisible || root == null || !root.gameObject.activeInHierarchy)
        {
            lanVisible = false;
            if (LanDiscovery.Browsing) LanDiscovery.StopBrowsing();
            return;
        }
        if (lanRefreshPending || navigationPending || lanSignature == LanSignature()) return;
        lanRefreshPending = true;
        var revision = pageRevision;
        Navigate(() =>
        {
            lanRefreshPending = false;
            if (!lanVisible || navigationPending || revision != pageRevision) return;
            // New or expired hosts update the list, not every heartbeat packet.
            var focus = root._listView.Interface.FocusedIndex;
            RenderLanGames();
            root._listView.Interface.FocusedIndex = Math.Min(focus, root._models.Count - 1);
        }, false);
    }

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
                ("Copy: " + selected.DisplayName, () =>
                {
                    var copied = CoopSaveManager.ImportSingleplayer(selected);
                    Page((copied ? "Copied! Open My worlds"
                        : "This world already has a multiplayer copy", ShowWorlds),
                        ("Back", ShowHome));
                }),
                ("Original stays unchanged — go back", ShowImportWorlds))));
        }
        if (options.Count == 0)
            options.Add(("No singleplayer saves found", ShowHome));
        options.Add(("Back", ShowHome));
        Page(options.ToArray());
    }

    private static void ShowJoinHelp() => Page(
        ("Host: pause → Copy invitation code", ShowJoinHelp),
        ("Copy the received code (Ctrl+C)", ShowJoinHelp),
        ("Paste code and join (same Wi-Fi/LAN)", JoinCopiedCode),
        ("Back", ShowJoin));

    private static void JoinCopiedCode()
    {
        var code = GUIUtility.systemCopyBuffer.Trim();
        if (!JoinCode.TryDecode(code, out _, out _, out _))
        {
            Page(("No valid code — copy it first (Ctrl+C)", ShowJoinHelp),
                ("Paste again and join", JoinCopiedCode),
                ("Back", ShowJoin));
            return;
        }
        StartJoin(code);
    }

    private static void StartJoin(string code)
        => AskPlayerName(() => StartJoinWithName(code), ShowJoin);

    private static void StartJoinWithName(string code)
    {
        if (joinLoading) return;
        lanVisible = false;
        LanDiscovery.StopBrowsing();
        CoopSaveManager.SelectRole("guest");
        pendingJoin = code;
        joinLoading = true;
        Page(("Joining...", () => { }));
        // Do not open NewGameOptionsUIRoot: guests do not configure the host's
        // world. Build the native default settings directly in guest-only storage.
        var staging = GameContext.Instance.AutoSaveDirector.GetSaveToContinue();
        try
        {
            if (staging != null && !staging.IsInvalid)
            {
                SrLogger.LogMessage("Playground: reusing guest staging save without world settings.");
                GameContext.Instance.AutoSaveDirector.BeginLoad(staging.SaveIdentifier, null);
            }
            else
            {
                SrLogger.LogMessage("Playground: preparing guest staging save automatically.");
                var definitions = GameContext.Instance.OptionsDirector._optionsItemDefinitionsProvider
                    .GetAssetForCurrentPlatform().GetRuntime().GameBasedDefinitions;
                var settings = new Il2CppMonomiPark.SlimeRancher.DataModel.GameSettingsModel(
                    definitions.Cast<Il2CppSystem.Collections.Generic.IEnumerable<Il2CppMonomiPark.SlimeRancher.Options.OptionsItemDefinition>>());
                var icons = GameContext.Instance.GameSettingsDirector.gameIconCollection;
                if (icons.Count == 0) throw new InvalidOperationException("No native save icon is available.");
                settings.SetGameIconForNewGame(icons[0]);
                GameContext.Instance.AutoSaveDirector.StartNewGame(0, settings);
            }
        }
        catch
        {
            pendingJoin = null;
            joinLoading = false;
            CoopSaveManager.SelectRole("singleplayer");
            throw;
        }
    }

    internal static IEnumerator EnterWorld()
    {
        if (enteringWorld || (!pendingHost && pendingJoin == null)) yield break;
        enteringWorld = true;
        var hiddenIntroCanvases = new List<(Canvas canvas, bool enabled)>();
        GameObject? joiningDisplay = null;
        try
        {
        yield return new WaitForSceneGroupLoad();
        yield return new WaitFrames(5);
        var intro = Resources.FindObjectsOfTypeAll<Il2CppMonomiPark.SlimeRancher.UI.IntroSequence.IntroSequenceUIRoot>()
            .FirstOrDefault(p => p.gameObject.activeInHierarchy);
        var nextFrame = 0f;
        var introCanvases = pendingJoin != null && intro != null ? intro.GetComponentsInChildren<Canvas>(true) : null;
        if (introCanvases != null)
        {
            joiningDisplay = GuestJoinLoading.Show(intro!.GetComponentInChildren<Il2CppTMPro.TMP_Text>(true));
            foreach (var canvas in introCanvases)
            {
                hiddenIntroCanvases.Add((canvas, canvas.enabled));
                canvas.enabled = false;
            }
        }
        while (intro != null && intro.gameObject.activeInHierarchy)
        {
            if (pendingJoin != null && intro._isInputEnabled && Time.realtimeSinceStartup >= nextFrame)
            {
                // Use the native progression path; forcing EndSequence can abort
                // loading. Only guests bypass the local staging world's prologue.
                nextFrame = Time.realtimeSinceStartup + 0.2f;
                intro.NextOnPerformed(new Il2CppMonomiPark.SlimeRancher.Input.InputEventData { Value = 1 });
            }
            yield return null;
        }
        yield return new WaitForSceneGroupLoad();
        while (!GameHelpers.inGame) yield return null;
        if (pendingJoin != null) SrLogger.LogMessage("Playground: guest staging is ready; connecting to the selected host.");
        CoopSaveManager.MarkSessionStarted();
        if (pendingHost)
        {
            pendingHost = false;
            Main.Server.Start(1919, false);
        }
        else if (pendingJoin != null)
        {
            // Checkpoint the completed local intro before switching to host-bound
            // guest storage, so subsequent joins can reuse this staging save.
            GameContext.Instance.AutoSaveDirector.SaveGameAndFlush();
            var code = pendingJoin;
            pendingJoin = null;
            if (JoinCode.TryDecode(code, out var ip, out var port, out _)) Main.Client.Connect(ip.ToString(), port);
        }
        }
        finally
        {
            if (joiningDisplay != null) Object.Destroy(joiningDisplay);
            foreach (var (canvas, enabled) in hiddenIntroCanvases)
                if (canvas != null) canvas.enabled = enabled;
            enteringWorld = false;
            joinLoading = false;
        }
    }

    private static void Back()
    {
        pendingHost = false;
        pendingJoin = null;
        homeVisible = false;
        lanVisible = false;
        CoopSaveManager.SelectRole("singleplayer");
        customPage = false;
        root!.CreateModels();
        root.Init();
    }

    internal static bool HandleCancel()
    {
        if (PlayerNameEntry.IsOpen) { Navigate(PlayerNameEntry.Cancel); return false; }
        if (joinLoading) return false;
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
            case "lan": ShowLanGames(); break;
            case "join-smoke": StartJoin(JoinCode.Encode(IPAddress.Parse("127.0.0.2"), 1919)); break;
            case "name":
                smokeOriginalName ??= Main.RawUsername;
                if (smokeOriginalName == Main.RawUsername) smokeOriginalConfirmed = Main.HasPlayerName;
                AskPlayerName(ShowHome, ShowHome);
                break;
            case "name-type": PlayerNameEntry.TestText("Ola Łąka"); break;
            case "name-confirm": PlayerNameEntry.TestConfirm(); break;
            case "name-cancel": PlayerNameEntry.Cancel(); break;
            case "name-status": SrLogger.LogMessage($"SMOKE NAME: {Main.RawUsername}; confirmed={Main.HasPlayerName}"); break;
            case "name-restore":
                if (smokeOriginalName != null) Main.SetPlayerName(smokeOriginalName, smokeOriginalConfirmed);
                break;
            case "import": ShowImportWorlds(); break;
            case "join-paste": JoinCopiedCode(); break;
            case "join-help": ShowJoinHelp(); break;
            case "layout-small": Screen.SetResolution(1280, 720, false); break;
            case "layout-original": Screen.SetResolution(2560, 1440, true); break;
            case "locale-de":
            case "locale-en":
                foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
                    if (locale.Identifier.Code.StartsWith(command.Substring(7), StringComparison.OrdinalIgnoreCase))
                    { LocalizationSettings.SelectedLocale = locale; break; }
                break;
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
            case "rules-smoke": HostWorldRules.SmokeTest(); break;
            case "storage-smoke": StorageSynchronization.SmokeTest(); break;
            case "gadget-smoke": HostGadgetPlacement.SmokeTest(); break;
            case "cosmetics-smoke": PlayerCosmetics.SmokeTest(); break;
            case "hair-smoke": PlayerHair.SmokeTest(); break;
            case "guest-profile-smoke": GuestPlayerProfile.SmokeTest(); break;
            case "remote-player-smoke": MelonLoader.MelonCoroutines.Start(SR2MP.Handlers.Internal.ConnectionApproveHandler.SpawnSmoke()); break;
            case "disconnect-smoke": SessionGuard.TestLostConnection(); break;
            case "play-selected": root!._models[0].InvokeBehavior(); break;
            case "back": Back(); break;
            case "home": ShowHome(); break;
            case "quit": Application.Quit(); break;
            default: throw new ArgumentException("Unknown menu integration test action.");
        }
    }

    internal static void AddPause(PauseMenuRoot pause)
    {
        if (!CoopSaveManager.Enabled || pause._pausedItems.Count == 0) return;
        var caption = Main.Server.IsRunning ? "Copy invitation code"
            : "Save and disconnect";
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
