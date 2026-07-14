using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HungerBar;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ru.yareks.lethalcompany.hungerbar";
    public const string PluginName = "Hunger Bar";
    public const string PluginVersion = "1.3.0";

    internal static ManualLogSource Log = null!;
    internal static float DurationSeconds { get; private set; } = 1200f;
    internal static float RightOffset { get; private set; } = 24f;
    internal static float BarHeight { get; private set; } = 260f;

    public static float Current { get; private set; } = 1f;

    private void Awake()
    {
        Log = Logger;
        ConfigEntry<float> duration = Config.Bind("Hunger", "FullDurationMinutes", 20f,
            "How many real-time minutes it takes for a full bar to become empty.");
        ConfigEntry<float> rightOffset = Config.Bind("Interface", "RightOffset", 24f,
            "Distance in pixels from the right edge.");
        ConfigEntry<float> barHeight = Config.Bind("Interface", "BarHeight", 260f,
            "Bar height in pixels.");

        DurationSeconds = Mathf.Max(1f, duration.Value * 60f);
        RightOffset = Mathf.Max(0f, rightOffset.Value);
        BarHeight = Mathf.Max(100f, barHeight.Value);

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded successfully");
        Logger.LogInfo($"Settings: duration={duration.Value:F1} min, rightOffset={RightOffset:F0}px, barHeight={BarHeight:F0}px");

        Harmony harmony = new(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo("Harmony patches installed. Waiting for HUDManager.Update.");
    }

    public static void Refill(float normalizedAmount)
    {
        Current = Mathf.Clamp01(Current + normalizedAmount);
        HungerHud.SetValue(Current);
        Log?.LogInfo($"Hunger refilled; current value is {Current * 100f:F1}%");
    }

    internal static void Tick(float deltaTime)
    {
        if (Time.timeScale > 0f && Current > 0f)
            Current = Mathf.Clamp01(Current - deltaTime / DurationSeconds);

        HungerHud.SetValue(Current);
    }
}

[HarmonyPatch(typeof(HUDManager), "Update")]
internal static class HudManagerUpdatePatch
{
    private static float _nextLogTime;
    private static bool _firstUpdate = true;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_firstUpdate)
        {
            _firstUpdate = false;
            Plugin.Log.LogInfo("HUDManager.Update patch is running");
        }

        HungerHud.EnsureCreated();
        Plugin.Tick(Time.unscaledDeltaTime);

        if (Time.unscaledTime >= _nextLogTime)
        {
            _nextLogTime = Time.unscaledTime + 10f;
            Plugin.Log.LogInfo($"HUD status: created={HungerHud.IsCreated}, active={HungerHud.IsActive}, hunger={Plugin.Current * 100f:F1}%, screen={Screen.width}x{Screen.height}");
        }
    }
}

internal static class HungerHud
{
    private static GameObject? _root;
    private static RectTransform? _fill;
    private static Image? _fillImage;

    internal static bool IsCreated => _root != null;
    internal static bool IsActive => _root != null && _root.activeInHierarchy;

    internal static void EnsureCreated()
    {
        if (_root != null)
            return;

        try
        {
            _root = new GameObject("HungerBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            GameObject background = CreateImage("Background", _root.transform, new Color(0.01f, 0.01f, 0.01f, 0.9f));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(1f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(1f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(-Plugin.RightOffset, 0f);
            backgroundRect.sizeDelta = new Vector2(30f, Plugin.BarHeight + 6f);

            GameObject fill = CreateImage("Fill", background.transform, Color.green);
            _fill = fill.GetComponent<RectTransform>();
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.pivot = new Vector2(0.5f, 0f);
            _fill.offsetMin = new Vector2(3f, 3f);
            _fill.offsetMax = new Vector2(-3f, -3f);
            _fillImage = fill.GetComponent<Image>();

            SetValue(Plugin.Current);
            Plugin.Log.LogInfo($"Hunger UI Canvas created: sortingOrder={canvas.sortingOrder}, resolution={Screen.width}x{Screen.height}");
        }
        catch (System.Exception exception)
        {
            Plugin.Log.LogError($"Failed to create hunger UI: {exception}");
            if (_root != null)
                Object.Destroy(_root);
            _root = null;
        }
    }

    internal static void SetValue(float value)
    {
        if (_fill == null || _fillImage == null)
            return;

        float normalized = Mathf.Clamp01(value);
        _fill.anchorMax = new Vector2(1f, normalized);
        _fillImage.color = Color.Lerp(new Color(0.9f, 0.08f, 0.03f, 1f), new Color(0.1f, 0.9f, 0.12f, 1f), normalized);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return gameObject;
    }
}
