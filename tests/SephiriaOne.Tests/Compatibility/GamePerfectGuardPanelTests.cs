using System.Reflection;
using HarmonyLib;

internal static class GamePerfectGuardPanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Field(CodeInstruction i, string name) => i.operand is FieldInfo f && f.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Perfect Guard panel: " + why); }
        var build = Code("BuildPvpBalanceEditor");
        Require(build.Any(i => i.LoadsConstant("Perfect Guard MP multiplier")) &&
            build.Any(i => i.LoadsConstant("/one friendlyfire perfect-guard")) &&
            build.Any(i => i.LoadsConstant("Native MP cost (x)")) &&
            build.Any(i => i.LoadsConstant("When on, shield MP minimum does not apply.")),
            "An independent toggle and native-cost multiplier explain the shield minimum exception");
        float[] Bounds(string label)
        {
            int index = build.FindIndex(i => i.LoadsConstant(label));
            Require(index >= 0 && build.Skip(index + 1).Take(4).All(i => i.operand is float), "Expected widget bounds: " + label);
            return build.Skip(index + 1).Take(4).Select(i => (float)i.operand).ToArray();
        }
        var master = Bounds("PvP balancing"); var rapid = Bounds("Rapid Hits");
        Require(master[1] == 0 && rapid[1] == 0 && master[0] + master[2] <= rapid[0] && rapid[0] + rapid[2] <= 273,
            "Master toggle and Rapid Hits navigation share the top row without overlap");
        Require(Bounds("Shield MP min (%)")[1] == 34 && Bounds("Perfect Guard MP multiplier")[1] == 70 &&
            Bounds("Native MP cost (x)")[1] == 106 && Bounds("All Damage Amplified limit")[1] == 145,
            "Shield, Perfect Guard and existing amplification retain their allotted rows");
        var hint = Bounds("When on, shield MP minimum does not apply.");
        Require(hint[1] >= 131 && hint[1] + hint[3] <= 145 && Bounds("Bonus %, not C-menu total.")[1] + Bounds("Bonus %, not C-menu total.")[3] <= 250,
            "Explanatory text stays inside the 250-high pane without covering the next control");
        var apply = Code("ApplyFriendlyPerfectGuard");
        Require(apply.Any(i => i.LoadsConstant("/one friendlyfire perfect-guard multiplier ")) &&
            apply.Any(i => Call(i, "TMP_InputField", "get_text")) && apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
            apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyPerfectGuard")),
            "Apply passes exact typed numbers or x-prefixed values through shared validation and field-specific cleanup");
        var refresh = Code("RefreshPvpBalance");
        Require(!Code("EditFriendlyPerfectGuard").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            refresh.Any(i => Field(i, "friendlyPerfectGuardDraft")) &&
            refresh.Any(i => Call(i, "PvpPerfectGuardSettings", "get_Number")) &&
            refresh.Any(i => Call(i, "SettingsSnapshot", "get_CanMutate")) &&
            refresh.Any(i => Call(i, "SettingsSnapshot", "get_FriendlyFireAvailable")) &&
            !refresh.Any(i => Field(i, "friendlyRefill")),
            "Refresh preserves multiplier drafts, applies host availability and has no dependency on the removed refill control");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyPerfectGuard")) &&
            Code("BuildPage").Any(i => Field(i, "friendlyPerfectGuard")) &&
            Code("BuildPage").Any(i => Field(i, "friendlyPerfectGuardDraft")),
            "Session, close, page and language transitions retire the multiplier draft");
        Require(!Code("ClearFriendlyPerfectGuard").Any(i => Field(i, "friendlyGuardMpDraft") || Field(i, "friendlyAmplificationDraft") || Field(i, "friendlyRefillDraft")) &&
            !Code("ClearFriendlyGuardMp").Concat(Code("ClearFriendlyAmplification")).Concat(Code("ClearFriendlyRefill"))
                .Any(i => Field(i, "friendlyPerfectGuardDraft")),
            "Successful Apply clears only its own draft");
        Console.WriteLine("Verified Perfect Guard panel routing, native-cost wording, bounds and draft isolation (not live Unity rendering).");
    }
}
