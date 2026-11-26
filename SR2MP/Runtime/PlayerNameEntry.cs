using System.Collections;
using Il2CppTMPro;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SR2MP.Runtime;

internal static class PlayerNameEntry
{
    private static GameObject? editor, header;
    private static TMP_InputField? input;
    private static TMP_Text? nativeLabel;
    private static Action? confirm, cancel;
    private static int generation;
    private static string heading = "Your name", hint = "Enter your name";
    private static int maximumLength = PlayerNames.MaxCharacters;
    private static byte pendingHair;
    private static bool appearance;
    private static GameObject? appearancePreview;
    internal static bool IsOpen { get; private set; }
    private static string L(string text) => PlaygroundLocalization.Translate(LocalizationSettings.SelectedLocale?.Identifier.Code, text);

    internal static void Open(Action accepted, Action cancelled)
        => Open("Your name", "Enter your name", Main.HasPlayerName ? Main.RawUsername : "", PlayerNames.MaxCharacters, accepted, cancelled);

    internal static void OpenWorld(string initial, Action accepted, Action cancelled)
        => Open("World name", "Enter a world name", initial, WorldLabels.MaxCharacters, accepted, cancelled);

    private static void Open(string titleKey, string hintKey, string initial, int limit, Action accepted, Action cancelled)
    {
        Close();
        heading = titleKey; hint = hintKey; maximumLength = limit;
        appearance = titleKey == "Your name";
        pendingHair = Main.HairColor;
        if (appearance)
        {
            appearancePreview = GameObject.Find("BeatrixMainMenu");
            if (appearancePreview != null) PlayerHair.Apply(appearancePreview, pendingHair);
        }
        IsOpen = true;
        confirm = accepted; cancel = cancelled;
        var revision = generation;
        MelonLoader.MelonCoroutines.Start(Build());
        IEnumerator Build()
        {
            yield return null; // The native list must finish binding its three rows.
            if (!IsOpen || revision != generation) yield break;
            var key = "playground." + CoopFiles.Key(heading);
            var button = Resources.FindObjectsOfTypeAll<MainMenuButton>().FirstOrDefault(b => b.gameObject.activeInHierarchy
                && b._label.StringReference.TableEntryReference.Key == key);
            if (button == null) { Close(); NativeMenu.ShowError(); yield break; }
            nativeLabel = button._label.GetComponent<TMP_Text>() ?? button._label.GetComponentInChildren<TMP_Text>(true);
            if (nativeLabel == null) { Close(); NativeMenu.ShowError(); yield break; }
            editor = new GameObject("Playground player name input");
            editor.transform.SetParent(button.transform, false);
            var background = editor.AddComponent<Image>();
            background.color = Color.clear;
            var rect = editor.transform.Cast<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(88, 8); rect.offsetMax = new Vector2(-85, -8);
            input = editor.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.transition = Selectable.Transition.None;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = maximumLength;
            input.richText = false;
            var viewport = new GameObject("Text viewport");
            viewport.transform.SetParent(editor.transform, false);
            viewport.AddComponent<RectMask2D>();
            Stretch(viewport.transform.Cast<RectTransform>());
            input.textViewport = viewport.transform.Cast<RectTransform>();
            var text = Label("Name text", viewport.transform, nativeLabel);
            text.richText = false;
            input.textComponent = text;
            // Confirm on an explicit Enter or native Continue action. The game's
            // input module can submit the field while selecting another row.
            input.onSubmit.AddListener(new Action<string>(_ =>
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) Submit();
            }));
            var placeholder = Label("Name placeholder", viewport.transform, nativeLabel);
            placeholder.text = L(hint);
            placeholder.color = new Color(1, 1, 1, 0.65f);
            input.placeholder = placeholder;
            input.text = initial;
            var title = Label("Name heading", button.transform, nativeLabel);
            header = title.gameObject;
            title.text = L(heading);
            title.fontSize = 26;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(24, 8); titleRect.offsetMax = new Vector2(-24, 48);
            nativeLabel.enabled = false;
            input.ForceLabelUpdate();
            Focus();
        }
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_Text template)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var label = obj.AddComponent<TextMeshProUGUI>();
        PlayerNameFonts.Ensure(template.font);
        label.font = template.font; label.fontSharedMaterial = template.fontSharedMaterial;
        label.fontSize = 36; label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    internal static void Focus()
    {
        if (input == null) return;
        EventSystem.current?.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
    }

    internal static bool Save()
    {
        if (!Read(out var name)) return false;
        Main.SetPlayerName(name);
        if (appearance) Main.SetHairColor(pendingHair);
        Close();
        return true;
    }

    internal static bool Read(out string name)
    {
        name = input == null ? "" : PlayerNames.Normalize(input.text, maximumLength);
        if (name.Length == 0) { Focus(); return false; }
        return true;
    }

    internal static void Cancel()
    {
        var action = cancel;
        Close();
        action?.Invoke();
    }

    internal static void Update()
    {
        if (!IsOpen || input == null) return;
        // Measure the field's actual caption (or placeholder) with the existing
        // native button fitter, so this pill also grows to fit its content.
        if (nativeLabel != null) nativeLabel.text = input.text.Length == 0 ? L(hint) : PlayerNames.Normalize(input.text, maximumLength);
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { NativeMenu.Navigate(Cancel); return; }
        if (input.isFocused && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        { Submit(); return; }
        // Focus once on opening or when the name row is explicitly selected.
        // Reclaiming focus every frame steals pointer/controller selection from
        // the native Continue and Back rows before they can submit.
    }

    internal static void SubmitMenuRow(int index)
    {
        if (!IsOpen) return;
        if (appearance)
        {
            if (index == 1) { NativeMenu.Navigate(CycleHair); return; }
            if (index > 1) index--;
        }
        switch (index)
        {
            case 0: Focus(); break;
            case 1: Submit(); break;
            case 2: NativeMenu.Navigate(Cancel); break;
        }
    }

    internal static void CycleHair()
    {
        if (!IsOpen || !appearance) return;
        input?.DeactivateInputField();
        pendingHair = HairColors.Next(pendingHair);
        if (MenuSmokeTest.Enabled) SrLogger.LogMessage("HAIR MENU choice=" + pendingHair);
        NativeMenu.RefreshHairCaption(pendingHair);
        if (appearancePreview != null) PlayerHair.Apply(appearancePreview, pendingHair);
    }

    private static void Submit()
    {
        if (!IsOpen) return;
        var action = confirm;
        NativeMenu.Navigate(() => action?.Invoke());
    }

    internal static void Close()
    {
        IsOpen = false; generation++;
        if (appearancePreview != null) PlayerHair.Apply(appearancePreview, 0);
        appearancePreview = null;
        if (input != null) input.DeactivateInputField();
        if (nativeLabel != null) nativeLabel.enabled = true;
        if (editor != null) Object.Destroy(editor);
        if (header != null) Object.Destroy(header);
        editor = header = null; input = null; nativeLabel = null;
        confirm = cancel = null;
    }

    internal static void TestText(string text)
    {
        if (!MenuSmokeTest.Enabled || input == null) return;
        input.text = text;
        input.ForceLabelUpdate();
    }

    internal static void TestConfirm()
    {
        if (!MenuSmokeTest.Enabled || !IsOpen) return;
        var action = confirm;
        NativeMenu.Navigate(() => action?.Invoke());
    }
}

[HarmonyLib.HarmonyPatch(typeof(MainMenuLandingRootUI), nameof(MainMenuLandingRootUI.UnityEngine_EventSystems_ISubmitHandler_OnSubmit))]
internal static class NameEntrySubmitPatch
{
    internal static bool Prefix(MainMenuLandingRootUI __instance)
    {
        if (!PlayerNameEntry.IsOpen) return true;
        // Keep the native list's selected action usable while an editor exists.
        // Only the name row is handled as text input, not as a normal menu action.
        PlayerNameEntry.SubmitMenuRow(__instance._listView.Interface.FocusedIndex);
        return false;
    }
}
