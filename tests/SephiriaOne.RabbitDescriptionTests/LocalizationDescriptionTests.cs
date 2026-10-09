using SephiriaOne;

internal static class LocalizationDescriptionTests
{
    public static int Run(Action<string> selectLanguage)
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        { if (!condition) throw new Exception(scenario); checks++; }
        const string native = "- <indent=10>Native effect and player-provided text</indent>";
        UI_CostumePanel.NativeDescription = native;
        RabbitPotionFeature.Available = true;
        RabbitLevelUpFeature.Available = true;
        SessionSettings.Change(true, true, true, true, 25, true);
        selectLanguage("en");
        RabbitDescriptionFeature.Initialize();
        var panel = new UI_CostumePanel();
        try
        {
            panel.Select(new CostumeEntity("HolyRabbit"));
            string english = panel.tooltipEffectText.text;
            Check(english.StartsWith(native + "\n") && english.Contains("25 MP") &&
                english.Contains("except Potion of Regeneration (Sample)") && english.Contains("Tension") && english.Contains("Each earned level grants one random non-HP/MP potion"),
                "English tooltip shows current amount, Sample exception, and level-up reward");
            selectLanguage("ko");
            Check(panel.tooltipEffectText.text.StartsWith(native + "\n") && panel.tooltipEffectText.text.Contains("25 MP") &&
                panel.tooltipEffectText.text.Contains("생존") && panel.tooltipEffectText.text.Contains("맛보기 샘플") &&
                panel.tooltipEffectText.text.Contains("보스 전투") &&
                !panel.tooltipEffectText.text.Contains("Survival") && !panel.tooltipEffectText.text.Contains("Each earned level"),
                "An open tooltip refreshes addon text on language change");
            Check(panel.tooltipEffectText.text.Split("<indent=10>").Length == 7,
                "Language refresh does not duplicate addon tooltip lines");
            selectLanguage("en");
            Check(panel.tooltipEffectText.text == english, "Switching back exactly restores the English tooltip");
            foreach (string language in new[] { "en", "ko" })
            {
                selectLanguage(language);
                string sample = language == "ko" ? "맛보기 샘플" : "except Potion of Regeneration (Sample)";
                SessionSettings.Change(false, true);
                Check(panel.tooltipEffectText.text.Contains(sample),
                    language + " sharing-only tooltip identifies the Sample exception");
                SessionSettings.Change(false, false, true, false, 25);
                Check(panel.tooltipEffectText.text.Contains(sample) && panel.tooltipEffectText.text.Contains("25 MP"),
                    language + " MP-only tooltip identifies the Sample exception");
            }
            RabbitDescriptionFeature.Shutdown();
            Check(panel.tooltipEffectText.text == native, "Shutdown restores the native tooltip");
            selectLanguage("ko");
            Check(panel.tooltipEffectText.text == native, "Shutdown unsubscribes language refresh");
        }
        finally
        {
            RabbitDescriptionFeature.Shutdown();
            SessionSettings.Change(false, false);
            selectLanguage("en");
        }
        return checks;
    }
}
