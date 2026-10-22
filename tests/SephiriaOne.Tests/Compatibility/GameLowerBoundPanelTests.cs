using System.Reflection;
using HarmonyLib;

internal static class GameLowerBoundPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Field(CodeInstruction i, string name) => i.operand is FieldInfo f && f.Name == name;
        void Require(bool ok, string why) { if (!ok) throw new Exception("Lower-bound panel: " + why); }
        var build = Code("BuildPvpLowerBound");
        Require(build.Any(i => i.LoadsConstant("Weak-hit assistance")) && build.Any(i => i.LoadsConstant("/one friendlyfire lower-bound")) &&
            build.Any(i => i.LoadsConstant("Reference (% opponent max HP)")) && build.Any(i => i.LoadsConstant("Bonus refill (% max HP/s)")) &&
            build.Any(i => i.LoadsConstant("Weak hits use one small bonus pool per opponent across all sources. Role reductions, shields and the main allowance still apply.")),
            "Controls explain opponent-relative tuning, shared bonus and remaining limits");
        foreach (string field in new[] { "Reference", "Refill" })
        {
            string lower = field.ToLowerInvariant();
            var apply = Code("ApplyFriendlyLower" + field);
            Require(apply.Any(i => i.LoadsConstant("/one friendlyfire lower-bound " + lower + " ")) &&
                apply.Any(i => Call(i, "TMP_InputField", "get_text")) && apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
                apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyLower" + field)), "Apply routes exact typed " + lower + " through shared validation");
            Require(Code("ResetFriendlyLower" + field).Any(i => i.LoadsConstant("/one friendlyfire lower-bound " + lower + " reset")),
                "Numeric reset changes only " + lower);
            Require(Code("RefreshPvpLowerBound").Any(i => Field(i, "friendlyLower" + field + "Draft")) &&
                Code("BuildPage").Any(i => Field(i, "friendlyLower" + field + "Draft")), "Draft survives refresh and retires on page/session transitions");
            string sibling = field == "Reference" ? "Refill" : "Reference";
            Require(!Code("ClearFriendlyLower" + field).Any(i => Field(i, "friendlyLower" + sibling + "Draft")), "Successful Apply preserves sibling draft");
        }
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyLowerBound")) &&
            Code("ResetFriendlyLowerBound").Any(i => i.LoadsConstant("/one friendlyfire lower-bound reset")) &&
            Code("RefreshPvpBalance").Any(i => Call(i, "SettingsPanel", "RefreshPvpLowerBound")), "Family reset and lifecycle cleanup use the shared control path");
        Console.WriteLine("Verified lower-bound panel commands, shared-pool wording and draft isolation (not live Unity rendering).");
    }
}
