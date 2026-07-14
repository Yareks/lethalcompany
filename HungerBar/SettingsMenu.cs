using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HungerBar;

[HarmonyPatch(typeof(MenuManager), "Start")]
internal static class MainMenuSettingsPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (GameObject.Find("HungerFoodSettingsCanvas") == null)
            SettingsMenu.Create();
    }
}

internal static class SettingsMenu
{
    private static Font _font = null!;

    internal static void Create()
    {
        _font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        GameObject root = new("HungerFoodSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = ImageObject("SettingsPanel", root.transform, new Color(0.02f, 0.02f, 0.02f, 0.96f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 650f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.SetActive(false);

        Button open = MakeButton(root.transform, "HUNGER / FOOD SETTINGS", new Vector2(250f, 48f));
        RectTransform openRect = open.GetComponent<RectTransform>();
        openRect.anchorMin = openRect.anchorMax = new Vector2(0f, 0f);
        openRect.pivot = new Vector2(0f, 0f);
        openRect.anchoredPosition = new Vector2(65f, 65f);
        open.onClick.AddListener(() => panel.SetActive(true));

        Text title = MakeText(panel.transform, "HUNGER / FOOD SETTINGS", 27, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 270f), new Vector2(650f, 45f));

        AddSetting(panel.transform, "Hunger bar right offset", 205f, Plugin.RightOffsetConfig, 2f, "F0");
        AddSetting(panel.transform, "Hunger bar height", 135f, Plugin.BarHeightConfig, 10f, "F0");
        AddSetting(panel.transform, "Mushroom held height", 65f, Plugin.HeldOffsetYConfig, 0.02f, "F2");
        AddSetting(panel.transform, "Mushroom held rotation", -5f, Plugin.HeldRotationZConfig, 5f, "F0");
        AddSetting(panel.transform, "Mushroom floor height", -75f, Plugin.FloorOffsetYConfig, 0.02f, "F2");
        AddSetting(panel.transform, "Mushroom floor scale", -145f, Plugin.FloorScaleConfig, 0.1f, "F1");

        Text hint = MakeText(panel.transform, "Changes apply live. Floor height: use minus to move the mushroom lower.", 16, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, new Vector2(0f, -220f), new Vector2(650f, 40f));

        Button close = MakeButton(panel.transform, "CLOSE", new Vector2(180f, 44f));
        SetRect(close.GetComponent<RectTransform>(), new Vector2(0f, -270f), new Vector2(180f, 44f));
        close.onClick.AddListener(() => panel.SetActive(false));
        Plugin.Log.LogInfo("Main-menu Hunger/Food settings button created");
    }

    private static void AddSetting(Transform parent, string name, float y, BepInEx.Configuration.ConfigEntry<float> entry, float step, string format)
    {
        Text label = MakeText(parent, name, 18, TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, new Vector2(-155f, y), new Vector2(330f, 44f));
        Text value = MakeText(parent, entry.Value.ToString(format), 18, TextAnchor.MiddleCenter);
        SetRect(value.rectTransform, new Vector2(130f, y), new Vector2(100f, 44f));
        Button minus = MakeButton(parent, "−", new Vector2(55f, 42f));
        SetRect(minus.GetComponent<RectTransform>(), new Vector2(215f, y), new Vector2(55f, 42f));
        Button plus = MakeButton(parent, "+", new Vector2(55f, 42f));
        SetRect(plus.GetComponent<RectTransform>(), new Vector2(280f, y), new Vector2(55f, 42f));
        minus.onClick.AddListener(() => { entry.Value -= step; value.text = entry.Value.ToString(format); });
        plus.onClick.AddListener(() => { entry.Value += step; value.text = entry.Value.ToString(format); });
    }

    private static Button MakeButton(Transform parent, string text, Vector2 size)
    {
        GameObject go = ImageObject(text + "Button", parent, new Color(0.85f, 0.18f, 0.03f, 0.96f));
        go.AddComponent<Button>();
        SetRect(go.GetComponent<RectTransform>(), Vector2.zero, size);
        Text label = MakeText(go.transform, text, 17, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    private static GameObject ImageObject(string name, Transform parent, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go;
    }

    private static Text MakeText(Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject go = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>(); text.text = value; text.font = _font; text.fontSize = size;
        text.alignment = alignment; text.color = new Color(1f, 0.75f, 0.55f); return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
