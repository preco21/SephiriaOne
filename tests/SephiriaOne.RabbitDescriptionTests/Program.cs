using SephiriaOne;
using System.Reflection;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}
const string native = "- <indent=10>Native costume effect</indent>";
const string rabbit = "HolyRabbit";
string Show(string costume, bool infinite, bool share, bool compatible = true) =>
    RabbitDescriptionText.Decorate(native, costume, infinite, share, compatible);

Check(Show(rabbit, false, false) == native, "both options off retains native text");
Check(RabbitDescriptionText.Decorate("", rabbit, true, true, true) == "", "locked or empty native tooltip stays empty");
Check(Show("PinkRabbit", true, true) == native, "other costumes retain native text");
Check(Show(rabbit, true, true, false) == native, "gameplay incompatibility hides claims");

string infiniteOnly = Show(rabbit, true, false);
Check(infiniteOnly.StartsWith(native + "\n"), "native text remains first");
Check(infiniteOnly.Contains("HP potions are not consumed"), "infinite line appears");
Check(!infiniteOnly.Contains("Nearby allies"), "share line is absent");

string shareOnly = Show(rabbit, false, true);
Check(shareOnly.Contains("Nearby allies"), "share line appears");
Check(!shareOnly.Contains("not consumed"), "infinite line is absent");

string both = Show(rabbit, true, true);
Check(both.Contains("not consumed") && both.Contains("Nearby allies"), "both lines appear");
string costOnly = RabbitDescriptionText.Decorate(native, rabbit, false, false, true, true, false);
Check(costOnly.Contains("10 MP") && costOnly.Contains("HP potion") && !costOnly.Contains("Survival"), "MP cost appears only when enabled");
string suppressOnly = RabbitDescriptionText.Decorate(native, rabbit, false, false, true, false, true);
Check(suppressOnly.Contains("Survival") && suppressOnly.Contains("HP potion") && !suppressOnly.Contains("10 MP"), "Survival suppression appears only when enabled");
Check(suppressOnly.Contains("except Potion of Regeneration (Sample)"), "Suppression tooltip identifies the Sample exception");
Check(RabbitDescriptionText.Decorate(native, rabbit, false, false, false, true, true) == native,
    "Compatibility loss hides balance claims");
Check(RabbitDescriptionText.Decorate(native, "PinkRabbit", false, false, true, true, true) == native,
    "Other costumes retain native text with balance flags");
Check(RabbitDescriptionText.Decorate(native, rabbit, false, false, true) == native, "reset retains native text");
Check(RabbitDescriptionText.Decorate(native, rabbit, true, true, false) == native, "compatibility loss retains native text");


RabbitPotionFeature.Available = true;
RabbitDescriptionFeature.Initialize();
Check(RabbitDescriptionFeature.Available, "native panel signature accepts hook");
var panel = new UI_CostumePanel();
panel.Select(new CostumeEntity(rabbit));
Check(panel.tooltipEffectText.text == native, "initially off retains native UI");
SessionSettings.Change(true, false);
Check(panel.tooltipEffectText.text.Contains("not consumed"), "policy event refreshes current tooltip");
SessionSettings.Change(true, true);
Check(panel.tooltipEffectText.text.Contains("Nearby allies"), "second option refreshes current tooltip");
Check(panel.tooltipEffectText.text.Split("Nearby allies").Length == 2, "refresh does not duplicate line");
SessionSettings.Change(false, false, true, true);
Check(panel.tooltipEffectText.text.Contains("10 MP") && panel.tooltipEffectText.text.Contains("Survival"), "balance options refresh current tooltip");
SessionSettings.Change(false, false, true, true, 25);
Check(panel.tooltipEffectText.text.Contains("25 MP") && !panel.tooltipEffectText.text.Contains("10 MP"), "custom amount replaces old tooltip cost");
SessionSettings.Change(false, false, true, true, 0);
Check(panel.tooltipEffectText.text.Contains("0 MP") && panel.tooltipEffectText.text.Split("MP;").Length == 2, "zero amount refreshes without duplicate lines");
SessionSettings.Change(false, false, false, true, 25);
Check(!panel.tooltipEffectText.text.Contains(" MP;"), "disabled fee hides custom cost description");
SessionSettings.Change(true, true);
panel.Select(new CostumeEntity("PinkRabbit"));
Check(panel.tooltipEffectText.text == native, "selection of another costume stays native");
panel.Select(new CostumeEntity(rabbit));
Check(panel.tooltipEffectText.text.Contains("Nearby allies"), "reselecting rabbit decorates fresh native UI");
RabbitPotionFeature.Available = false;
RabbitDescriptionFeature.Refresh();
Check(panel.tooltipEffectText.text == native, "gameplay compatibility loss removes text");
RabbitPotionFeature.Available = true;
SessionSettings.Change(false, false);
Check(panel.tooltipEffectText.text == native, "reset restores native UI");
SessionSettings.Change(true, true);
panel.OnClosed();
SessionSettings.Change(false, false);
Check(panel.tooltipEffectText.text == native, "closed panel does not retain added text");
panel.Select(new CostumeEntity(rabbit));
SessionSettings.Change(true, true);
RabbitDescriptionFeature.Shutdown();
Check(!RabbitDescriptionFeature.Available && panel.tooltipEffectText.text == native, "unload removes rendered text and hook");

const string nativeIdentical = native + "\n- <indent=10>HP potions are not consumed after a successful drink (requires one potion).</indent>";
UI_CostumePanel.NativeDescription = nativeIdentical;
RabbitDescriptionFeature.Initialize();
panel.Select(new CostumeEntity(rabbit));
SessionSettings.Change(true, false);
Check(panel.tooltipEffectText.text == nativeIdentical + "\n- <indent=10>HP potions are not consumed after a successful drink (requires one potion).</indent>",
    "option line is appended even when native text coincidentally ends with it");
SessionSettings.Change(false, false);
Check(panel.tooltipEffectText.text == nativeIdentical, "reset preserves identical native trailing line");
SessionSettings.Change(true, false);
RabbitDescriptionFeature.Shutdown();
Check(panel.tooltipEffectText.text == nativeIdentical, "unload preserves identical native trailing line");
Check(Array.TrueForAll(typeof(RabbitDescriptionFeature).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
    field => field.FieldType.Namespace != "HarmonyLib"), "facade has no Harmony-typed fields before runtime bootstrap");
Console.WriteLine($"Rabbit description checks passed: {checks}");
L.Initialize(Path.Combine(Path.GetTempPath(), "SephiriaOne-Description-Language-" + Guid.NewGuid().ToString("N")), _ => { });
Console.WriteLine($"Rabbit localized description checks passed: {LocalizationDescriptionTests.Run(language =>
{
    if (!L.TrySetLanguage(language, out string error)) throw new Exception(error);
})}");
L.Shutdown();
