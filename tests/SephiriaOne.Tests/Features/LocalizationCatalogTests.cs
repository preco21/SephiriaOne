using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SephiriaOne;

internal static class LocalizationCatalogTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        Dictionary<string, string> Read(string language)
        {
            using var stream = typeof(L).Assembly.GetManifestResourceStream("SephiriaOne.Localization." + language + ".json")!;
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())!;
        }
        var english = Read("en");
        var korean = Read("ko");
        Check(english.Count > 300 && english.Keys.ToHashSet().SetEquals(korean.Keys), "Bundled catalogs must cover the same complete set of keys");
        foreach (var catalog in new[] { english, korean })
        {
            string conversion = string.Format(catalog["[Z] \"{0}\" infected you! You are a Zombie - hunt remaining [H] Humans!"], new string('A', 32));
            Check(conversion.Length <= 120 && conversion.Contains("[Z]") && conversion.Contains("[H]") && conversion.Contains(new string('A', 32)),
                "Default conversion translations fit the stock limit with a full-length name and both faction markers");
        }
        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-Catalog-" + Guid.NewGuid().ToString("N"));
        var warnings = new List<string>();
        try
        {
            L.Initialize(folder, warnings.Add);
            Check(warnings.Count == 0, "Bundled catalog initialization has no rejected JSON or format entries: " + string.Join("; ", warnings));
            Check(L.TrySetLanguage("en", out string error), error);
            foreach (var entry in english)
                Check(entry.Key == entry.Value && L.T(entry.Key) == entry.Key, "Default English wording changed: " + entry.Key);
            Check(L.TrySetLanguage("ko", out error), error);
            foreach (var entry in korean)
            {
                Check(!string.IsNullOrWhiteSpace(entry.Value) && L.T(entry.Key) == entry.Value, "Korean entry was rejected or missing: " + entry.Key);
                // Presence alone misses English copied into ko.json. Product
                // names and purely symbolic templates intentionally stay intact.
                // Product name and the requested PvP section acronym are language-neutral.
                if (entry.Key != "SephiriaOne" && entry.Key != "PvP" && Regex.IsMatch(entry.Key, "[A-Za-z]{2}"))
                    Check(Regex.IsMatch(entry.Value, "[가-힣]"), "Korean wording is untranslated: " + entry.Key);
            }
            foreach (var stat in StatCatalog.All)
            {
                Check(korean.ContainsKey(stat.Unit), "Missing stat unit translation: " + stat.Unit);
                Check(korean.ContainsKey(stat.Label), "Missing stat label translation: " + stat.Label);
            }
            foreach (var resource in ResourceCatalog.All)
                Check(korean.ContainsKey(resource.Label), "Missing resource label translation: " + resource.Label);
            foreach (var merchant in MerchantCatalog.All)
                Check(korean.ContainsKey(merchant.Name), "Missing merchant label translation: " + merchant.Name);
            foreach (var effect in Enum.GetValues<PvpDotEffect>())
                Check(korean.ContainsKey(PvpDotSettings.Label(effect)), "Missing DoT label translation: " + effect);
            foreach (string label in new[] { "Rapid Hits", "History window (s)", "Full-damage hits/s", "Reduction exponent",
                "Stored burst (% HP)", "Reset rapid settings", "Periodic recovery", "Recovery interval (s)",
                "Rolling hit history, not a budget reset.\nDefault 1 s · separate attack categories.",
                "Default on / 1 s · 0.1..60 s\nRestores burst + history, even while firing." })
                Check(korean.ContainsKey(label), "Missing Rapid Hits control translation: " + label);
            foreach (string label in new[] { "Perfect Guard MP multiplier", "Native MP cost (x)", "When on, shield MP minimum does not apply." })
                Check(korean.ContainsKey(label), "Missing Perfect Guard control translation: " + label);
            foreach (string label in new[] { "Limit secondary spawn rate", "Creations / player / s", "Creations / source / s", "Reset spawn rates",
                "Independent rate budget: stores 1 second and refills continuously. Includes supported secondary effects beyond active caps; denied attempts are skipped." })
                Check(korean.ContainsKey(label), "Missing projectile spawn-rate control translation: " + label);

            // Catch new explicit literal lookup sites that omitted their catalog entry.
            // Dynamic widget label arrays have separate installed-UI and feature checks.
            string sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SephiriaOne"));
            Check(Directory.Exists(sourceRoot), "Run the source-linked catalog suite from this checkout's build output");
            var literals = new Regex("L\\.[TF]\\(\\s*(\"(?:[^\"\\\\]|\\\\.)*\")");
            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
                foreach (Match match in literals.Matches(File.ReadAllText(file)))
                {
                    string key = JsonConvert.DeserializeObject<string>(match.Groups[1].Value)!;
                    Check(korean.ContainsKey(key), "Missing explicit translation in " + Path.GetFileName(file) + ": " + key);
                }

            // Real installed catalogs survive addon upgrades and may predate
            // hundreds of keys. Every bundled Korean entry must remain usable.
            const string oldKorean = "{\"Stats\":\"사용자 능력치\"}";
            File.WriteAllText(Path.Combine(folder, "ko.json"), oldKorean);
            File.WriteAllText(Path.Combine(folder, "en.json"), "{}");
            Check(L.Reload(out error), error);
            foreach (var entry in korean)
                Check(L.T(entry.Key) == (entry.Key == "Stats" ? "사용자 능력치" : entry.Value),
                    "Older Korean catalog lost bundled translation: " + entry.Key);
            Check(File.ReadAllText(Path.Combine(folder, "ko.json")) == oldKorean,
                "Catalog upgrades preserve customized files exactly");
        }
        finally { L.Shutdown(); }
        return checks;
    }
}
