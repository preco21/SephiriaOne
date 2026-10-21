using System.Reflection;
using HarmonyLib;

internal static class GameRapidPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Rapid Hits panel: " + why); }
        Require(AccessTools.DeclaredMethod(panel, "ContentForPage").Invoke(null, new object[] { 19 })?.ToString() == "None",
            "Rapid Hits requests no participant details");
        Require(Code("Initialize").Any(i => i.LoadsConstant("Rapid Hits")) &&
            Code("BuildPage").Any(i => Call(i, "SettingsPanel", "BuildPvpRapidEditor")) &&
            Code("Refresh").Any(i => Call(i, "SettingsPanel", "RefreshPvpRapid")),
            "Rapid Hits has a top-level tab, build route and early refresh route");
        Require(Code("BuildPvpBalanceEditor").Any(i => i.LoadsConstant("Rapid Hits")) &&
            !Code("BuildPvpBalanceEditor").Any(i => i.LoadsConstant("/one friendlyfire hit-frequency")),
            "PvP Balance links to the dedicated frequency controls");
        var build = Code("BuildPvpRapidEditor");
        Require(build.Any(i => i.LoadsConstant("/one friendlyfire hit-frequency")) &&
            build.Any(i => Call(i, "SettingsPanel", "AddCheckbox")) &&
            new[] { "window", "rate", "exponent", "burst" }.All(key => build.Any(i => i.LoadsConstant(key))),
            "Independent rapid values and the existing toggle use the shared host command family");
        var apply = Code("ApplyPvpRapid");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire rapid ")) &&
            apply.Any(i => Call(i, "TMP_InputField", "get_text")) && apply.Any(i => Call(i, "SettingsPanel", "Execute")),
            "Apply sends the exact typed value through the validated settings action");
        Require(Code("ResetPvpRapid").Any(i => i.LoadsConstant("/one friendlyfire rapid reset")) &&
            !Code("ResetPvpRapid").Any(i => Call(i, "SettingsPanel", "ClearFriendlyRefill")),
            "Reset rapid uses the four-value reset and preserves the refill draft");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearPvpRapid")) &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "pvpRapidEditors"),
            "Scope, close, page and language lifetimes retire rapid drafts");
        Require(Code("RefreshPvpRapidValue").Any(i => i.operand is FieldInfo f && f.Name == "Draft") &&
            Code("RefreshPvpRapidValue").Any(i => i.operand is FieldInfo f && f.Name == "LastValue") &&
            Code("RefreshPvpRapidValue").Any(i => Call(i, "TMP_InputField", "SetTextWithoutNotify")) &&
            !Code("RefreshPvpRapid").Concat(Code("RefreshPvpRapidValue")).Any(i => Call(i, "SettingsPanel", "Execute")),
            "Refresh preserves drafts and caches unchanged numeric values without dispatching actions");
        Require(Code("RefreshPvpRapid").Any(i => i.operand is FieldInfo f && f.Name == "pvpRapidLastBurst") &&
            Code("RefreshPvpRapid").Any(i => i.operand is FieldInfo f && f.Name == "pvpRapidLastRefill"),
            "Recovery text changes only when the applied burst or refill changes");
        Require(Code("RefreshPvpRapid").Any(i => i.operand is FieldInfo f && f.Name == "pvpRapidLastExponent") &&
            Code("RefreshPvpRapid").Any(i => Call(i, "SettingsSnapshot", "get_FaultedFeature")),
            "Applied exponent examples are cached and rapid reset preserves combat fault recovery");
        var example = AccessTools.DeclaredMethod(panel, "PvpRapidExample");
        Require(example.Invoke(null, new object[] { 0m })?.ToString() == "Frequency damage: 2× threshold 100%; 4× 100%." &&
            example.Invoke(null, new object[] { 0.5m })?.ToString() == "Frequency damage: 2× threshold 71%; 4× 50%." &&
            example.Invoke(null, new object[] { 2m })?.ToString() == "Frequency damage: 2× threshold 25%; 4× 6%.",
            "Examples explain the applied frequency curve at zero, default and maximum strength");
        Console.WriteLine("Verified Rapid Hits routing, minimal snapshots, exact Apply, draft lifetimes and cached readouts (not live Unity rendering).");
    }
}
