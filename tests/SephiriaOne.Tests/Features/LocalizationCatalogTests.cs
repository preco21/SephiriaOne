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
                Check(!string.IsNullOrWhiteSpace(entry.Value) && L.T(entry.Key) == entry.Value, "Korean entry was rejected or missing: " + entry.Key);
            foreach (var stat in StatCatalog.All)
                Check(korean.ContainsKey(stat.Unit), "Missing stat unit translation: " + stat.Unit);
            foreach (var resource in ResourceCatalog.All)
                Check(korean.ContainsKey(resource.Label), "Missing resource label translation: " + resource.Label);
            foreach (var merchant in MerchantCatalog.All)
                Check(korean.ContainsKey(merchant.Name), "Missing merchant label translation: " + merchant.Name);

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
        }
        finally { L.Shutdown(); }
        return checks;
    }
}
