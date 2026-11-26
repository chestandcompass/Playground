using HarmonyLib;
using Il2CppTMPro;
using Il2CppMonomiPark.SlimeRancher.UI.MainMenu;
using Il2CppMonomiPark.SlimeRancher.UI.Pause;
using UnityEngine.UI;

namespace SR2MP.Runtime;

// Native list entries are pooled. Restore the original layout when an entry is
// rebound to a vanilla button, and measure again after localization updates.
[MelonLoader.RegisterTypeInIl2Cpp]
public sealed class PlaygroundButtonSizing : MonoBehaviour
{
    private TMP_Text? label;
    private UnityEngine.Localization.LocalizedString? caption;
    private string? previousLocale;
    private RectTransform? rect;
    private LayoutElement? layout;
    private bool captured;
    private bool fitting;
    private Vector2 originalSize;
    private Vector2 originalBounds;
    private Vector2 originalLabelSize;
    private Vector4 originalMargin;
    private Vector2 originalPivot;
    private float originalAnchorX;
    private HorizontalOrVerticalLayoutGroup? parentLayout;
    private static readonly Dictionary<int, (HorizontalOrVerticalLayoutGroup group, TextAnchor alignment, int users)> Groups = new();
    private float minWidth, preferredWidth, flexibleWidth, minHeight, preferredHeight, flexibleHeight;
    private float fontSize, fontSizeMin, fontSizeMax;
    private bool autoSize, wrap;
    private TextOverflowModes overflow;
    private float horizontalPadding, verticalPadding;
    private string? previousText;
    private int previousScreenWidth, previousScreenHeight;
    private float previousCanvasWidth;
    private float targetWidth, targetHeight, targetTextWidth;

    public PlaygroundButtonSizing(IntPtr pointer) : base(pointer) { }

    internal void Configure(TMP_Text text, UnityEngine.Localization.LocalizedString localizedCaption, bool enabled)
    {
        if (!enabled)
        {
            Restore();
            return;
        }
        if (!captured)
        {
            label = text;
            rect = transform.Cast<RectTransform>();
            originalSize = rect.sizeDelta;
            originalBounds = rect.rect.size;
            originalPivot = rect.pivot;
            originalAnchorX = rect.anchoredPosition.x;
            originalLabelSize = text.rectTransform.sizeDelta;
            originalMargin = text.margin;
            parentLayout = rect.parent == null ? null : rect.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (parentLayout != null)
            {
                var id = parentLayout.GetInstanceID();
                if (Groups.TryGetValue(id, out var state)) Groups[id] = (state.group, state.alignment, state.users + 1);
                else Groups[id] = (parentLayout, parentLayout.childAlignment, 1);
                // Native rows start at the left edge of a narrow list. Right align
                // growing entries so the extra width stays inside the screen.
                parentLayout.childAlignment = (TextAnchor)(((int)parentLayout.childAlignment / 3) * 3 + 2);
            }
            else
            {
                // Some native menu containers position their rows explicitly and
                // have no LayoutGroup. Keep each row's original right edge fixed.
                rect.pivot = new Vector2(1, originalPivot.y);
                rect.anchoredPosition = new Vector2(originalAnchorX + (1 - originalPivot.x) * originalBounds.x, rect.anchoredPosition.y);
            }
            layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
            minWidth = layout.minWidth; preferredWidth = layout.preferredWidth; flexibleWidth = layout.flexibleWidth;
            minHeight = layout.minHeight; preferredHeight = layout.preferredHeight; flexibleHeight = layout.flexibleHeight;
            fontSize = label.fontSize; fontSizeMin = label.fontSizeMin; fontSizeMax = label.fontSizeMax;
            autoSize = label.enableAutoSizing; wrap = label.enableWordWrapping; overflow = label.overflowMode;
            // Preserve the native arrow, selection icon and text insets.
            horizontalPadding = Mathf.Max(80, rect.rect.width - label.rectTransform.rect.width);
            verticalPadding = Mathf.Max(20, rect.rect.height - label.rectTransform.rect.height);
            captured = true;
        }
        fitting = true;
        caption = localizedCaption;
        previousLocale = null;
        previousText = null;
    }

    public void LateUpdate()
    {
        if (!fitting || label == null || rect == null || layout == null) return;
        var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.Identifier.Code;
        if (caption != null && (previousLocale != locale || label.text.StartsWith("!! UI.playground.", StringComparison.Ordinal)))
        {
            previousLocale = locale;
            var translated = NativeMenu.RefreshCaption(caption);
            if (translated.Length > 0) label.text = translated;
        }
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRect = canvas.rootCanvas.transform.Cast<RectTransform>();
        var canvasWidth = canvasRect.rect.width;
        if (previousText == label.text && previousScreenWidth == Screen.width
            && previousScreenHeight == Screen.height && Mathf.Approximately(previousCanvasWidth, canvasWidth)
            && Mathf.Approximately(rect.rect.width, targetWidth) && Mathf.Approximately(rect.rect.height, targetHeight)
            && Mathf.Approximately(label.rectTransform.rect.width, targetTextWidth)) return;
        previousText = label.text;
        previousScreenWidth = Screen.width; previousScreenHeight = Screen.height; previousCanvasWidth = canvasWidth;
        label.enableAutoSizing = false;
        label.fontSize = fontSize;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Overflow;
        // Leave space for the native keyboard/controller submit badge when focused.
        label.margin = new Vector4(originalMargin.x + 64, originalMargin.y, originalMargin.z, originalMargin.w);
        // Grow the actual button first. Keep it within the canvas, then wrap the
        // caption and grow vertically instead of showing an ellipsis.
        var natural = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity);
        var width = Mathf.Min(Mathf.Max(natural.x + horizontalPadding, originalBounds.x), canvasWidth * 0.85f);
        var textWidth = Mathf.Max(40, width - horizontalPadding);
        var wrapped = label.GetPreferredValues(label.text, textWidth, float.PositiveInfinity);
        var height = Mathf.Max(originalBounds.y, wrapped.y + verticalPadding);
        targetWidth = width; targetHeight = height; targetTextWidth = textWidth;
        layout.minWidth = layout.preferredWidth = width;
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = layout.flexibleHeight = 0;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(wrapped.y, height - verticalPadding));
        LayoutRebuilder.MarkLayoutForRebuild(rect);
        if (rect.parent != null) LayoutRebuilder.MarkLayoutForRebuild(rect.parent.Cast<RectTransform>());
    }

    private void Restore()
    {
        fitting = false;
        if (!captured) return;
        if (label != null)
        {
            label.fontSize = fontSize; label.fontSizeMin = fontSizeMin; label.fontSizeMax = fontSizeMax;
            label.enableAutoSizing = autoSize; label.enableWordWrapping = wrap; label.overflowMode = overflow;
            label.rectTransform.sizeDelta = originalLabelSize;
            label.margin = originalMargin;
        }
        if (layout != null)
        {
            layout.minWidth = minWidth; layout.preferredWidth = preferredWidth; layout.flexibleWidth = flexibleWidth;
            layout.minHeight = minHeight; layout.preferredHeight = preferredHeight; layout.flexibleHeight = flexibleHeight;
            // Keep an inert element on pooled entries; deferred Destroy could race
            // the native list rebinding this same object again in the same frame.
        }
        if (rect != null)
        {
            rect.pivot = originalPivot;
            rect.anchoredPosition = new Vector2(originalAnchorX, rect.anchoredPosition.y);
            rect.sizeDelta = originalSize;
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        if (parentLayout != null && Groups.TryGetValue(parentLayout.GetInstanceID(), out var state))
        {
            var id = parentLayout.GetInstanceID();
            if (state.users <= 1) { parentLayout.childAlignment = state.alignment; Groups.Remove(id); }
            else Groups[id] = (state.group, state.alignment, state.users - 1);
        }
        parentLayout = null;
        captured = false;
    }

    public void OnDestroy() => Restore();
}

internal static class NativeButtonSizing
{
    internal static void Bind(Component button, UnityEngine.Localization.Components.LocalizeStringEvent localizedLabel)
    {
        var custom = localizedLabel.StringReference.TableEntryReference.Key?.StartsWith("playground.", StringComparison.Ordinal) == true;
        var sizing = button.GetComponent<PlaygroundButtonSizing>();
        if (!custom && sizing == null) return;
        var text = localizedLabel.GetComponent<TMP_Text>() ?? localizedLabel.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        if (sizing == null) sizing = button.gameObject.AddComponent<PlaygroundButtonSizing>();
        sizing.Configure(text, localizedLabel.StringReference, custom);
    }
}

[HarmonyPatch(typeof(MainMenuButton), nameof(MainMenuButton.BindItem))]
internal static class MainMenuButtonSizingPatch
{
    internal static void Postfix(MainMenuButton __instance) => NativeButtonSizing.Bind(__instance, __instance._label);
}

[HarmonyPatch(typeof(PauseMenuButton), nameof(PauseMenuButton.BindItem))]
internal static class PauseMenuButtonSizingPatch
{
    internal static void Postfix(PauseMenuButton __instance) => NativeButtonSizing.Bind(__instance, __instance._label);
}
