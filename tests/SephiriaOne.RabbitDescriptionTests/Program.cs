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
Check(infiniteOnly.Contains("Healing potions are not consumed"), "infinite line appears");
Check(!infiniteOnly.Contains("Nearby allies"), "share line is absent");

string shareOnly = Show(rabbit, false, true);
Check(shareOnly.Contains("Nearby allies"), "share line appears");
Check(!shareOnly.Contains("not consumed"), "infinite line is absent");

string both = Show(rabbit, true, true);
Check(both.Contains("not consumed") && both.Contains("Nearby allies"), "both lines appear");
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

const string nativeIdentical = native + "\n- <indent=10>Healing potions are not consumed after a successful drink (requires one potion).</indent>";
UI_CostumePanel.NativeDescription = nativeIdentical;
RabbitDescriptionFeature.Initialize();
panel.Select(new CostumeEntity(rabbit));
SessionSettings.Change(true, false);
Check(panel.tooltipEffectText.text == nativeIdentical + "\n- <indent=10>Healing potions are not consumed after a successful drink (requires one potion).</indent>",
    "option line is appended even when native text coincidentally ends with it");
SessionSettings.Change(false, false);
Check(panel.tooltipEffectText.text == nativeIdentical, "reset preserves identical native trailing line");
SessionSettings.Change(true, false);
RabbitDescriptionFeature.Shutdown();
Check(panel.tooltipEffectText.text == nativeIdentical, "unload preserves identical native trailing line");
Check(Array.TrueForAll(typeof(RabbitDescriptionFeature).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
    field => field.FieldType.Namespace != "HarmonyLib"), "facade has no Harmony-typed fields before runtime bootstrap");
Console.WriteLine($"Rabbit description checks passed: {checks}");
