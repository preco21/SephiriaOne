using System.Reflection;
using HarmonyLib;

internal static class GamePlayTagControlsTests
{
    internal static void Run(Assembly addon)
    {
        List<CodeInstruction> Code(Assembly assembly, string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(assembly.GetType(type, true)!, method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string owner, string method) =>
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Play Tag controls contract: " + why); }
        var build = Code(addon, "SephiriaOne.SettingsPanel", "BuildPlayTagEditor");
        Require(build.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "PanelWidgets" && m.Name == "Input") == 4,
            "Tag exposes independent fuse, rounds, chaser refill and incoming-damage fields");
        Require(build.Any(i => i.LoadsConstant("Refill (x)")) && build.Any(i => i.LoadsConstant("Incoming (%)")) &&
            build.Any(i => i.LoadsConstant("/one tag chaser-refill")) && build.Any(i => i.LoadsConstant("/one tag chaser-damage")),
            "Chaser options have separate shared-command toggles");
        foreach (string name in new[] { "PlayTagDuration", "PlayTagRounds", "PlayTagRefill", "PlayTagIncoming" })
        {
            var edit = Code(addon, "SephiriaOne.SettingsPanel", "Edit" + name);
            var apply = Code(addon, "SephiriaOne.SettingsPanel", "Apply" + name);
            Require(!Calls(edit, "SettingsPanel", "Execute") && Calls(apply, "SettingsPanel", "Execute") &&
                Calls(apply, "TMP_InputField", "get_text") && Calls(apply, "SettingsPanel", "Clear" + name),
                name + " types locally and applies exact text with its own cleanup");
            Require(Calls(Code(addon, "SephiriaOne.SettingsPanel", "ClearInput"), "SettingsPanel", "Clear" + name),
                "Scope and close reset clears " + name + " draft");
        }
        var refreshTag = Code(addon, "SephiriaOne.SettingsPanel", "RefreshPlayTag");
        Require(Calls(refreshTag, "CombatRoleSettings", "get_RefillNumber") && Calls(refreshTag, "CombatRoleSettings", "get_IncomingNumber") &&
            Calls(refreshTag, "TMP_InputField", "SetTextWithoutNotify"), "Tag refresh uses canonical policy values without recursive events");

    }
}
