using System.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMonomiPark.SlimeRancher.UI.ButtonBehavior;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Model;
using SR2MP.Shared.Managers;
using Il2CppTMPro;
using UnityEngine.UI;

namespace SR2MP.Runtime;

// Keep the game's slot list, summary, deletion prompt and navigation. Only the
// storage role and the actions offered after selecting a slot belong to Playground.
internal static class WorldSlotMenu
{
    private static SaveGamesRootUI? screen;
    private static readonly List<GameObject> tabs = new();
    private static readonly List<PlaygroundMenuItem> models = new();
    private static string? pressedTab;
    internal static string Role { get; private set; } = "singleplayer";
    internal static WorldSlot? Source { get; private set; }
    internal static bool IsOpen => screen != null && screen.gameObject.activeInHierarchy;

    internal static void Open(string role = "singleplayer", WorldSlot? source = null)
    {
        Role = role;
        Source = source;
        CoopSaveManager.SelectRole(role,false);
        NativeMenu.OpenSaveFiles();
    }
    internal static void Prepare(SaveGamesRootUI ui)
    {
        NativeMenu.ClearWorldIntent();
        screen = ui;
        if(CoopSaveManager.Role != Role) CoopSaveManager.SelectRole(Role,false);
    }
    internal static void Released(SaveGamesRootUI ui)
    {
        if (screen == null || screen.Pointer != ui.Pointer) return;
        foreach(var tab in tabs) if(tab != null) UnityEngine.Object.Destroy(tab);
        tabs.Clear(); models.Clear(); screen = null;
        // Do not leave vanilla Continue/New Game pointing at multiplayer storage.
        if(!GameHelpers.inGame && !Main.Server.IsRunning && !Main.Client.IsConnected)
            CoopSaveManager.SelectRole("singleplayer",false);
        Role = "singleplayer";
        Source = null;
    }
    internal static void Attached(SaveGamesRootUI ui)
    {
        MelonLoader.MelonCoroutines.Start(Build());
        IEnumerator Build()
        {
            yield return null;
            if(!IsOpen || screen!.Pointer != ui.Pointer) yield break;
            try
            {
                var template = Resources.FindObjectsOfTypeAll<MainMenuButton>().First(b=>!b.gameObject.name.StartsWith("Playground tab ",StringComparison.Ordinal));
                Add("Singleplayer", "singleplayer", .07f, .29f);
                Add("Multiplayer", "host", .71f, .93f);
                RefreshTabs();
                void Add(string caption,string role,float left,float right)
                {
                    var clone = UnityEngine.Object.Instantiate(template.gameObject,ui.transform);
                    clone.name = "Playground tab " + role;
                    tabs.Add(clone);
                    var button = clone.GetComponent<MainMenuButton>();
                    var model = new PlaygroundMenuItem(ui._occupiedSlotBehavior,caption,()=>Switch(role));
                    models.Add(model);
                    button.BindItem(0,model);
                    // The native list adapter normally subscribes to this event.
                    // Tabs sit outside that list and need their own subscription.
                    button._onActionButton.AddListener(new Action(model.InvokeBehavior));
                    var sizing = button.GetComponent<PlaygroundButtonSizing>();
                    if(sizing != null)
                    {
                        sizing.Configure(button._label.GetComponent<TMP_Text>(),button._label.StringReference,false);
                        sizing.enabled = false;
                    }
                    button._icon.gameObject.SetActive(false);
                    var marker=new GameObject("Playground active tab");
                    marker.transform.SetParent(clone.transform,false);
                    var markerImage=marker.AddComponent<Image>(); markerImage.color=new Color(.98f,.85f,.65f); markerImage.raycastTarget=false;
                    var markerRect=marker.transform.Cast<RectTransform>();
                    markerRect.anchorMin=new Vector2(.2f,.02f); markerRect.anchorMax=new Vector2(.8f,.07f);
                    markerRect.offsetMin=markerRect.offsetMax=Vector2.zero;
                    var rect = clone.transform.Cast<RectTransform>();
                    rect.pivot = new Vector2(.5f,.5f);
                    rect.anchorMin = new Vector2(left,.88f); rect.anchorMax = new Vector2(right,.94f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    var text = button._label.GetComponent<TMP_Text>();
                    text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.offsetMin = new Vector2(18,4); text.rectTransform.offsetMax = new Vector2(-18,-4);
                    text.enableAutoSizing = true; text.fontSizeMin = 18; text.fontSizeMax = 38;
                    text.alignment = TextAlignmentOptions.Center;
                    clone.SetActive(true);
                }
            }
            catch(Exception ex) { SrLogger.LogError("Save Files tabs: " + ex); }
        }
    }
    internal static void Switch(string role)
    {
        if(!IsOpen || role == Role) return;
        Role = role;
        CoopSaveManager.SelectRole(role,false);
        screen!.DeInit();
        screen.Init();
        RefreshTabs();
    }
    internal static void Update()
    {
        if(!IsOpen || !Application.isFocused) { pressedTab=null; return; }
        var mouse=UnityEngine.InputSystem.Mouse.current;
        if(mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame))
        {
            string? hovered=null;
            foreach(var tab in tabs)
            {
                var canvas=tab.GetComponentInParent<Canvas>();
                var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                if(RectTransformUtility.RectangleContainsScreenPoint(tab.transform.Cast<RectTransform>(),mouse.position.ReadValue(),camera))
                {
                    hovered=tab.name=="Playground tab host"?"host":"singleplayer";
                    break;
                }
            }
            if(mouse.leftButton.wasPressedThisFrame) pressedTab=hovered;
            if(mouse.leftButton.wasReleasedThisFrame)
            {
                if(hovered!=null && hovered==pressedTab) { var role=hovered; NativeMenu.Navigate(()=>Switch(role)); }
                pressedTab=null;
            }
        }
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        var pad=UnityEngine.InputSystem.Gamepad.current;
        if(keyboard?.leftArrowKey.wasPressedThisFrame==true || pad?.leftShoulder.wasPressedThisFrame==true)
            NativeMenu.Navigate(()=>Switch("singleplayer"));
        else if(keyboard?.rightArrowKey.wasPressedThisFrame==true || pad?.rightShoulder.wasPressedThisFrame==true)
            NativeMenu.Navigate(()=>Switch("host"));
    }
    private static void RefreshTabs()
    {
        foreach(var tab in tabs)
        {
            tab.transform.Find("Playground active tab").gameObject.SetActive(tab.name=="Playground tab "+Role);
        }
    }
    internal static void TestSelect(int index) => screen!.GetSelectedModel(index).InvokeBehavior();
    internal static void TestTab(string role) => tabs.First(t=>t.name=="Playground tab "+role).GetComponent<MainMenuButton>()._onActionButton.Invoke();
    internal static bool Select(LoadGameBehaviorModel model)
    {
        if(!IsOpen) return true;
        var slot = WorldSlots.Read(Role).FirstOrDefault(s=>s.Save?.SaveName == model.GameDataSummary.SaveName);
        if(slot == null) return false;
        var source = Source;
        Leave(()=>
        {
            if(source != null && source.Role != slot.Role) NativeMenu.ConfirmWorldCopy(source,slot);
            else NativeMenu.ManageSelectedWorld(slot,model);
        });
        return false;
    }
    internal static void Leave(Action next)
    {
        if(screen == null) return;
        // Closing the native display rebuilds the landing menu. Bind our actions
        // on the following frame, after that rebuild has finished.
        var generation=NativeMenu.LandingGeneration;
        NativeMenu.AfterSaveFiles(generation,next);
        screen.Close();
    }
    internal static void ReplaceEmpty(SaveGamesRootUI ui,Il2CppReferenceArray<ButtonBehaviorModel> buttons)
    {
        for(var i=0;i<buttons.Length;i++)
        {
            if(buttons[i].TryCast<LoadGameBehaviorModel>() != null) continue;
            var index = i;
            var native = buttons[i];
            var role = Role;
            var source = Source;
            var model = new PlaygroundMenuItem(ui._emptySlotBehavior,"Empty",()=>
            {
                if(source != null && source.Role != role)
                {
                    var slot = WorldSlots.Read(role)[index];
                    Leave(()=>NativeMenu.ConfirmWorldCopy(source,slot));
                }
                else Leave(()=>NativeMenu.CreateManagedWorld(role,native));
            });
            models.Add(model); buttons[i]=model;
        }
    }
}

[HarmonyPatch(typeof(SaveGamesRootUI),nameof(SaveGamesRootUI.OnEnable))]
internal static class WorldSlotsEnabled
{
    internal static void Prefix(SaveGamesRootUI __instance)=>WorldSlotMenu.Prepare(__instance);
    internal static void Postfix(SaveGamesRootUI __instance)=>WorldSlotMenu.Attached(__instance);
}
[HarmonyPatch(typeof(SaveGamesRootUI),nameof(SaveGamesRootUI.OnDisable))]
internal static class WorldSlotsDisabled
{ internal static void Postfix(SaveGamesRootUI __instance)=>WorldSlotMenu.Released(__instance); }
[HarmonyPatch(typeof(LoadGameBehaviorModel),nameof(LoadGameBehaviorModel.InvokeBehavior))]
internal static class WorldSlotSelected
{ internal static bool Prefix(LoadGameBehaviorModel __instance)=>WorldSlotMenu.Select(__instance); }
[HarmonyPatch(typeof(LoadGameBehaviorModel),"get_Label")]
internal static class WorldSlotLabel
{
    internal static void Postfix(LoadGameBehaviorModel __instance,ref UnityEngine.Localization.LocalizedString __result)
    {
        if(!WorldSlotMenu.IsOpen || __instance.GameDataSummary == null) return;
        var label=WorldSlots.DisplayLabel(WorldSlotMenu.Role,__instance.GameDataSummary);
        if(label.Length>0) __result=NativeMenu.Text(__result,label);
    }
}
[HarmonyPatch(typeof(SaveGamesRootUI),nameof(SaveGamesRootUI.CreateBehaviorModels))]
internal static class WorldSlotDestinations
{ internal static void Postfix(SaveGamesRootUI __instance,Il2CppReferenceArray<ButtonBehaviorModel> __result)=>WorldSlotMenu.ReplaceEmpty(__instance,__result); }
