using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameGuardTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage")).ToList();
        var useMp = AccessTools.DeclaredMethod(avatar, "UseMp", new[] { typeof(int) });
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireGuardHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("Guard MP contract: " + why); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        var patched = Rewrite(native);
        Require(patched.Count == native.Count, "guard conversion rewrite adds no calls or branches outside its one replacement");
        int converted = 0;
        for (int i = 0; i < native.Count; i++)
        {
            bool helper = Call(patched[i], "FriendlyFireRuntime", "GuardMpCost");
            if (helper)
            {
                converted++;
                Require(native[i].opcode == OpCodes.Conv_I4 && native[i + 1].Calls(useMp), "only conversion immediately before native guard UseMp changes");
            }
            else Require(patched[i].opcode == native[i].opcode && Equals(patched[i].operand, native[i].operand), "native guard callbacks/modifiers/break and MP shield instructions preserved");
            Require(patched[i].labels.SequenceEqual(native[i].labels) && patched[i].blocks.SequenceEqual(native[i].blocks), "branch and exception entry points preserved");
        }
        Require(converted == 1, "exactly one guard cost replaced");
        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (InvalidOperationException) { rejected = true; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        int guardSpend = native.FindIndex(i => i.Calls(useMp));
        Reject(native.Where((_, index) => index != guardSpend), "missing guard spend cannot wrap a later MP shield spend");
        Reject(native.Where(i => i.operand is not FieldInfo f || f.Name != "OnCalculateDurabilityDamage"), "changed cost callback contract rejects installation");
        var changed = native.Select(i => new CodeInstruction(i)).ToList();
        changed[guardSpend - 1].opcode = OpCodes.Conv_I8;
        Reject(changed, "changed integer conversion is rejected");
        Reject(native.Select(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 10f) ? new CodeInstruction(OpCodes.Ldc_R4, 11f) : i), "native base-cost change requires reaudit");
        Reject(native.Select(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 0.5f) ? new CodeInstruction(OpCodes.Ldc_R4, 0.25f) : i), "native perfect-guard change requires reaudit");
        Require(native.Skip(guardSpend).Take(25).Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "INFINITYMP")) &&
            native.Skip(guardSpend).Any(i => Call(i, "UnitAvatar", "InactivateGuard")), "native infinite-MP and guard-break tail remains in use");
        var spendCode = PatchProcessor.GetOriginalInstructions(useMp).ToList();
        Require(spendCode.Any(i => Call(i, "UnitAvatar", "set_Networkmp")) &&
            spendCode.Any(i => i.operand is FieldInfo f && f.Name == "OnMpUsedServerside"), "native MP callback and stock replication remain authoritative");

        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Panel(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        var build = Panel("BuildPvpBalanceEditor"); var refresh = Panel("RefreshPvpBalance"); var apply = Panel("ApplyFriendlyGuardMp");
        Require(build.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Guard MP (%)")) &&
            build.Count(i => Call(i, "PanelWidgets", "Input")) == 2, "dedicated balancing section includes guard and refill inputs");
        Require(apply.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "/one friendlyfire guard-mp ")) &&
            apply.Any(i => Call(i, "SettingsPanel", "Execute")) && apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyGuardMp")),
            "guard Apply uses shared command and clears only its own draft");
        Require(!Panel("EditFriendlyGuardMp").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            refresh.Any(i => i.operand is FieldInfo f && f.Name == "friendlyGuardMpDraft") &&
            refresh.Any(i => Call(i, "FriendlyFireSettings", "get_GuardMpNumber")), "typing remains local and refresh preserves canonical draft state");
        Require(Panel("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyGuardMp")) &&
            Panel("BuildPage").Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "friendlyGuardMp"),
            "scope/page/language transitions clear old guard field state");
        Require(Panel("RefreshCombat").Any(i => Call(i, "SettingsPanel", "RefreshPvpBalance")) &&
            Panel("BuildCombatEditor").Any(i => Call(i, "SettingsPanel", "BuildParticipantsEditor")), "balancing refresh and shared participant controls remain wired");
        Console.WriteLine("Verified guard-only final MP conversion, native modifiers/break/replication, drift rejection and compact shared-command balancing controls (not live Unity).");
    }
}
