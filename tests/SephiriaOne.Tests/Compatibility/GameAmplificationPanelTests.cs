using System.Reflection;
using HarmonyLib;

internal static class GameAmplificationPanelTests
{
    internal static void Run(Assembly addon)
    {
        GameElementalPanelTests.Run(addon);
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction instruction, string type, string name) => instruction.operand is MethodInfo method &&
            method.DeclaringType?.Name == type && method.Name == name;
        void Require(bool condition, string why) { if (!condition) throw new Exception("Amplification panel: " + why); }
        var build = Code("BuildPvpBalanceEditor");
        Require(build.Any(i => i.LoadsConstant("All Damage Amplified limit")) &&
            build.Any(i => i.LoadsConstant("/one friendlyfire amplification")) &&
            build.Any(i => i.LoadsConstant("Soft bonus (%)")) && build.Any(i => i.LoadsConstant("Hard bonus (%)")) &&
            build.Any(i => i.LoadsConstant("Bonus %, not C-menu total.")),
            "Controls identify bonus units and the required balancing toggle");
        Require(build.Any(i => Call(i, "PanelWidgets", "ScrollSection")) &&
            build.Any(i => Call(i, "SettingsPanel", "BuildPvpLowerBound")) &&
            build.Any(i => Call(i, "SettingsPanel", "BuildPvpDotEditor")),
            "General balancing scrolls through assistance controls beside the full dedicated debuff editor");
        var apply = Code("ApplyFriendlyAmplification");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire amplification limits ")) &&
            apply.Count(i => Call(i, "TMP_InputField", "get_text")) == 2 && apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
            apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyAmplification")),
            "Apply sends both exact input values through one shared command and clears only its own draft");
        Require(!Code("EditFriendlyAmplification").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            Code("RefreshPvpBalance").Any(i => i.operand is FieldInfo f && f.Name == "friendlyAmplificationDraft") &&
            Code("RefreshPvpBalance").Any(i => Call(i, "PvpAmplificationSettings", "get_SoftNumber")) &&
            Code("RefreshPvpBalance").Any(i => Call(i, "PvpAmplificationSettings", "get_HardNumber")),
            "Typing stays local and refresh preserves the paired draft");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyAmplification")) &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyAmplificationSoft") &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyAmplificationHard"),
            "Page, language and scope transitions retire both numeric drafts");
        Console.WriteLine("Verified amplification checkbox, paired Apply, draft isolation and dedicated balancing controls (not live Unity).");
    }
}
