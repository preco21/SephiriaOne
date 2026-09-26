using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static class RabbitDescriptionFeature
    {
        private static bool installed;
        private static WeakReference<UI_CostumePanel> selectedPanel;
        private static string selectedCostume;
        private static string nativeDescription;
        private static string renderedDescription;

        public static bool Available => installed;

        public static void Initialize()
        {
            if (installed) return;
            try
            {
                HarmonyRuntime.EnsureLoaded();
                InstallHooks();
                SessionSettings.SettingsChanged += Refresh;
                installed = true;
            }
            catch (Exception error)
            {
                installed = false;
                Debug.LogError("[SephiriaOne] Rabbit costume description unavailable: " + error);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallHooks() => RabbitDescriptionHooks.Install();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UninstallHooks() => RabbitDescriptionHooks.Uninstall();

        public static void Shutdown()
        {
            SessionSettings.SettingsChanged -= Refresh;
            try { RefreshNative(); }
            catch (Exception error) { Debug.LogError("[SephiriaOne] Rabbit description cleanup failed: " + error); }
            if (installed)
            {
                UninstallHooks();
                installed = false;
            }
            selectedPanel = null;
            selectedCostume = null;
            nativeDescription = null;
            renderedDescription = null;
        }

        public static void Refresh()
        {
            if (!Available || selectedPanel == null || !selectedPanel.TryGetTarget(out UI_CostumePanel panel) ||
                panel == null || panel.tooltipEffectText == null) return;
            try { Apply(panel, selectedCostume); }
            catch (Exception error) { Debug.LogError("[SephiriaOne] Rabbit description refresh failed: " + error); }
        }

        internal static void AfterUpdate(UI_CostumePanel panel, CostumeEntity costume)
        {
            try
            {
                if (selectedPanel != null && selectedPanel.TryGetTarget(out UI_CostumePanel previous) &&
                    !ReferenceEquals(previous, panel)) RefreshNative();
                selectedPanel = new WeakReference<UI_CostumePanel>(panel);
                selectedCostume = costume != null ? costume.id : null;
                nativeDescription = panel.tooltipEffectText != null ? panel.tooltipEffectText.text : null;
                renderedDescription = nativeDescription;
                Apply(panel, selectedCostume);
            }
            catch (Exception error) { Debug.LogError("[SephiriaOne] Rabbit description update failed: " + error); }
        }

        internal static void AfterClose(UI_CostumePanel panel)
        {
            try
            {
                if (selectedPanel != null && selectedPanel.TryGetTarget(out UI_CostumePanel selected) &&
                    ReferenceEquals(selected, panel))
                {
                    RefreshNative();
                    selectedPanel = null;
                    selectedCostume = null;
                    nativeDescription = null;
                    renderedDescription = null;
                }
            }
            catch (Exception error) { Debug.LogError("[SephiriaOne] Rabbit description close failed: " + error); }
        }

        private static void Apply(UI_CostumePanel panel, string costumeId)
        {
            if (panel.tooltipEffectText.text != renderedDescription) return;
            RabbitPotionSettings settings = SessionSettings.RabbitPotionsForDisplay;
            renderedDescription = RabbitDescriptionText.Decorate(nativeDescription,
                costumeId, settings.Infinite, settings.Share, RabbitPotionFeature.Available,
                settings.ConsumeMp, settings.SuppressSurvival, settings.MpCostPerDrink);
            panel.tooltipEffectText.text = renderedDescription;
        }

        private static void RefreshNative()
        {
            if (selectedPanel != null && selectedPanel.TryGetTarget(out UI_CostumePanel panel) &&
                panel != null && panel.tooltipEffectText != null)
                if (panel.tooltipEffectText.text == renderedDescription)
                    panel.tooltipEffectText.text = nativeDescription;
        }
    }
}
