using System.Reflection;
using HarmonyLib;

internal static class GameElementalPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction instruction, string type, string name) => instruction.operand is MethodInfo method &&
            method.DeclaringType?.Name == type && method.Name == name;
        void Require(bool condition, string why) { if (!condition) throw new Exception("Elemental panel: " + why); }
        var build = Code("BuildPvpElementalEditor");
        Require(build.Any(i => i.LoadsConstant("Elemental stat limits")) && build.Any(i => i.LoadsConstant("/one friendlyfire elements")) &&
            build.Any(i => i.LoadsConstant("Physical")) && build.Any(i => i.LoadsConstant("Flame")) &&
            build.Any(i => i.LoadsConstant("Ice")) && build.Any(i => i.LoadsConstant("Lightning")) &&
            build.Any(i => i.LoadsConstant("Soft limit")) && build.Any(i => i.LoadsConstant("Hard limit")),
            "One master checkbox and four named soft/hard pairs are available");
        Require(build.Count(i => Call(i, "PanelWidgets", "Input")) == 2 &&
            build.Any(i => i.LoadsConstant("Reset")) && Code("BuildPvpBalanceEditor").Any(i => Call(i, "SettingsPanel", "BuildPvpElementalEditor")),
            "Each loop iteration creates a pair and reset inside the existing scroll");
        var apply = Code("ApplyFriendlyElement");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire elements ")) && apply.Any(i => i.LoadsConstant(" limits ")) &&
            apply.Count(i => Call(i, "TMP_InputField", "get_text")) == 2 && apply.Any(i => Call(i, "SettingsPanel", "Execute")),
            "One shared command atomically applies both fields for the chosen element");
        Require(Code("RefreshPvpElements").Any(i => i.operand is FieldInfo f && f.Name == "friendlyElementDraft") &&
            Code("RefreshPvpElements").Any(i => Call(i, "PvpElementalSettings", "GetLimit")) &&
            Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyElements")) &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyElementSoft"),
            "Independent drafts survive refresh and are retired on scope or page transitions");
        Require(build.Any(i => i.LoadsConstant("Effective participant stats during balanced PvP; also affects their PvE damage. Saved stats unchanged.")),
            "The panel discloses participant PvE impact and saved-stat preservation");
        Console.WriteLine("Verified elemental master toggle, independent pairs, reset, atomic Apply and draft lifecycle (not live Unity).");
    }
}
