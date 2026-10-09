using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireHooks
    {
        private const string Id = "SephiriaOne.FriendlyFire";
        internal static void Install()
        {
            var apply = AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDamage", new[] { typeof(DamageInstance) });
            var die = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Die", new[] { typeof(int), typeof(DamageInstance) });
            var beforeAttack = AccessTools.DeclaredMethod(typeof(PlayerAvatar), "HandleBeforeAttack", new[] { typeof(UnitAvatar), typeof(DamageInstance) });
            var relation = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "GetRelation", new[] { typeof(UnitAvatar) });
            var update = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "OnAIUpdate", Type.EmptyTypes);
            var setTarget = AccessTools.DeclaredMethod(typeof(UnitAI_NewBasic), "SetTarget", new[] { typeof(UnitAvatar) });
            if (apply == null || apply.ReturnType != typeof(EApplyDamageResult) || die == null ||
                beforeAttack == null || beforeAttack.IsStatic || beforeAttack.ReturnType != typeof(void) ||
                relation == null || relation.IsStatic || relation.ReturnType != typeof(ERelationBehaviour) ||
                !ValidateCompanionRelation(PatchProcessor.GetOriginalInstructions(relation)) ||
                update == null || update.IsStatic || update.ReturnType != typeof(void) ||
                setTarget == null || setTarget.ReturnType != typeof(void) ||
                !ValidateCompanionUpdate(PatchProcessor.GetOriginalInstructions(update), PatchProcessor.GetOriginalInstructions(setTarget)) ||
                !ValidatePlayerProtection(PatchProcessor.GetOriginalInstructions(beforeAttack)) ||
                !FriendlyFireTranspiler.Validate(PatchProcessor.GetOriginalInstructions(apply)))
                throw new InvalidOperationException("Native allied damage/defense contract changed.");
            var harmony = new Harmony(Id);
            try
            {
                harmony.Patch(apply, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeHit)),
                    transpiler: new HarmonyMethod(typeof(FriendlyFireTranspiler), nameof(FriendlyFireTranspiler.Rewrite)),
                    finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterHit)));
                harmony.Patch(die, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeDeath)),
                    postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterDeath)));
                harmony.Patch(beforeAttack, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforePlayerAttack)));
                harmony.Patch(relation, postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterCompanionRelation)));
                harmony.Patch(update, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeCompanionUpdate)));
                FriendlyFireEffectHooks.Install(harmony);
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }

        internal static bool ValidateCompanionUpdate(IEnumerable<CodeInstruction> update, IEnumerable<CodeInstruction> setTarget)
        {
            var code = update.ToList();
            int active = -1;
            for (int i = 1; i < code.Count; i++)
                if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo f &&
                    f.DeclaringType == typeof(UnitAI_NewBasic) && f.Name == "isInBattleActiveByAI" &&
                    f.FieldType == typeof(bool) && code[i - 1].LoadsConstant(1)) { active = i; break; }
            int attack = code.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAI_NewBasic) && m.Name == "OnAIUpdate_FoundEnemy");
            var change = setTarget.ToList();
            return active >= 0 && attack > active &&
                change.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAI_NewBasic) && m.Name == "OnLostTarget") &&
                change.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAI_NewBasic) && m.Name == "set_CurrentTarget");
        }

        internal static bool ValidateCompanionRelation(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            return code.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAI_NewBasic) && m.Name == "get_Avatar") == 4 &&
                code.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnitAvatar) && m.Name == "get_NetworkLeader") == 2 &&
                code.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(RuntimeFactionManager) && m.Name == "GetRelationBehaviour") == 2 &&
                !code.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld);
        }

        // We bypass this callback, so reject additions that would also skip
        // unrelated work. Its only writes must be the three native vetoes.
        internal static bool ValidatePlayerProtection(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            int deny = 0, block = 0, relation = 0, value = 0, reason = 0, factionInstance = 0, dungeonInstance = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].operand is FieldInfo field)
                {
                    if (code[i].opcode == OpCodes.Stfld && field.DeclaringType == typeof(DamageInstance) && field.Name == "failed" && i > 0)
                    {
                        if (code[i - 1].LoadsConstant((int)EDamageFailType.Deny)) deny++;
                        else if (code[i - 1].LoadsConstant((int)EDamageFailType.Block)) block++;
                        else return false;
                    }
                    else if (code[i].opcode != OpCodes.Ldfld ||
                        !(field.DeclaringType == typeof(UnitAvatar) && (field.Name == "faction" || field.Name == "attackableTargetSelector" || field.Name == "monsterType") ||
                          field.DeclaringType == typeof(PlayerAvatar) && field.Name == "safeMode")) return false;
                }
                else if (code[i].operand is MethodBase method)
                {
                    if (code[i].opcode != OpCodes.Call && code[i].opcode != OpCodes.Callvirt) return false;
                    if (method.DeclaringType == typeof(RuntimeFactionManager) && method.Name == "GetRelationBehaviour") relation++;
                    else if (method.DeclaringType == typeof(RuntimeFactionManager) && method.Name == "GetRelationValue") value++;
                    else if (method.DeclaringType == typeof(DungeonManager) && method.Name == "BreakShieldOfReason") reason++;
                    else if (method.DeclaringType == typeof(RuntimeFactionManager) && method.Name == "get_Instance") factionInstance++;
                    else if (method.DeclaringType == typeof(DungeonManager) && method.Name == "get_Instance") dungeonInstance++;
                    else return false;
                }
            }
            return deny == 2 && block == 1 && relation == 1 && value == 1 && reason == 1 && factionInstance == 2 && dungeonInstance == 1;
        }
        internal static void Uninstall() { new Harmony(Id).UnpatchAll(Id); FriendlyFireRuntime.Clear(); }
    }
}
