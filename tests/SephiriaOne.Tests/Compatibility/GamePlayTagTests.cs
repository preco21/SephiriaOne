using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePlayTagTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        List<CodeInstruction> Code(Assembly assembly, string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(assembly.GetType(type, true)!, method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string owner, string method) =>
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Play Tag contract: " + why); }

        // Bomb expiry must retain the native replicated death path, without going
        // through attack defenses, extra-life interception or synthetic kill credit.
        var die = Code(game, "UnitAvatar", "Die");
        foreach (string call in new[] { "set_Networkhp", "set_NetworkIsDead", "ClearDamageRestoreHp", "RpcDie" })
            Require(Calls(die, "UnitAvatar", call), "native bomb death preserves " + call);
        Require(die.Any(i => i.operand is FieldInfo f && f.Name == "OnDie") &&
            !Calls(die, "UnitAvatar", "ApplyDamage") && !Calls(die, "UnitAvatar", "ForceDie"),
            "native death invokes lifecycle observers without an attack or forced teardown");
        Require(Calls(Code(game, "UnitAvatar", "RpcDie"), "NetworkBehaviour", "SendRPCInternal"),
            "ordinary death is visible to stock guest clients");
        var explode = Code(addon, "SephiriaOne.CombatMatchRuntime", "ExplodePlayTag");
        int death = explode.FindIndex(i => i.operand is MethodInfo m && m.Name == "Die");
        Require(death >= 2 && explode[death - 1].opcode == OpCodes.Ldnull && explode[death - 2].LoadsConstant(5),
            "expiry sends a native death with no fabricated attacker");
        Require(!Calls(explode, "UnitAvatar", "ApplyDamage") && !Calls(explode, "UnitAvatar", "ForceDie") &&
            Calls(explode, "PlayTagBomb", "ConsumeExplosion") && Calls(explode, "CombatMatchRuntime", "Stop"),
            "one consumed expiry follows shared stop/recovery");
        Require(explode.Any(i => i.operand is FieldInfo f && f.Name == "IsDead"),
            "an already-dead carrier is not killed a second time");

        var afterDeath = Code(addon, "SephiriaOne.FriendlyFireRuntime", "AfterDeath");
        int complete = afterDeath.FindIndex(i => i.operand is MethodInfo m && m.Name == "CompleteDeath");
        int pass = afterDeath.FindIndex(i => i.operand is MethodInfo m && m.Name == "ConfirmedKill");
        Require(complete >= 0 && pass > complete && afterDeath.Any(i => i.operand is FieldInfo f && f.Name == "Exceptional"),
            "passes use completed attributed kills and distinguish exceptional death callbacks");
        var finish = Code(addon, "SephiriaOne.FriendlyFireRuntime", "FinishDeath");
        Require(Calls(finish, "FriendlyFireRuntime", "AfterDeath") && Calls(finish, "CombatMatchRuntime", "LeaveDeath"),
            "expiry after a pass waits for the shared native death boundary");
        var hooks = Code(addon, "SephiriaOne.FriendlyFireHooks", "Install");
        Require(hooks.Any(i => Equals(i.operand, "AfterRevive")), "successful native/manual revivals reach the shared postfix");
        Require(hooks.Any(i => Equals(i.operand, "FinishRevive")) &&
            Calls(Code(addon, "SephiriaOne.FriendlyFireRuntime", "FinishRevive"), "CombatMatchRuntime", "ReviveFailed"),
            "an exceptional partial native revival cannot leave a transferred bomb unarmed forever");
        Require(Calls(Code(addon, "SephiriaOne.FriendlyFireRuntime", "AfterRevive"), "CombatMatchRuntime", "Revived") &&
            Calls(Code(addon, "SephiriaOne.CombatMatchRuntime", "Revived"), "PlayTagBomb", "ArmAfterRespawn"),
            "the transferred fuse arms from actual native revival completion");
        // Modifiers must use the same resolved player-hit hook as direct,
        // companion, reflected and debuff damage, without native stat mutations.
        var scale = Code(addon, "SephiriaOne.FriendlyFireRuntime", "Scale");
        var roles = Code(addon, "SephiriaOne.CombatMatchRuntime", "TryRoleSettings");
        Require(Calls(roles, "SessionSettings", "get_PlayTagChaser") && Calls(roles, "CombatMatchRuntime", "IsActiveChaser") &&
            Calls(scale, "CombatMatchRuntime", "RefillRoleScale") && Calls(scale, "CombatMatchRuntime", "IncomingRoleScale"),
            "resolved PvP damage reads both current roles and configured multipliers");
        Require(!scale.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld),
            "chaser composition does not mutate native stats or retain applied bonuses");
        var budget = Code(addon, "SephiriaOne.FriendlyFireRuntime", "LimitBalancedDamage");
        Require(Calls(budget, "CombatMatchRuntime", "get_RoleRevision") &&
            budget.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "BudgetIncomingScale") &&
            Code(addon, "SephiriaOne.FriendlyFireRuntime", "FinishBalancedDamage").Any(i =>
                i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.Name == "BudgetIncomingScale"),
            "shared reservation tracks role changes and captures incoming scale for refunds");
        Console.WriteLine("Verified Play Tag native death, replication, revival and shared chaser damage/budget integration contracts (not live multiplayer).");
    }
}
