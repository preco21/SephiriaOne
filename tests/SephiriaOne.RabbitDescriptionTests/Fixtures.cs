using System;

namespace TMPro
{
    public sealed class TextMeshProUGUI { public string text = ""; }
}

public sealed class CostumeEntity
{
    public string id;
    public CostumeEntity(string id) { this.id = id; }
}

public sealed class UI_CostumePanel
{
    public static string NativeDescription = "- <indent=10>Native costume effect</indent>";
    public TMPro.TextMeshProUGUI tooltipEffectText = new();
    private void UpdateData(CostumeEntity costume) => tooltipEffectText.text = NativeDescription;
    public void Select(CostumeEntity costume) => UpdateData(costume);
    public void OnClosed() { }
}

namespace UnityEngine
{
    public static class Debug { public static void LogError(object value) { } }
}

namespace SephiriaOne
{
    internal readonly struct RabbitPotionSettings
    {
        public const int MpCostPerDrink = 10;
        public bool Infinite { get; }
        public bool Share { get; }
        public bool ConsumeMp { get; }
        public bool SuppressSurvival { get; }
        public RabbitPotionSettings(bool infinite, bool share, bool consumeMp = false, bool suppressSurvival = false)
        { Infinite = infinite; Share = share; ConsumeMp = consumeMp; SuppressSurvival = suppressSurvival; }
    }
    internal static class SessionSettings
    {
        internal static event Action SettingsChanged;
        internal static RabbitPotionSettings RabbitPotionsForDisplay { get; set; }
        internal static void Change(bool infinite, bool share, bool consumeMp = false, bool suppressSurvival = false)
        {
            RabbitPotionsForDisplay = new RabbitPotionSettings(infinite, share, consumeMp, suppressSurvival);
            SettingsChanged?.Invoke();
        }
    }
    internal static class RabbitPotionFeature { internal static bool Available { get; set; } }
    internal static class HarmonyRuntime { internal static void EnsureLoaded() { } }
}
