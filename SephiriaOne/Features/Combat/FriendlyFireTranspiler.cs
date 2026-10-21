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
            int shield = toughness < 0 ? -1 : code.FindIndex(toughness, i => Call(i, typeof(UnitAvatar), "get_Shield"));
            int mpShield = shield < 0 ? -1 : code.FindIndex(shield, i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "MPSHIELD"));
            if (!(leader >= 0 && equality > leader && equality < followers && contains > followers && contains < faction && faction < end &&
                end < beforeAttack && beforeLeader < toughness && shield > toughness) ||
                equality - leader > 4 || contains - followers > 3 || shield - toughness > 28 || code[shield - 1].opcode != OpCodes.Ldarg_0 ||
                mpShield <= shield || mpShield - shield > 45 || code[mpShield - 1].opcode != OpCodes.Ldarg_0)
                throw new InvalidOperationException("Friendly-fire guard/scale anchors changed.");
            int store = shield - 2;
            while (store > toughness && code[store].opcode == OpCodes.Nop) store--;
            if (!code[store].IsStloc() || !code.Skip(toughness).Take(store - toughness).Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, 1f)))
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
            return FriendlyFireWhiteBreadHooks.Rewrite(FriendlyFireAmplificationHooks.Rewrite(
                FriendlyFireEvasionHooks.Rewrite(FriendlyFireHarvestHooks.Rewrite(FriendlyFireGuardHooks.Rewrite(FriendlyFireActiveDefenseHooks.Rewrite(code))))));
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
