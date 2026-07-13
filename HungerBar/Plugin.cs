using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace HungerBar;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "ru.yareks.lethalcompany.hungerbar";
    public const string PluginName = "Hunger Bar";
    public const string PluginVersion = "1.0.0";

    private ConfigEntry<float> _fullDurationMinutes = null!;
    private ConfigEntry<float> _rightOffset = null!;
    private ConfigEntry<float> _barHeight = null!;
    private ConfigEntry<bool> _showPercentage = null!;
    private float _hunger = 1f;
    private GUIStyle? _labelStyle;

    /// <summary>Current hunger from 0 (empty) to 1 (full).</summary>
    public static float Current { get; private set; } = 1f;

    private void Awake()
    {
        _fullDurationMinutes = Config.Bind(
            "Hunger", "FullDurationMinutes", 20f,
            "How many real-time minutes it takes for a full hunger bar to become empty.");
        _rightOffset = Config.Bind(
            "Interface", "RightOffset", 24f,
            "Distance in pixels between the bar and the right edge of the screen.");
        _barHeight = Config.Bind(
            "Interface", "BarHeight", 260f,
            "Maximum bar height in pixels.");
        _showPercentage = Config.Bind(
            "Interface", "ShowPercentage", true,
            "Show the hunger percentage next to the bar.");

        Current = _hunger;
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || _hunger <= 0f)
            return;

        float seconds = Mathf.Max(1f, _fullDurationMinutes.Value * 60f);
        _hunger = Mathf.Clamp01(_hunger - Time.unscaledDeltaTime / seconds);
        Current = _hunger;
    }

    /// <summary>Adds hunger. Useful for food-item mods.</summary>
    public static void Refill(float normalizedAmount)
    {
        Plugin? instance = FindObjectOfType<Plugin>();
        if (instance == null)
            return;

        instance._hunger = Mathf.Clamp01(instance._hunger + normalizedAmount);
        Current = instance._hunger;
    }

    private void OnGUI()
    {
        const float width = 24f;
        float height = Mathf.Clamp(_barHeight.Value, 100f, Screen.height - 80f);
        float x = Screen.width - Mathf.Max(0f, _rightOffset.Value) - width;
        float y = (Screen.height - height) * 0.5f;

        DrawRect(new Rect(x - 3f, y - 3f, width + 6f, height + 6f), new Color(0f, 0f, 0f, 0.78f));
        DrawRect(new Rect(x, y, width, height), new Color(0.10f, 0.10f, 0.10f, 0.88f));

        float filledHeight = height * _hunger;
        Color hungerColor = Color.Lerp(new Color(0.9f, 0.12f, 0.05f), new Color(0.18f, 0.85f, 0.16f), _hunger);
        DrawRect(new Rect(x, y + height - filledHeight, width, filledHeight), hungerColor);

        // Small divisions make the indicator readable over bright game scenes.
        for (int i = 1; i < 4; i++)
            DrawRect(new Rect(x, y + height * i / 4f, width, 1f), new Color(0f, 0f, 0f, 0.45f));

        if (!_showPercentage.Value)
            return;

        _labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(x - 67f, y + height * 0.5f - 12f, 60f, 24f), $"{Mathf.RoundToInt(_hunger * 100f)}%", _labelStyle);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
