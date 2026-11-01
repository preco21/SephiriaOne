using System.Reflection;
using HarmonyLib;

internal static class GameEvasionPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction instruction, string type, string name) => instruction.operand is MethodInfo method &&
            method.DeclaringType?.Name == type && method.Name == name;
        void Require(bool condition, string why) { if (!condition) throw new Exception("Evasion panel: " + why); }
        var build = Code("BuildPvpEvasionEditor");
        Require(build.Any(i => i.LoadsConstant("Evasion chance limit")) &&
            build.Any(i => i.LoadsConstant("/one friendlyfire evasion")) &&
            build.Any(i => i.LoadsConstant("Soft chance (%)")) && build.Any(i => i.LoadsConstant("Hard chance (%)")),
            "Controls clearly identify chance units, rather than raw stat points");
        Require(Code("BuildPvpBalanceEditor").Any(i => Call(i, "SettingsPanel", "BuildPvpEvasionEditor")) &&
            Code("RefreshPvpBalance").Any(i => Call(i, "SettingsPanel", "RefreshPvpEvasion")),
            "Evasion uses the existing balancing tab and refresh cycle");
        var apply = Code("ApplyFriendlyEvasion");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire evasion limits ")) &&
            apply.Count(i => Call(i, "TMP_InputField", "get_text")) == 2 && apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
            apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyEvasion")),
            "Apply sends both exact input values through one shared command and clears only its own draft");
        Require(!Code("EditFriendlyEvasion").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            Code("RefreshPvpEvasion").Any(i => i.operand is FieldInfo f && f.Name == "friendlyEvasionDraft") &&
            Code("RefreshPvpEvasion").Any(i => Call(i, "PvpEvasionSettings", "get_SoftNumber")) &&
            Code("RefreshPvpEvasion").Any(i => Call(i, "PvpEvasionSettings", "get_HardNumber")),
            "Typing stays local and refresh preserves the paired draft");
        Require(Code("RefreshPvpEvasion").Any(i => Call(i, "SettingsPanel", "CanEditPvpEvasion")) &&
            build.Any(i => Call(i, "SettingsPanel", "CanEditPvpEvasion")) &&
            !Code("RefreshPvpEvasion").Any(i => Call(i, "SettingsSnapshot", "get_CanMutate")) &&
            Code("CanEditPvpEvasion").Any(i => Call(i, "SettingsSnapshot", "get_HostActive")) &&
            Code("CanEditPvpEvasion").Any(i => Call(i, "SettingsSnapshot", "get_SessionIdentity")),
            "Policy-only checkbox/inputs/reset allow current hosts despite loading guests or unrelated faults");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyEvasion")) &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyEvasionSoft") &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyEvasionHard"),
            "Page, language and scope transitions retire both numeric drafts");
        Console.WriteLine("Verified evasion checkbox, paired Apply, draft isolation and dedicated balancing controls (not live Unity).");
    }
}
