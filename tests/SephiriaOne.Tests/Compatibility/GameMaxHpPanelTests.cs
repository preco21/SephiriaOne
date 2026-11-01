using System.Reflection;
using HarmonyLib;

internal static class GameMaxHpPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name)
        {
            var method = AccessTools.DeclaredMethod(panel, name)
                ?? throw new Exception("Max HP panel: required control method missing: " + name);
            return PatchProcessor.GetOriginalInstructions(method).ToList();
        }
        bool Call(CodeInstruction instruction, string type, string name) => instruction.operand is MethodInfo method &&
            method.DeclaringType?.Name == type && method.Name == name;
        void Require(bool condition, string why) { if (!condition) throw new Exception("Max HP panel: " + why); }

        var build = Code("BuildPvpMaxHpEditor");
        Require(build.Any(i => i.LoadsConstant("Max HP normalization")) &&
            build.Any(i => i.LoadsConstant("/one friendlyfire max-hp")) &&
            build.Any(i => i.LoadsConstant("Baseline (HP)")) &&
            build.Any(i => i.LoadsConstant("Soft +HP")) && build.Any(i => i.LoadsConstant("Hard +HP")) &&
            build.Count(i => Call(i, "PanelWidgets", "Input")) == 3,
            "One checkbox and three inputs clearly distinguish baseline HP from additional HP");
        Require(Code("BuildPvpBalanceEditor").Any(i => Call(i, "SettingsPanel", "BuildPvpMaxHpEditor")) &&
            Code("RefreshPvpBalance").Any(i => Call(i, "SettingsPanel", "RefreshPvpMaxHp")),
            "Max HP belongs to the existing balancing page and refresh cycle");
        var apply = Code("ApplyFriendlyMaxHp");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire max-hp limits ")) &&
            apply.Count(i => Call(i, "TMP_InputField", "get_text")) == 3 &&
            apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
            apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyMaxHp")),
            "Apply submits all three exact draft values atomically through the shared command");
        var reset = Code("ResetFriendlyMaxHp");
        Require(reset.Any(i => i.LoadsConstant("/one friendlyfire max-hp reset")) &&
            reset.Any(i => Call(i, "SettingsPanel", "ClearFriendlyMaxHp")),
            "Successful reset discards unapplied Max HP values");
        var refresh = Code("RefreshPvpMaxHp");
        Require(refresh.Any(i => i.operand is FieldInfo f && f.Name == "friendlyMaxHpDraft") &&
            refresh.Any(i => Call(i, "PvpMaxHpSettings", "get_BaselineNumber")) &&
            refresh.Any(i => Call(i, "PvpMaxHpSettings", "get_SoftNumber")) &&
            refresh.Any(i => Call(i, "PvpMaxHpSettings", "get_HardNumber")) &&
            !Code("EditFriendlyMaxHp").Any(i => Call(i, "SettingsPanel", "Execute")),
            "Typing stays local and refresh preserves the complete draft");
        Require(refresh.Any(i => Call(i, "SettingsSnapshot", "get_CanMutate")) &&
            refresh.Any(i => Call(i, "SettingsSnapshot", "get_MaxHpAvailable")) &&
            refresh.Any(i => Call(i, "SettingsSnapshot", "get_FriendlyFireAvailable")) &&
            refresh.Any(i => i.operand is FieldInfo f && f.Name == "friendlyMaxHpReset"),
            "Inputs and Apply obey host and independent compatibility gates while reset remains recoverable");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyMaxHp")) &&
            Code("BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "friendlyMaxHpBaseline"),
            "Closing, scope, language and page changes retire all Max HP drafts");

        var snapshot = addon.GetType("SephiriaOne.SettingsSnapshot", true)!;
        var constructor = snapshot.GetConstructors().Single();
        var optional = constructor.GetParameters().Last();
        Require(optional.Name == "maxHpAvailable" && optional.IsOptional && Equals(optional.DefaultValue, false),
            "Old snapshot callers retain a safe false availability default");
        var capture = addon.GetType("SephiriaOne.SessionSettings", true)!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "ReadSnapshot" && m.GetParameters()[0].ParameterType.Name == "SnapshotContent");
        Require(PatchProcessor.GetOriginalInstructions(capture).Any(i => Call(i, "PvpMaxHpFeature", "get_Available")),
            "Snapshot capture reads the separate Max HP feature gate");
        Console.WriteLine("Verified Max HP controls, atomic Apply/reset, draft lifetime and separate compatibility gate (not live Unity).");
    }
}
