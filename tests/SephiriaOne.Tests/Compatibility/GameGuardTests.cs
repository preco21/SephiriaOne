using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameGuardTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage"), out var generator).ToList();
        var useMp = AccessTools.DeclaredMethod(avatar, "UseMp", new[] { typeof(int) });
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireGuardHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("Guard MP contract: " + why); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        int guardSpend = native.FindIndex(i => i.Calls(useMp));
        int infiniteStat = native.FindIndex(guardSpend + 1, i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "INFINITYMP"));
        int infinityInsertion = infiniteStat + 2;
        int perfectScale = Enumerable.Range(1, guardSpend - 3).Single(i =>
            native[i].opcode == OpCodes.Ldc_R4 && Equals(native[i].operand, 0.5f) && native[i - 1].IsLdloc() &&
            native[i + 1].opcode == OpCodes.Mul && native[i + 2].IsStloc());
        Require(perfectScale > 0 && perfectScale < guardSpend && native[perfectScale - 1].IsLdloc() &&
            native[perfectScale + 1].opcode == OpCodes.Mul && native[perfectScale + 2].IsStloc(),
            "native Perfect Guard halves a local cost before its final modifiers/spend");
        Require(guardSpend > 0 && infiniteStat > guardSpend && Call(native[infiniteStat + 1], "UnitAvatar", "GetCustomStatUnsafe"),
            "guard spend and its native infinite-MP guard-break exemption are identified independently");
        void CheckRewrite(List<CodeInstruction> original)
        {
            var patched = Rewrite(original);
            foreach (string name in new[] { "GuardMpCost", "SpendGuardMp", "GuardInfiniteMp", "MarkPerfectGuard" })
                Require(patched.Count(i => Call(i, "FriendlyFireRuntime", name)) == 1, "exactly one " + name + " guard boundary is installed");
            int exemption = patched.FindIndex(i => Call(i, "FriendlyFireRuntime", "GuardInfiniteMp"));
            int perfect = patched.FindIndex(i => Call(i, "FriendlyFireRuntime", "MarkPerfectGuard"));
            Require(patched[perfect - 1].IsLdloc() && patched[perfect + 1].opcode == OpCodes.Ldc_R4 &&
                Equals(patched[perfect + 1].operand, 0.5f), "Perfect Guard marker stays inside the native conditional half-cost path");
            Require(Call(patched[exemption - 1], "UnitAvatar", "GetCustomStatUnsafe") &&
                patched[exemption - 2].opcode == OpCodes.Ldstr && Equals(patched[exemption - 2].operand, "INFINITYMP"),
                "guard-break exemption reads the native stat before applying the eligible PvP override");
            int cursor = 0;
            for (int i = 0; i < original.Count; i++)
            {
                int entry = cursor;
                if (cursor < patched.Count && (Call(patched[cursor], "FriendlyFireRuntime", "GuardInfiniteMp") ||
                    Call(patched[cursor], "FriendlyFireRuntime", "MarkPerfectGuard"))) cursor++;
                Require(cursor < patched.Count, "guard rewrite retains the entire native instruction stream");
                if (i == guardSpend - 1)
                    Require(original[i].opcode == OpCodes.Conv_I4 && Call(patched[cursor], "FriendlyFireRuntime", "GuardMpCost"),
                        "only the final native guard conversion is scaled");
                else if (i == guardSpend)
                    Require(Call(patched[cursor], "FriendlyFireRuntime", "SpendGuardMp"), "only native guard spending is wrapped in this rewrite");
                else Require(patched[cursor].opcode == original[i].opcode && Equals(patched[cursor].operand, original[i].operand),
                    "native callbacks, modifiers, guard break and all MP-shield instructions remain intact");
                var entries = patched.Skip(entry).Take(cursor - entry + 1).ToArray();
                Require(entries.SelectMany(n => n.labels).SequenceEqual(original[i].labels) &&
                    entries.SelectMany(n => n.blocks).SequenceEqual(original[i].blocks),
                    "branch and exception metadata is preserved without duplication");
                cursor++;
            }
            Require(cursor == patched.Count, "no unrelated guard rewrite operation is introduced");
            Require(patched[exemption].labels.SequenceEqual(original[infinityInsertion].labels) &&
                patched[exemption].blocks.SequenceEqual(original[infinityInsertion].blocks),
                "every entry into the native guard-break comparison first receives the PvP infinite-MP adjustment");
        }
        CheckRewrite(native);
        var labeled = native.Select(i => new CodeInstruction(i)).ToList();
        foreach (int boundary in new[] { guardSpend - 1, guardSpend, infinityInsertion, perfectScale })
        {
            labeled[boundary].labels.Add(generator.DefineLabel());
            labeled[boundary].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        }
        CheckRewrite(labeled);
        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (InvalidOperationException) { rejected = true; }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        Reject(native.Where((_, index) => index != guardSpend), "missing guard spend cannot wrap a later MP shield spend");
        Reject(native.Where(i => i.operand is not FieldInfo f || f.Name != "OnCalculateDurabilityDamage"), "changed cost callback contract rejects installation");
        var changed = native.Select(i => new CodeInstruction(i)).ToList();
        changed[guardSpend - 1].opcode = OpCodes.Conv_I8;
        Reject(changed, "changed integer conversion is rejected");
        Reject(native.Select(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 10f) ? new CodeInstruction(OpCodes.Ldc_R4, 11f) : i), "native base-cost change requires reaudit");
        Reject(native.Select(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 0.5f) ? new CodeInstruction(OpCodes.Ldc_R4, 0.25f) : i), "native perfect-guard change requires reaudit");
        var wrongPerfect = native.Select(i => new CodeInstruction(i)).ToList();
        wrongPerfect[perfectScale + 1].opcode = OpCodes.Add;
        Reject(wrongPerfect, "changed Perfect Guard arithmetic cannot tag an unrelated branch");
        wrongPerfect = native.Select(i => new CodeInstruction(i)).ToList();
        wrongPerfect[perfectScale + 2] = new CodeInstruction(OpCodes.Stloc, 65535);
        Reject(wrongPerfect, "Perfect Guard must update the exact local passed to final native MP spending");
        Reject(native.Where((_, index) => index != infiniteStat), "missing guard-break infinite-MP read requires reaudit");
        Reject(Rewrite(native), "guard minimum cannot be inserted twice into the same hit stream");
        Require(native.Skip(guardSpend).Take(25).Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "INFINITYMP")) &&
            native.Skip(guardSpend).Any(i => Call(i, "UnitAvatar", "InactivateGuard")), "native infinite-MP and guard-break tail remains in use");
        var spendCode = PatchProcessor.GetOriginalInstructions(useMp).ToList();
        Require(spendCode.Any(i => Call(i, "UnitAvatar", "set_Networkmp")) &&
            spendCode.Any(i => i.operand is FieldInfo f && f.Name == "OnMpUsedServerside"), "native MP callback and stock replication remain authoritative");
        var nativeMaximum = PatchProcessor.GetOriginalInstructions(AccessTools.PropertyGetter(avatar, "MaxMp")).ToList();
        int multiply = nativeMaximum.FindIndex(i => i.opcode == OpCodes.Mul);
        Require(nativeMaximum.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "INFINITYMP")) &&
            nativeMaximum.Any(i => Call(i, "KeywordDatabase", "GetConstValue")) &&
            nativeMaximum.Any(i => i.LoadsField(AccessTools.DeclaredField(avatar, "maxMp"))) &&
            nativeMaximum.Any(i => i.LoadsConstant(Convert.ToInt32(Enum.Parse(game.GetType("ECustomStat", true)!, "FinalMP")))) &&
            multiply >= 0 && nativeMaximum[multiply + 1].opcode == OpCodes.Conv_R4,
            "guard maximum mirrors native FinalMP/base-MP inputs and the separate infinite-MP constant");
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var safeMaximum = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(runtime, "GuardMaximumMp")).ToList();
        Require(safeMaximum.Any(i => i.opcode == OpCodes.Conv_I8) &&
            !safeMaximum.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld),
            "overflow-safe guard maximum widens arithmetic without changing native stats");

        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Panel(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        var build = Panel("BuildPvpBalanceEditor"); var refresh = Panel("RefreshPvpBalance"); var apply = Panel("ApplyFriendlyGuardMp");
        Require(build.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Shield MP min (%)")) &&
            build.Count(i => Call(i, "PanelWidgets", "Input")) == 6, "dedicated balancing section includes shield minimum, Perfect Guard and paired amplification/critical inputs");
        Require(apply.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "/one friendlyfire guard-mp ")) &&
            apply.Any(i => Call(i, "SettingsPanel", "Execute")) && apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyGuardMp")),
            "guard Apply uses shared command and clears only its own draft");
        Require(!Panel("EditFriendlyGuardMp").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            refresh.Any(i => i.operand is FieldInfo f && f.Name == "friendlyGuardMpDraft") &&
            refresh.Any(i => Call(i, "FriendlyFireSettings", "get_GuardMpNumber")), "typing remains local and refresh preserves canonical draft state");
        Require(Panel("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyGuardMp")) &&
            Panel("BuildPage").Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "friendlyGuardMp"),
            "scope/page/language transitions clear old guard field state");
        Require(Panel("Refresh").Any(i => Call(i, "SettingsPanel", "RefreshPvpBalance")) &&
            Panel("BuildCombatEditor").Any(i => Call(i, "SettingsPanel", "BuildParticipantsEditor")), "balancing refresh and shared participant controls remain wired");
        GameAmplificationPanelTests.Run(addon);
        GameActiveDefenseTests.Run(game, addon);
        Console.WriteLine("Verified strict guard floor/spend/exemption boundaries, native callbacks/break/replication, drift rejection and dedicated shared-command balancing controls (not live Unity).");
    }
}
