using SephiriaOne;

internal static class LocalizationFeatureTests
{
    // The caller initializes the catalogs and supplies the language selector.
    public static int Run(Action<string> selectLanguage)
    {
        int checks = 0;
        void Check(bool condition, string scenario)
        { if (!condition) throw new Exception(scenario); checks++; }
        try
        {
            selectLanguage("en");
            Check(StatCommand.Parse("/stats luck +10", out var englishStat, out _) == StatParseResult.Valid,
                "English stat command parses");
            string statUnit = StatCatalog.Find("luck")!.Unit;
            string resourceLabel = ResourceCatalog.Get(ResourceKind.Slots).Label;
            selectLanguage("ko");
            Check(StatCommand.Parse("/stats invalid 10", out _, out string error) == StatParseResult.Invalid &&
                error == "알 수 없는 능력치입니다. 지원하는 이름과 단위는 /stats list로 확인하세요.",
                "Unknown-stat feedback is Korean");
            Check(StatCommand.Parse("/stats luck +10", out var koreanStat, out _) == StatParseResult.Valid &&
                koreanStat.Stat == englishStat.Stat && koreanStat.Operation == englishStat.Operation &&
                koreanStat.Amount == englishStat.Amount, "Language keeps command semantics identical");
            Check(StatCatalog.Find("luck")!.Unit == statUnit && ResourceCatalog.Get(ResourceKind.Slots).Label == resourceLabel,
                "Canonical units and resource labels remain unchanged");
            Check(FountainCommand.Parse("/fountain 5", out var fountain, out _) == FountainParseResult.Valid &&
                !fountain.TryPlan(Array.Empty<int>(), 0, out _, out _, out error) && error.Contains("플레이어"),
                "Fountain planning failure is Korean");
            Check(ChoiceCommand.Parse("/choices item x2", out _, out error) == ChoiceParseResult.Invalid &&
                error.Contains("추가 후보") && error.Contains("set/add/sub"), "Choice guidance preserves operation tokens");
            Check(!new ResourceSetting(ResourceMode.Set, 5).TryTarget(6, 6, 96, out _, out error) &&
                error.Contains("6..96") && error.EndsWith(" 아무 플레이어도 변경되지 않았습니다."),
                "Resource validation translates with exact numeric limits");
            Check(RabbitCommand.Parse("/one rabbit mp-cost 25", out var rabbit, out _) == RabbitParseResult.Valid &&
                rabbit.Amount == 25 && RabbitCommand.Usage.Contains("/one rabbit mp-cost on|off|<0..10000>") &&
                RabbitCommand.Usage.Contains("호스트"), "Rabbit help translates without altering syntax");
            Check(MerchantCommand.Parse("/one merchant chance 100", out var merchant, out _) == MerchantParseResult.Valid &&
                merchant.Chance == 100 && MerchantCommand.Usage.Contains("/one merchant chance <0..100>") &&
                MerchantCommand.Usage.Contains("호스트"), "Merchant help translates without altering bounds");
            const string native = "Native costume text <color=red>untouched</color>";
            string description = RabbitDescriptionText.Decorate(native, "HolyRabbit", true, true, true, true, true, 25);
            Check(description.StartsWith(native + "\n") && description.Contains("25 MP") && description.Contains("생존") &&
                description.Split("<indent=10>").Length == 5 && description.Split("</indent>").Length == 5,
                "Rabbit description preserves native text and rich text while translating addon lines");
            Check(RabbitDescriptionText.Decorate(native, "OtherCostume", true, true, true) == native,
                "Other costume descriptions remain native");
        }
        finally { selectLanguage("en"); }
        return checks;
    }
}
