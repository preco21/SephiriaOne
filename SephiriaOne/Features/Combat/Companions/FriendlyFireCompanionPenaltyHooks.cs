using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireCompanionPenaltyHooks
    {
        internal static void Install(Harmony harmony)
        {
            var crime = AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.NPCDeadCheckServerside),
                new[] { typeof(UnitAI_NewBasic), typeof(DamageInstance) });
            var damage = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnDamaged", new[] { typeof(DamageInstance) });
            var death = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnDie", new[] { typeof(DamageInstance) });
            if (crime == null || damage == null || death == null || crime.ReturnType != typeof(void) ||
                damage.ReturnType != typeof(void) || death.ReturnType != typeof(void) ||
                !Validate(PatchProcessor.GetOriginalInstructions(crime), PatchProcessor.GetOriginalInstructions(damage),
                    PatchProcessor.GetOriginalInstructions(death)))
                throw new InvalidOperationException("Native companion penalty contract changed.");
            harmony.Patch(crime, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeCompanionCrime)));
            harmony.Patch(damage, transpiler: new HarmonyMethod(typeof(FriendlyFireCompanionPenaltyHooks), nameof(RewriteDamage)));
        }

        internal static bool Validate(IEnumerable<CodeInstruction> crime, IEnumerable<CodeInstruction> damage, IEnumerable<CodeInstruction> death)
        {
            var penalty = crime.ToList(); var dying = death.ToList();
            return penalty.Count(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f &&
                    f.DeclaringType == typeof(DungeonManager) && f.Name == "crimeDebuff") == 1 &&
                penalty.Count(i => Calls(i, typeof(UnitAvatar), "ApplyBuff")) == 1 &&
                penalty.Count(i => Calls(i, typeof(RuntimeFactionManager), "SetRelationValue")) == 2 &&
                penalty.Count(i => Calls(i, typeof(DungeonManager), "RpcMerchantsHostileChange")) == 1 &&
                penalty.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "CrimeCount")) == 2 &&
                penalty.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "RedMerchant")) == 1 &&
                dying.Count(i => Calls(i, typeof(DungeonManager), nameof(DungeonManager.NPCDeadCheckServerside))) == 1 &&
                dying.Count(i => Calls(i, typeof(UnitAI_NewBasic), "OnLostTarget")) == 1 &&
                dying.Count(i => Calls(i, typeof(UnitAvatar), "StopBattle")) == 1 && ValidateDamage(damage);
        }

        private static bool ValidateDamage(IEnumerable<CodeInstruction> damage)
        {
            var code = damage.ToList();
            return code.Count(i => Calls(i, typeof(RuntimeFactionManager), "SetTempEnemyRelation")) == 1 &&
                code.Count(i => Calls(i, typeof(DungeonManager), nameof(DungeonManager.NotifyWanderersHostile))) == 1 &&
                code.Count(i => Calls(i, typeof(UnitAI_NewBasic), "SetTarget")) == 1;
        }

        internal static IEnumerable<CodeInstruction> RewriteDamage(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            if (!ValidateDamage(code)) throw new InvalidOperationException("Native companion retaliation contract changed.");
            foreach (var instruction in code)
            {
                string helper = Calls(instruction, typeof(RuntimeFactionManager), "SetTempEnemyRelation") ?
                    nameof(FriendlyFireRuntime.CompanionTempHostility) :
                    Calls(instruction, typeof(DungeonManager), nameof(DungeonManager.NotifyWanderersHostile)) ?
                    nameof(FriendlyFireRuntime.CompanionWandererHostility) : null;
                if (helper == null) { yield return instruction; continue; }
                // Retain the native branch, argument reads, personal target and
                // cleanup. Only the two shared reputation/notice writes change.
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return CodeInstruction.Call(typeof(FriendlyFireRuntime), helper);
            }
        }

        private static bool Calls(CodeInstruction instruction, Type owner, string name) =>
            (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
            instruction.operand is MethodInfo method && method.DeclaringType == owner && method.Name == name;
    }
}
