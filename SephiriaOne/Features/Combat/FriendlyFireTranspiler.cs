using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireTranspiler
    {
        internal static bool Validate(IEnumerable<CodeInstruction> instructions)
        {
            try { Rewrite(instructions, new DynamicMethod("FriendlyFireValidation", typeof(void), Type.EmptyTypes).GetILGenerator()).ToList(); return true; }
            catch (InvalidOperationException) { return false; }
        }

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            int beforeAttack = BeforeAttackInvoke(code, "OnAttackUnitBeforeOperation", typeof(Action<UnitAvatar, DamageInstance>));
            int beforeLeader = BeforeAttackInvoke(code, "OnAttackUnitBeforeOperation_AsLeader", typeof(Action<UnitAvatar, UnitAvatar, DamageInstance>));
            int recheck = beforeLeader + 1;
            if (beforeAttack < 0 || beforeLeader <= beforeAttack || beforeLeader - beforeAttack > 24 || recheck >= code.Count ||
                code[recheck].opcode != OpCodes.Ldarg_0 && code[recheck].opcode != OpCodes.Ldarg_1 ||
                code.Take(recheck + 1).Any(i => i.blocks.Count != 0))
                throw new InvalidOperationException("Native before-attack callback boundary changed.");
            int end = code.FindIndex(i => i.operand is FieldInfo f && f.DeclaringType == typeof(UnitAvatar) && f.Name == "isForcedChaosDamage");
            int leader = code.FindIndex(i => Call(i, typeof(UnitAvatar), "get_NetworkLeader"));
            int equality = leader < 0 ? -1 : code.FindIndex(leader, i => Call(i, typeof(UnityEngine.Object), "op_Equality"));
            int followers = code.FindIndex(i => i.operand is FieldInfo f && f.DeclaringType == typeof(UnitAvatar) && f.Name == "followers");
            int contains = followers < 0 ? -1 : code.FindIndex(followers, i => Call(i, typeof(Mirror.SyncList<UnitAvatar>), "Contains"));
            int faction = code.FindIndex(i => Call(i, typeof(CombatManager), "ContainsAttackableFaction"));
            int toughness = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "TOUGHNESS"));
            // 1.1.0 moved defense, true damage, Toughness and the minimum-one
            // clamp into ApplyDefense. Keep scaling at its caller, before shields.
            var defense = AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDefense", new[] { typeof(float), typeof(int), typeof(int) });
            var defenseCalls = Enumerable.Range(0, code.Count).Where(i => defense != null && code[i].Calls(defense)).ToArray();
            bool extractedDefense = toughness < 0 && defenseCalls.Length == 1 && !defense.IsStatic && defense.ReturnType == typeof(float) &&
                ValidateDefenseHelper(PatchProcessor.GetOriginalInstructions(defense));
            int defenseEnd = extractedDefense ? defenseCalls[0] : toughness;
            int shield = defenseEnd < 0 ? -1 : code.FindIndex(defenseEnd, i => Call(i, typeof(UnitAvatar), "get_Shield"));
            int mpShield = shield < 0 ? -1 : code.FindIndex(shield, i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "MPSHIELD"));
            if (!(leader >= 0 && equality > leader && equality < followers && contains > followers && contains < faction && faction < end &&
                end < beforeAttack && beforeLeader < defenseEnd && shield > defenseEnd) ||
                equality - leader > 4 || contains - followers > 3 || shield - defenseEnd > 28 || code[shield - 1].opcode != OpCodes.Ldarg_0 ||
                mpShield <= shield || mpShield - shield > 45 || code[mpShield - 1].opcode != OpCodes.Ldarg_0)
                throw new InvalidOperationException("Friendly-fire guard/scale anchors changed.");
            int store = shield - 2;
            while (store > defenseEnd && code[store].opcode == OpCodes.Nop) store--;
            if (!code[store].IsStloc() || (extractedDefense ? store != defenseEnd + 1 || defenseEnd < 4 ||
                    code[defenseEnd - 4].opcode != OpCodes.Ldarg_0 || !code[defenseEnd - 3].IsLdloc() ||
                    !code[defenseEnd - 2].IsLdloc() || !code[defenseEnd - 1].IsLdloc() ||
                    !SameLocal(code[defenseEnd - 3], code[store]) :
                !code.Skip(toughness).Take(store - toughness).Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 1f))))
                throw new InvalidOperationException("Native minimum damage clamp changed.");
            // NaN/negative values introduced by native calculations can bypass
            // the positive-damage block entirely. Sanitize its common exit too,
            // before native MP integer conversion, without applying the scale twice.
            var sanitizeLoad = LoadLocal(code[store]);
            sanitizeLoad.labels.AddRange(code[mpShield - 1].labels); code[mpShield - 1].labels.Clear();
            sanitizeLoad.blocks.AddRange(code[mpShield - 1].blocks); code[mpShield - 1].blocks.Clear();
            code.InsertRange(mpShield - 1, new[] { sanitizeLoad, CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.Sanitize)), new CodeInstruction(code[store].opcode, code[store].operand) });
            // Inject on the common entry to shield absorption, including the
            // branch that skips the minimum-one clamp. Move its labels with it.
            var load = LoadLocal(code[store]);
            load.labels.AddRange(code[shield - 1].labels); code[shield - 1].labels.Clear();
            load.blocks.AddRange(code[shield - 1].blocks); code[shield - 1].blocks.Clear();
            code.InsertRange(shield - 1, new[] { load, CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.Scale)), new CodeInstruction(code[store].opcode, code[store].operand) });
            // Both null-delegate paths and the leader callback join here, before
            // native protection/parry/guard callbacks or costs. Return only for
            // an admitted allied hit whose permission changed during callbacks.
            var continuation = generator.DefineLabel();
            var permission = CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ContinueHit));
            permission.labels.AddRange(code[recheck].labels); code[recheck].labels.Clear();
            code[recheck].labels.Add(continuation);
            code.InsertRange(recheck, new[] { permission, new CodeInstruction(OpCodes.Brtrue, continuation),
                new CodeInstruction(OpCodes.Ldc_I4, (int)EApplyDamageResult.Fail_Absolute), new CodeInstruction(OpCodes.Ret) });
            code.Insert(faction + 1, CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AllowFaction)));
            code.Insert(contains + 1, CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ProtectLeader)));
            code.Insert(equality + 1, CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.ProtectLeader)));
            code = FriendlyFireCriticalDamageHooks.Rewrite(code, generator).ToList();
            return FriendlyFireDefensiveEffectHooks.Rewrite(FriendlyFireWhiteBreadHooks.Rewrite(FriendlyFireAmplificationHooks.Rewrite(
                FriendlyFireEvasionHooks.Rewrite(FriendlyFireHarvestHooks.Rewrite(FriendlyFireGuardHooks.Rewrite(FriendlyFireActiveDefenseHooks.Rewrite(code)))))));
        }
        internal static bool ValidateDefenseHelper(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            int toughness = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "TOUGHNESS"));
            int follower = code.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "FOLLOWERDEFENSE"));
            // The extracted helper must return the same damage argument after
            // Toughness subtraction and the native minimum-one clamp.
            bool StoreDamage(CodeInstruction i) => (i.opcode == OpCodes.Starg || i.opcode == OpCodes.Starg_S) &&
                (i.operand is ParameterInfo p ? p.Position == 0 : Convert.ToInt32(i.operand) == 1);
            return toughness > 0 && toughness + 15 == code.Count && follower > 0 && follower < toughness &&
                Call(code[follower - 1], typeof(UnitAvatar), "get_NetworkLeader") &&
                Call(code[follower + 1], typeof(UnitAvatar), "GetCustomStatUnsafe") &&
                code.Any(i => Call(i, typeof(UnitAvatar), "GetDamageReduction")) &&
                code[toughness - 1].IsLdarg(0) && Call(code[toughness + 1], typeof(UnitAvatar), "GetCustomStatUnsafe") &&
                code[toughness + 2].IsStloc() && code[toughness + 3].IsLdarg(1) &&
                SameLocal(code[toughness + 4], code[toughness + 2]) && code[toughness + 5].opcode == OpCodes.Conv_R4 &&
                code[toughness + 6].opcode == OpCodes.Sub && StoreDamage(code[toughness + 7]) &&
                code[toughness + 8].IsLdarg(1) && code[toughness + 9].LoadsConstant(1f) &&
                (code[toughness + 10].opcode == OpCodes.Bge_Un || code[toughness + 10].opcode == OpCodes.Bge_Un_S) &&
                code[toughness + 10].operand is Label end && code[toughness + 13].labels.Contains(end) &&
                code[toughness + 11].LoadsConstant(1f) && StoreDamage(code[toughness + 12]) &&
                code[toughness + 13].IsLdarg(1) && code[toughness + 14].opcode == OpCodes.Ret;
        }
        private static bool SameLocal(CodeInstruction load, CodeInstruction store)
        {
            var expected = LoadLocal(store);
            return load.opcode == expected.opcode && Equals(load.operand, expected.operand);
        }
        private static int BeforeAttackInvoke(List<CodeInstruction> code, string name, Type signature)
        {
            var fields = Enumerable.Range(0, code.Count).Where(i => code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f &&
                f.DeclaringType == typeof(UnitAvatar) && f.Name == name && f.FieldType == signature).ToArray();
            if (fields.Length != 1) return -1;
            int call = code.FindIndex(fields[0] + 1, i => Call(i, signature, "Invoke"));
            return call > fields[0] && call - fields[0] <= 10 ? call : -1;
        }
        private static bool Call(CodeInstruction i, Type type, string name) =>
            (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.DeclaringType == type && m.Name == name;
        private static CodeInstruction LoadLocal(CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            return new CodeInstruction(store.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc, store.operand);
        }
    }
}
