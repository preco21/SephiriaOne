using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameActiveDefenseTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage"), out var generator).ToList();
        var spend = AccessTools.DeclaredMethod(avatar, "UseMp", new[] { typeof(int) });
        var useShield = AccessTools.DeclaredMethod(avatar, "UseShield", new[] { typeof(float) });
        var parry = AccessTools.DeclaredField(avatar, "parryInvincibleApplied");
        var callback = AccessTools.DeclaredField(avatar, "OnParry");
        var timer = AccessTools.DeclaredField(avatar, "parrySuccessBonusTimer");
        var hooks = addon.GetType("SephiriaOne.FriendlyFireActiveDefenseHooks", true)!;
        var rewrite = AccessTools.DeclaredMethod(hooks, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> input) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { input })!).ToList();
        bool Helper(CodeInstruction i, string name) => Call(i, "FriendlyFireRuntime", name);
        bool Insertion(CodeInstruction i) => Helper(i, "ParryAllowed") || Helper(i, "BeginParryMp") || Helper(i, "AfterParryMp");
        int parryRead = native.FindIndex(i => i.LoadsField(parry));
        int callbackRead = native.FindIndex(i => i.LoadsField(callback));
        int timerRead = native.FindIndex(i => i.LoadsField(timer));
        int fullSpend = native.FindLastIndex(i => i.Calls(spend));
        int partialSpend = native.FindLastIndex(fullSpend - 1, i => i.Calls(spend));
        int ordinaryShield = native.FindIndex(i => i.Calls(useShield));
        Require(parryRead >= 0 && callbackRead > parryRead && timerRead > callbackRead &&
            ordinaryShield > timerRead && ordinaryShield < partialSpend &&
            native[fullSpend - 1].opcode == OpCodes.Conv_I4 && native[partialSpend - 1].LoadsField(AccessTools.DeclaredField(avatar, "mp")),
            "native parry callback, ordinary shield and distinct partial/full MP absorption boundaries are present");

        void CheckRewrite(List<CodeInstruction> original)
        {
            var patched = Rewrite(original);
            foreach (string name in new[] { "ParryAllowed", "BeginParryMp", "AfterParryMp", "ActiveMpShieldCost", "SpendActiveShieldMp", "SpendPartialShieldMp", "AbsorbShield" })
                Require(patched.Count(i => Helper(i, name)) == 1, "exactly one " + name + " boundary is installed");
            int allowed = patched.FindIndex(i => Helper(i, "ParryAllowed"));
            int begin = patched.FindIndex(i => Helper(i, "BeginParryMp"));
            int after = patched.FindIndex(i => Helper(i, "AfterParryMp"));
            int eventRead = patched.FindIndex(i => i.LoadsField(callback));
            int successTimer = patched.FindIndex(i => i.LoadsField(timer));
            int cost = patched.FindIndex(i => Helper(i, "ActiveMpShieldCost"));
            int partial = patched.FindIndex(i => Helper(i, "SpendPartialShieldMp"));
            int ordinary = patched.FindIndex(i => Helper(i, "AbsorbShield"));
            Require(patched[allowed - 1].LoadsField(parry) && patched[allowed + 1].LoadsConstant(0),
                "parry eligibility adapts only the native parry counter before its positive-count branch");
            Require(allowed < begin && begin < eventRead && eventRead < after && after < successTimer &&
                patched.Skip(eventRead).Take(after - eventRead).Any(i => i.operand is MethodInfo m && m.Name == "Invoke"),
                "MP capture and settlement surround the native parry callback before bonus invulnerability");
            Require(Helper(patched[cost + 1], "SpendActiveShieldMp") && patched[cost - 1].IsLdloc(),
                "full MP absorption adapts resolved damage and its adjacent native spend");
            Require(ordinary < partial && partial < cost && patched[partial - 1].LoadsField(AccessTools.DeclaredField(avatar, "mp")) &&
                patched[ordinary - 1].IsLdloc() && patched[ordinary + 1].IsStloc(),
                "ordinary shield absorption and native partial MP spending retain their original operands and order");
            Signature(patched[allowed], typeof(int), typeof(int));
            Signature(patched[begin], typeof(void));
            Signature(patched[after], typeof(void));
            Signature(patched[cost], typeof(int), typeof(float));
            Signature(patched[cost + 1], typeof(void), avatar, typeof(int));
            Signature(patched[partial], typeof(void), avatar, typeof(int));
            Signature(patched[ordinary], typeof(float), avatar, typeof(float));

            // Map by meaning, not a total IL count: adapters may move entry metadata,
            // but every native instruction must survive except these four replacements.
            int cursor = 0;
            for (int n = 0; n < original.Count; n++)
            {
                int entry = cursor;
                while (cursor < patched.Count && Insertion(patched[cursor])) cursor++;
                Require(cursor < patched.Count, "rewriter retains the complete native instruction stream");
                var mapped = patched[cursor];
                if (n == fullSpend - 1)
                    Require(Helper(mapped, "ActiveMpShieldCost"), "only the full shield conversion changes");
                else if (n == fullSpend)
                    Require(Helper(mapped, "SpendActiveShieldMp"), "only the full shield spend changes");
                else if (n == partialSpend)
                    Require(Helper(mapped, "SpendPartialShieldMp"), "partial shield spending uses its own accounting wrapper");
                else if (n == ordinaryShield)
                    Require(Helper(mapped, "AbsorbShield"), "ordinary shield use preserves its native returned damage remainder");
                else
                    Require(mapped.opcode == original[n].opcode && Equals(mapped.operand, original[n].operand),
                        "native callbacks, guards, counters, dodges, partial spill and damage accounting remain intact");
                var entries = patched.Skip(entry).Take(cursor - entry + 1).ToArray();
                Require(entries.SelectMany(i => i.labels).SequenceEqual(original[n].labels) &&
                    entries.SelectMany(i => i.blocks).SequenceEqual(original[n].blocks),
                    "branch and exception entry metadata reaches the complete adapted operation without duplication");
                cursor++;
            }
            Require(cursor == patched.Count, "no extra unclassified instruction or trailing operation is introduced");

            // The compiler emits a separate null-delegate branch to the common join.
            // Charging immediately after Invoke alone would silently miss that branch.
            var nullSkips = original.Skip(callbackRead).Take(timerRead - callbackRead)
                .Where(i => (i.opcode == OpCodes.Br || i.opcode == OpCodes.Br_S) && i.operand is Label).ToArray();
            Require(nullSkips.Length == 1, "native null-parry-callback continuation remains unambiguous");
            int nullTarget = patched.FindIndex(i => i.labels.Contains((Label)nullSkips[0].operand));
            Require(nullTarget > eventRead && nullTarget <= after,
                "both present and absent OnParry delegates flow through MP settlement");
            Require(patched.Count(i => i.Calls(spend)) == native.Count(i => i.Calls(spend)) - 2 &&
                !patched.Any(i => i.Calls(useShield)),
                "only the ordinary guard spend remains direct in this rewrite; shield calls use their dedicated wrappers");
        }
        CheckRewrite(native);
        var labeled = native.Select(i => new CodeInstruction(i)).ToList();
        int[] boundaries = { parryRead + 1, callbackRead - 1, timerRead - 1, ordinaryShield, partialSpend, fullSpend - 1, fullSpend };
        foreach (int boundary in boundaries)
        {
            labeled[boundary].labels.Add(generator.DefineLabel());
            labeled[boundary].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        }
        CheckRewrite(labeled);
        Require(labeled.Sum(i => i.labels.Count) == native.Sum(i => i.labels.Count) + boundaries.Length &&
            labeled.Sum(i => i.blocks.Count) == native.Sum(i => i.blocks.Count) + boundaries.Length,
            "rewriting never mutates the caller's instruction metadata");

        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (InvalidOperationException) { rejected = true; }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        void RejectAt(int index, CodeInstruction replacement, string why)
        {
            var changed = native.Select(i => new CodeInstruction(i)).ToList();
            changed[index] = replacement;
            Reject(changed, why);
        }
        Reject(native.Where(i => !i.LoadsField(parry)), "missing parry counter requires a fresh audit");
        Reject(native.Concat(new[] { native[parryRead] }), "duplicate parry counters cannot be patched ambiguously");
        Reject(native.Where(i => !i.LoadsField(callback)), "missing parry callback cannot be treated as another defensive branch");
        Reject(native.Where(i => !i.LoadsField(timer)), "missing common parry continuation rejects installation");
        RejectAt(fullSpend - 1, new(OpCodes.Conv_I8), "changed MP-shield conversion rejects installation");
        RejectAt(fullSpend, new(OpCodes.Nop), "missing full-absorption spend cannot redirect to partial absorption");
        RejectAt(partialSpend, new(OpCodes.Nop), "missing partial-absorption spend requires a fresh accounting audit");
        RejectAt(ordinaryShield, new(OpCodes.Nop), "missing ordinary shield absorption requires a fresh accounting audit");
        Reject(native.Concat(new[] { native[ordinaryShield] }), "duplicate ordinary shield consumption cannot be patched ambiguously");
        Reject(native.Where(i => !Text(i, "MPSHIELD")), "missing MP-shield stat boundary rejects installation");
        Reject(Rewrite(native), "the same hit stream cannot receive active-defense hooks twice");

        NativeDefenseProducers(game);
        var pipeline = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireTranspiler", true)!, "Rewrite");
        var combined = ((IEnumerable<CodeInstruction>)pipeline.Invoke(null, new object[] { native, generator })!).ToList();
        foreach (string name in new[] { "ParryAllowed", "BeginParryMp", "AfterParryMp", "ActiveMpShieldCost", "SpendActiveShieldMp", "SpendPartialShieldMp", "AbsorbShield", "GuardMpCost", "Scale", "Sanitize" })
            Require(combined.Count(i => Helper(i, name)) == 1, "combined damage pipeline preserves exactly one " + name + " boundary");
        var install = AccessTools.DeclaredMethod(hooks, "Install", new[] { typeof(Harmony) });
        Require(install != null && install.IsStatic, "subshield success hooks use the shared Harmony installation");
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var absorb = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(runtime, "AbsorbShield")).ToList();
        Require(absorb.Count(i => i.Calls(useShield)) == 1 &&
            !absorb.Any(i => Call(i, "UnitAvatar", "ApplyDamage") || Call(i, "UnitAvatar", "set_Networkhp") ||
                i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType == avatar && f.Name == "hp"),
            "ordinary shield wrapper delegates absorption once and does not create extra HP damage");
        ShieldSettlement(game, addon, hooks);
        Console.WriteLine("Verified MP/ordinary shield and parry boundaries, callback/null-path accounting, native IL/metadata preservation, drift rejection and passive/dash defensive producers (not live Unity).");
    }

    private static void NativeDefenseProducers(Assembly game)
    {
        List<CodeInstruction> Code(string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType(type, true)!, method)).ToList();
        bool Spends(IEnumerable<CodeInstruction> code) => code.Any(i => Call(i, "UnitAvatar", "UseMp"));
        var active = game.GetType("Charm_MPShieldActive", true)!;
        Require(active.BaseType?.Name == "Charm_Active" && AccessTools.DeclaredField(active, "isShieldActive")?.FieldType == typeof(bool),
            "active shield has explicit live state separate from the generic MPSHIELD stat");
        var activate = Code("Charm_MPShieldActive", "ActivateShield");
        var deactivate = Code("Charm_MPShieldActive", "DeactivateShield");
        Require(Code("Charm_MPShieldActive", "FireCastingServer").Any(i => Call(i, "Charm_MPShieldActive", "ActivateShield")) &&
            activate.Any(i => Text(i, "MPSHIELD")) && activate.Any(i => Call(i, "UnitAvatar", "AddCustomStatUnsafe")) &&
            activate.Any(i => Call(i, "Charm_MPShieldActive", "set_NetworkisShieldActive")) && !Spends(activate),
            "active shield activation enables MP absorption without charging a startup spell cost");
        Require(deactivate.Any(i => Text(i, "MPSHIELD")) && deactivate.Any(i => i.LoadsConstant(-1)) &&
            deactivate.Any(i => Call(i, "UnitAvatar", "AddCustomStatUnsafe")) &&
            Code("Charm_MPShieldActive", "OnDisabledEffect").Any(i => Call(i, "Charm_MPShieldActive", "DeactivateShield")) &&
            Code("Charm_MPShieldActive", "Update").Any(i => Call(i, "Charm_MPShieldActive", "DeactivateShield")),
            "expiry and disabled effects remove the active contribution while the shared MP-shield branch also accepts passive sources");

        foreach (var producer in new[]
        {
            ("WeaponControllerSimple", "StartGuard", "UnitAvatar", "ActivateGuard"),
            ("WeaponControllerSimple", "StartGuardCustomAngle", "UnitAvatar", "ActivateGuard"),
            ("WeaponSimple_SwordAndShield", "OnServerReceivedMessage", "WeaponControllerSimple", "StartGuard"),
            ("WeaponSimple_QuartterStaff", "StartSpecialAttackAnimation", "WeaponControllerSimple", "StartGuardCustomAngle"),
            ("WeaponSimple_QuartterStaff", "StartDashAttackAnimation", "WeaponControllerSimple", "StartGuardCustomAngle"),
            ("WeaponAddon_OppositeAttackGuard", "StartOppositeGuard", "UnitAvatar", "ActivateGuard"),
            ("WeaponSkillSubAction_Block", "BeginSubAction", "UnitAvatar", "ActivateGuard"),
            ("WeaponSkill_Blocking", "BeginWeaponSkillAnimation", "UnitAvatar", "ActivateGuard"),
            ("Weapon_SwordAndShield_AttackShield", "BeginAttackAnimation", "UnitAvatar", "ActivateGuard"),
            ("Weapon_SwordAndShield_BackShield", "BeginAttackAnimation", "UnitAvatar", "ActivateGuard")
        })
            Require(Code(producer.Item1, producer.Item2).Any(i => Call(i, producer.Item3, producer.Item4)),
                producer.Item1 + "." + producer.Item2 + " still converges on the shared native guard branch");
        Require(Spends(Code("WeaponSimple_QuartterStaff", "StartSpecialAttackAnimation")) &&
            Code("WeaponSimple_QuartterStaff", "get_SpecialAttackCost").Any(i => i.LoadsConstant(5)),
            "staff's separate startup attack cost is retained in addition to per-hit guarding");
        var dagger = Code("WeaponSimple_Dagger", "StartSpecialAttackAnimation");
        var refund = Code("WeaponSimple_Dagger", "HandleParry");
        Require(Spends(dagger) && dagger.Any(i => Call(i, "WeaponSimple_Dagger", "get_ParryCost")) &&
            refund.Any(i => Call(i, "WeaponSimple_Dagger", "get_ParryCost")) && refund.Any(i => Call(i, "UnitAvatar", "HealMp")) &&
            refund.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "costPaid"),
            "dagger pays on activation and may refund MP inside the successful parry callback");
        var katana = Code("WeaponSimple_Katana", "StartSpecialAttackAnimation");
        Require(Spends(katana) && katana.Any(i => i.LoadsConstant(9999)) &&
            katana.Any(i => Call(i, "WeaponControllerSimple", "StartParryInvincible")) &&
            katana.Any(i => Call(i, "WeaponControllerSimple", "StartFuryInvincible")),
            "katana's paid deflection and distinct offensive counter invincibility remain distinguishable");
        Require(Code("WeaponControlAnimator", "StartDodgeInvincible_Parry").Any(i => Call(i, "WeaponControllerSimple", "StartParryInvincible")) &&
            Code("WeaponControllerSimple", "StartParryInvincible").Any(i => Call(i, "UnitAvatar", "StartDodgeInvincible")),
            "animation parries still enter the native parry counter");
        Require(!Spends(Code("WeaponSimple_Katana", "OnServerReceivedMessage")) &&
            Code("WeaponSimple_Katana", "OnServerReceivedMessage").Any(i => Call(i, "WeaponControllerSimple", "StartFuryInvincible")),
            "free sheath counter windows remain distinct from paid deflection");
        var greatsword = Code("WeaponSimple_GreatSword", "SubAttackButtonUp");
        Require(Spends(greatsword) && !greatsword.Any(i => Call(i, "WeaponControllerSimple", "StartParryInvincible") || Call(i, "UnitAvatar", "ActivateGuard")),
            "greatsword's direct sweep/transform spend is not mistaken for a per-hit defensive cost");

        var subshield = Code("UnitSubShield", "OnCalculateDamage");
        Require(!Spends(subshield) && subshield.Any(i => Call(i, "UnitSubShield", "UseGuardPower")) &&
            subshield.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType?.Name == "DamageInstance" && f.Name == "failed") &&
            subshield.Any(i => i.operand is FieldInfo f && f.Name == "OnGuardSucceeded"),
            "passive subshield success preserves native guard-power consumption and the block result used by MP settlement");
        var dashShield = Code("Charm_DashShield", "OnBeginDash");
        Require(dashShield.Any(i => Call(i, "UnitAvatar", "AddShield")) && dashShield.Any(i => Text(i, "Charm_DashShield")) &&
            !Spends(dashShield) && Code("Charm_DashShield", "OnDisabledEffect").Any(i => Call(i, "UnitAvatar", "RemoveShield")),
            "dash-granted shields use ordinary shield absorption; generating a shield does not itself spend defense MP");
        Require(Code("Charm_BrokenMirror", "HandleEnterFloor").Any(i => Call(i, "UnitAvatar", "AddShield")),
            "automatic item shields converge on the same ordinary shield accounting");
        var shieldUse = Code("UnitAvatar", "UseShield");
        Require(shieldUse.Any(i => i.opcode == OpCodes.Sub) && shieldUse.Any(i => i.LoadsConstant(0f)) &&
            shieldUse.Any(i => i.operand is FieldInfo f && f.Name == "OnRemoveShield") &&
            shieldUse.Any(i => i.operand is FieldInfo f && f.Name == "OnShieldChanged") &&
            shieldUse.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "shield") && !Spends(shieldUse),
            "native shield use consumes shield points, reports removal/change and returns unabsorbed damage without native MP spending");
        Require(!Spends(Code("Charm_ShadowEye", "CalculateIncomingDamage")) &&
            !Spends(Code("Charm_MiniBossFight", "HandleDamageFromMiniboss")) &&
            !Spends(Code("CharacterBuff_Invincible", "ApplyStatusInner")) &&
            !Spends(Code("CharacterBuff_Invulnerable", "ApplyStatusInner")),
            "item evasion, passive damage callbacks and pure invulnerability are not MP defenses");
    }

    private static void ShieldSettlement(Assembly game, Assembly addon, Type hooks)
    {
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var damage = game.GetType("DamageInstance", true)!;
        var subshield = game.GetType("UnitSubShield", true)!;
        List<CodeInstruction> Code(string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(runtime, method)).ToList();
        var before = AccessTools.DeclaredMethod(runtime, "BeforeSubShield", new[] { damage, typeof(bool).MakeByRefType() });
        var after = AccessTools.DeclaredMethod(runtime, "AfterSubShield", new[] { subshield, damage, typeof(bool) });
        Require(before is { IsStatic: true } && before.ReturnType == typeof(void) && before.GetParameters()[1].IsOut &&
            after is { IsStatic: true } && after.ReturnType == typeof(void),
            "subshield prefix captures prior block state and postfix receives the native shield and exact damage instance");
        var install = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(hooks, "Install")).ToList();
        Require(install.Any(i => Text(i, "OnCalculateDamage")) && install.Any(i => i.opcode == OpCodes.Ldtoken && Equals(i.operand, subshield)) &&
            install.Any(i => Text(i, "BeforeSubShield")) && install.Any(i => Text(i, "AfterSubShield")) &&
            install.Any(i => Call(i, "Harmony", "Patch")) &&
            !install.Any(i => Text(i, "ActivateShield") || Text(i, "DeactivateShield")),
            "shared installation observes subshield outcomes without depending on active-artifact lifecycle provenance");
        Require(Code("AfterSubShield").Any(i => i.operand is FieldInfo f && f.DeclaringType == damage && f.Name == "failed") &&
            Code("AfterSubShield").Any(i => i.operand is FieldInfo f && f.DeclaringType == subshield && f.Name == "owner"),
            "subshield settlement reads the native block result and its owning victim");
        var afterHit = Code("AfterHit");
        int finish = afterHit.FindIndex(i => Call(i, "FriendlyFireRuntime", "FinishDefenseMp"));
        int restore = afterHit.FindIndex(i => i.opcode == OpCodes.Stsfld && i.operand is FieldInfo f && f.Name == "current");
        Require(finish >= 0 && finish < restore && afterHit.Count(i => Call(i, "FriendlyFireRuntime", "FinishDefenseMp")) == 1,
            "shield MP is settled once before the enclosing hit scope is restored");
        var settle = Code("FinishDefenseMp");
        Require(settle.Any(i => i.operand is FieldInfo f && f.Name == "ShieldAbsorbed") &&
            settle.Any(i => i.operand is FieldInfo f && f.Name == "DefenseMpSpent") &&
            settle.Any(i => Call(i, "FriendlyFireRuntime", "BalancedDefense")) &&
            settle.Count(i => Call(i, "FriendlyFireRuntime", "SpendGuardMp")) == 1,
            "layered shields share one MP charge after successful absorption and revalidate current PvP eligibility");
        var spend = Code("SpendGuardMp");
        int mark = spend.FindIndex(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "DefenseMpSpent");
        Require(mark >= 0 && spend.Skip(mark + 1).Any(i => Call(i, "UnitAvatar", "UseMp")),
            "MP defense spending is marked before native resource callbacks can reenter settlement");
        foreach (string method in new[] { "ActiveMpShieldCost", "SpendPartialShieldMp", "AbsorbShield", "FinishDefenseMp" })
            Require(!Code(method).Any(i => i.operand is MethodInfo m &&
                (m.DeclaringType?.Name == "Charm_MPShieldActive" || m.DeclaringType?.Name == "GridInventory")),
                method + " applies through shared absorption without active-item lookup");
    }

    private static bool Call(CodeInstruction instruction, string owner, string name) =>
        instruction.operand is MethodInfo method && method.DeclaringType?.Name == owner && method.Name == name;

    private static bool Text(CodeInstruction instruction, string value) =>
        instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, value);

    private static void Signature(CodeInstruction instruction, Type result, params Type[] parameters)
    {
        Require(instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method.IsStatic &&
            method.ReturnType == result && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters),
            "defensive helper preserves the native evaluation stack");
    }

    private static void Require(bool value, string why)
    {
        if (!value) throw new Exception("Active defense contract: " + why);
    }
}
