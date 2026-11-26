using Il2CppTMPro;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SR2MP.Runtime;

// Loading text only, using the running game's font/material. No window, frame,
// buttons or separate input system; the native intro still controls loading.
internal static class GuestJoinLoading
{
    internal static GameObject Show(TMP_Text? template)
    {
        var root = new GameObject("Playground guest loading");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var textObject = new GameObject("Joining text");
        textObject.transform.SetParent(root.transform, false);
        var label = textObject.AddComponent<TextMeshProUGUI>();
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
        }
        label.text = PlaygroundLocalization.Translate(LocalizationSettings.SelectedLocale?.Identifier.Code, "Joining...");
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontSize = 38;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24;
        label.fontSizeMax = 38;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.3f);
        rect.anchorMax = new Vector2(0.9f, 0.7f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return root;
    }
}
