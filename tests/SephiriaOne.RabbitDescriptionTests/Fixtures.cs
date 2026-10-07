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
    internal static class SessionSettings
    {
        internal static event Action SettingsChanged;
        internal static RabbitPotionSettings RabbitPotionsForDisplay { get; set; }
        internal static void Change(bool infinite, bool share, bool consumeMp = false, bool suppressSurvival = false, int mpCost = 10, bool levelUpPotion = false)
        {
            RabbitPotionsForDisplay = new RabbitPotionSettings(infinite, share, consumeMp, suppressSurvival, mpCost, levelUpPotion);
            SettingsChanged?.Invoke();
        }
    }
    internal static class RabbitPotionFeature { internal static bool Available { get; set; } }
    internal static class RabbitLevelUpFeature { internal static bool Available { get; set; } = true; }
    internal static class HarmonyRuntime { internal static void EnsureLoaded() { } }
}
