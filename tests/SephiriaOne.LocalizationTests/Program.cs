using System.Globalization;
using Newtonsoft.Json;
using SephiriaOne;

int passed = 0, failed = 0;
void Equal<T>(T expected, T actual, string context = "")
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{context} Expected <{expected}>, got <{actual}>.");
}
void Check(bool condition, string context)
{
    if (!condition) throw new Exception(context);
}
void Run(string name, Action<string, List<string>> test)
{
    string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne.LocalizationTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var warnings = new List<string>();
    try { L.Shutdown(); test(folder, warnings); passed++; }
    catch (Exception ex) { Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); failed++; }
    finally { L.Shutdown(); Directory.Delete(folder, true); }
}
void Write(string folder, string file, string json) => File.WriteAllText(Path.Combine(folder, file), json);
void Catalog(string folder, string language, params (string Key, object Value)[] entries) =>
    Write(folder, language + ".json", JsonConvert.SerializeObject(entries.ToDictionary(e => e.Key, e => e.Value)));
void Korean(string folder, List<string> warnings)
{
    Write(folder, "config.json", "{\"language\":\"ko\"}");
    L.Initialize(folder, warnings.Add);
    Equal("ko", L.Language);
}

Run("initialization seeds embedded defaults and English selection", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    Equal("en", L.Language);
    foreach (string file in new[] { "config.json", "en.json", "ko.json" })
        Check(File.Exists(Path.Combine(folder, file)), "Missing seeded file " + file);
    Equal("English fallback", L.T("Only English"));
    Equal(0, warnings.Count);
});
Run("Korean lookup and English fallback", (folder, warnings) =>
{
    Korean(folder, warnings);
    Equal("안녕하세요", L.T("Hello"));
    Equal("English fallback", L.T("Only English"));
    Equal("Unknown", L.T("Unknown"));
});
Run("lookup is exact and does not rewrite player or command fragments", (folder, warnings) =>
{
    Korean(folder, warnings);
    Equal("Player Hello /one Hello", L.T("Player Hello /one Hello"));
    Equal("Hello 플레이어: 2.5", L.F("Player {0}: {1:0.0}", "Hello", 2.5));
});
Run("formatting uses invariant culture and escaped braces", (folder, warnings) =>
{
    Korean(folder, warnings);
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        Equal("P 플레이어: 2.5", L.F("Player {0}: {1:0.0}", "P", 2.5));
        Equal("리터럴 {value} 3", L.F("Literal {{value}} {0}", 3));
    }
    finally { CultureInfo.CurrentCulture = previous; }
});
Run("initialization preserves customized catalogs and config", (folder, warnings) =>
{
    Catalog(folder, "en", ("Hello", "Customized English"));
    Catalog(folder, "ko", ("Hello", "사용자 번역"));
    string custom = File.ReadAllText(Path.Combine(folder, "ko.json"));
    Korean(folder, warnings);
    Equal("사용자 번역", L.T("Hello"));
    Equal(custom, File.ReadAllText(Path.Combine(folder, "ko.json")));
});
Run("language selection persists and notifies once", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    int revision = L.Revision, changed = 0;
    L.Changed += () => changed++;
    Check(L.TrySetLanguage("ko", out var error), error);
    Equal("ko", L.Language);
    Equal("안녕하세요", L.T("Hello"));
    Check(L.Revision > revision, "Language change must increase revision");
    Equal(1, changed);
    Check(L.TrySetLanguage("ko", out error), error);
    Equal(1, changed);
    L.Shutdown();
    L.Initialize(folder, warnings.Add);
    Equal("ko", L.Language);
});
Run("language codes are a fixed allowlist", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    foreach (string language in new[] { "../ko", "KO", "kr", "", null })
    {
        Check(!L.TrySetLanguage(language, out var error), "Invalid language accepted");
        Check(!string.IsNullOrEmpty(error), "Failure should explain the error");
    }
    Equal("en", L.Language);
});
Run("lookup is cached until explicit reload", (folder, warnings) =>
{
    Korean(folder, warnings);
    Catalog(folder, "ko", ("Hello", "수정됨"));
    Equal("안녕하세요", L.T("Hello"));
    File.Delete(Path.Combine(folder, "en.json"));
    Equal("English fallback", L.T("Only English"));
});
Run("reload publishes changed catalogs and config together", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    int revision = L.Revision, changed = 0;
    L.Changed += () => changed++;
    Catalog(folder, "ko", ("Hello", "수정됨"));
    Write(folder, "config.json", "{\"language\":\"ko\"}");
    Check(L.Reload(out var error), error);
    Equal("수정됨", L.T("Hello"));
    Equal("ko", L.Language);
    Equal(1, changed);
    Check(L.Revision > revision, "Reload must increase revision");
});
foreach (string invalid in new[] { "{", "[]", "{\"Hello\":null}", "{\"Hello\":1}", "{\"Hello\":{\"x\":\"y\"}}", "{\"Hello\":\"a\",\"Hello\":\"b\"}", "{} {}", "{\"Hello\":\"a\",}", "{\"Hello\":\"a\nb\"}", "{\"Hello\":\"a\\'b\"}", "{'Hello':'a'}", "{/*comment*/\"Hello\":\"a\"}" })
    Run("malformed reload preserves active state: " + invalid, (folder, warnings) =>
    {
        Korean(folder, warnings);
        int revision = L.Revision, changed = 0;
        L.Changed += () => changed++;
        Write(folder, "ko.json", invalid);
        Write(folder, "config.json", "{\"language\":\"en\"}");
        Check(!L.Reload(out var error), "Malformed reload accepted");
        Check(!string.IsNullOrEmpty(error), "Failure should explain the error");
        Equal("ko", L.Language);
        Equal("안녕하세요", L.T("Hello"));
        Equal(revision, L.Revision);
        Equal(0, changed);
    });
foreach (string config in new[] { "{", "{\"language\":\"ja\"}", "{\"language\":7}", "{}", "{\"language\":\"ko\",\"language\":\"en\"}" })
    Run("invalid startup config falls back to English: " + config, (folder, warnings) =>
    {
        Write(folder, "config.json", config);
        L.Initialize(folder, warnings.Add);
        Equal("en", L.Language);
        Equal("Hello", L.T("Hello"));
        Check(warnings.Count == 1 && warnings[0].Length <= 512, "Expected one bounded warning");
        Equal(config, File.ReadAllText(Path.Combine(folder, "config.json")));
    });
foreach (string translation in new[] { "잘못 {1}", "잘못 {", "잘못 }", "누락", "{0}{64}", "{0,99999999}" })
    Run("unsafe translation falls back per entry: " + translation, (folder, warnings) =>
    {
        Catalog(folder, "ko", ("Count {0}", translation), ("Hello", "정상"));
        Korean(folder, warnings);
        Equal("Count 3", L.F("Count {0}", 3));
        Equal("정상", L.T("Hello"));
        Check(warnings.Count == 1 && warnings[0].Length <= 512, "Expected bounded invalid-entry warning");
    });
Run("translations may reorder and repeat existing arguments", (folder, warnings) =>
{
    Catalog(folder, "ko", ("Player {0}: {1:0.0}", "{1:0.00}: {0} / {0}"));
    Korean(folder, warnings);
    Equal("2.50: P / P", L.F("Player {0}: {1:0.0}", "P", 2.5));
});
Run("invalid English format entry falls back to source", (folder, warnings) =>
{
    Catalog(folder, "en", ("Count {0}", "Count {2}"));
    L.Initialize(folder, warnings.Add);
    Equal("Count 3", L.F("Count {0}", 3));
});
foreach (string blank in new[] { "", "  \t" })
    Run("blank translations cannot erase labels or diagnostic state: " + blank.Length, (folder, warnings) =>
    {
        Catalog(folder, "en", ("Hello", blank));
        Catalog(folder, "ko", ("Hello", blank));
        Korean(folder, warnings);
        Equal("Hello", L.T("Hello"));
        Check(warnings.Count == 1, "Blank entries must warn and use source fallback");
    });
Run("format failures cannot escape into the UI", (folder, warnings) =>
{
    Korean(folder, warnings);
    Equal("Count {0}", L.F("Count {0}"));
    Equal("Broken {", L.F("Broken {", 1));
});
Run("missing reload file retains state", (folder, warnings) =>
{
    Korean(folder, warnings);
    File.Delete(Path.Combine(folder, "ko.json"));
    Check(!L.Reload(out _), "Missing reload file must fail");
    Equal("안녕하세요", L.T("Hello"));
});
Run("catalog file size is bounded", (folder, warnings) =>
{
    Korean(folder, warnings);
    Write(folder, "ko.json", new string(' ', 1024 * 1024 + 1));
    Check(!L.Reload(out _), "Oversized catalog accepted");
    Equal("안녕하세요", L.T("Hello"));
});
Run("catalog entry count is bounded", (folder, warnings) =>
{
    Korean(folder, warnings);
    Write(folder, "ko.json", JsonConvert.SerializeObject(Enumerable.Range(0, 4097).ToDictionary(i => "key" + i, _ => "value")));
    Check(!L.Reload(out _), "Too many catalog entries accepted");
});
Run("catalog text length is bounded", (folder, warnings) =>
{
    Korean(folder, warnings);
    Catalog(folder, "ko", ("Hello", new string('a', 16385)));
    Check(!L.Reload(out _), "Oversized catalog string accepted");
});
Run("unavailable directory fails safely", (folder, warnings) =>
{
    string obstruction = Path.Combine(folder, "a-file");
    File.WriteAllText(obstruction, "occupied");
    L.Initialize(obstruction, warnings.Add);
    Equal("en", L.Language);
    Equal("Hello", L.T("Hello"));
    Check(warnings.Count == 1, "Expected initialization failure warning");
    Check(!L.TrySetLanguage("ko", out _), "Unavailable storage must not accept selection");
});
Run("failed atomic selection write leaves language unchanged", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    int revision = L.Revision;
    File.Delete(Path.Combine(folder, "config.json"));
    Directory.CreateDirectory(Path.Combine(folder, "config.json"));
    Check(!L.TrySetLanguage("ko", out _), "Blocked config path accepted");
    Equal("en", L.Language);
    Equal(revision, L.Revision);
    Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "Temporary write file leaked");
});
Run("shutdown clears selection and subscribers", (folder, warnings) =>
{
    Korean(folder, warnings);
    int changed = 0;
    L.Changed += () => changed++;
    L.Shutdown();
    Equal("en", L.Language);
    Equal("Hello", L.T("Hello"));
    Check(!L.Reload(out _), "Shutdown service must not reload");
    L.Initialize(folder, warnings.Add);
    Equal(0, changed);
});
Run("throwing change subscribers cannot break a successful language update", (folder, warnings) =>
{
    L.Initialize(folder, warnings.Add);
    int changed = 0;
    L.Changed += () => throw new InvalidOperationException("broken view");
    L.Changed += () => changed++;
    Check(L.TrySetLanguage("ko", out var error), error);
    Equal("ko", L.Language);
    Equal(1, changed);
    Equal(1, warnings.Count);
});
Run("throwing warning logger cannot escape initialization", (folder, warnings) =>
{
    Write(folder, "config.json", "broken");
    L.Initialize(folder, _ => throw new InvalidOperationException("broken logger"));
    Equal("en", L.Language);
});
Run("failed startup can recover after config is repaired", (folder, warnings) =>
{
    Write(folder, "config.json", "broken");
    L.Initialize(folder, warnings.Add);
    Write(folder, "config.json", "{\"language\":\"ko\"}");
    Check(L.Reload(out var error), error);
    Equal("안녕하세요", L.T("Hello"));
});
Console.WriteLine($"Localization checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
