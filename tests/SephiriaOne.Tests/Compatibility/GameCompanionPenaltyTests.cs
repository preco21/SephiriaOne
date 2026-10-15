using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameCompanionPenaltyTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        List<CodeInstruction> Code(string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType(type, true)!, method)).ToList();
        bool Call(CodeInstruction i, string owner, string method) =>
            i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == method;
        void Require(bool pass, string reason) { if (!pass) throw new Exception("Companion penalty contract: " + reason); }
        var crime = Code("DungeonManager", "NPCDeadCheckServerside");
        var damage = Code("UnitAI_NewBasic", "OnDamaged");
        var death = Code("UnitAI_NewBasic", "OnDie");
        var hooks = addon.GetType("SephiriaOne.FriendlyFireCompanionPenaltyHooks", true)!;
        var validate = AccessTools.DeclaredMethod(hooks, "Validate");
        bool Valid(IEnumerable<CodeInstruction> a, IEnumerable<CodeInstruction> b, IEnumerable<CodeInstruction> c) =>
            (bool)validate.Invoke(null, new object[] { a, b, c })!;
        Require(Valid(crime, damage, death), "installed penalty and cleanup boundaries pass the runtime guard");
        Require(!Valid(crime.Where(i => !Call(i, "UnitAvatar", "ApplyBuff")), damage, death), "changed crime behavior is rejected");
        Require(!Valid(crime, damage, death.Where(i => !Call(i, "UnitAvatar", "StopBattle"))), "changed death cleanup is rejected");
        Require(!Valid(crime, damage.Concat(damage.Where(i => Call(i, "DungeonManager", "NotifyWanderersHostile"))), death),
            "additional hostility sites require a new audit");
        Require(crime.Count(i => Call(i, "SaveData", "SetInt")) == 1 &&
            crime.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "CrimeCount")) == 2 &&
            crime.Count(i => Call(i, "RuntimeFactionManager", "SetRelationValue")) == 2 &&
            crime.Count(i => Call(i, "DungeonManager", "RpcMerchantsHostileChange")) == 1 &&
            crime.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "RedMerchant")),
            "one skipped crime call owns saved count, party debuff and persistent hostility together");

        var rewrite = AccessTools.DeclaredMethod(hooks, "RewriteDamage");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        var original = damage.Select(i => new CodeInstruction(i)).ToList();
        var actual = Rewrite(damage.Select(i => new CodeInstruction(i)).ToList());
        int cursor = 0;
        foreach (var native in original)
        {
            string? helper = Call(native, "RuntimeFactionManager", "SetTempEnemyRelation") ? "CompanionTempHostility" :
                Call(native, "DungeonManager", "NotifyWanderersHostile") ? "CompanionWandererHostility" : null;
            if (helper == null)
            {
                Require(actual[cursor].opcode == native.opcode && Equals(actual[cursor].operand, native.operand),
                    "unrelated native instruction including target/branch handling must be preserved");
                cursor++;
            }
            else
            {
                Require(actual[cursor].opcode == OpCodes.Ldarg_0 && actual[cursor + 1].opcode == OpCodes.Ldarg_1 &&
                    Call(actual[cursor + 2], "FriendlyFireRuntime", helper), "wrapper adds only exact NPC and damage context");
                cursor += 3;
            }
        }
        Require(cursor == actual.Count, "rewrite has no extra behavior outside the two global consequences");
        bool rejected = false;
        try { Rewrite(damage.Where(i => !Call(i, "RuntimeFactionManager", "SetTempEnemyRelation"))); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "transpiler rejects changed instructions before emitting a partial patch");

        var die = Code("UnitAvatar", "Die");
        int dead = die.FindIndex(i => Call(i, "UnitAvatar", "set_NetworkIsDead"));
        int callbacks = die.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnDie");
        int release = die.FindIndex(i => Call(i, "UnitAvatar", "SetLeader"));
        Require(dead >= 0 && callbacks > dead && release > callbacks &&
            Code("Charm_SummonUnit", "HandleUnitDie").Any(i => Call(i, "UnitAvatar", "SetLeader")),
            "native death and summon callbacks require captured ownership through cleanup");
        Require(death.FindIndex(i => Call(i, "UnitAI_NewBasic", "OnLostTarget")) <
            death.FindIndex(i => Call(i, "DungeonManager", "NPCDeadCheckServerside")) &&
            death.FindIndex(i => Call(i, "UnitAvatar", "StopBattle")) >
            death.FindIndex(i => Call(i, "DungeonManager", "NPCDeadCheckServerside")),
            "native NPC death cleanup remains outside the skipped penalty method");
        var install = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireHooks", true)!, "Install"));
        Require(install.Any(i => Call(i, "FriendlyFireCompanionPenaltyHooks", "Install")), "shared friendly-fire lifecycle owns the hooks");
        Console.WriteLine("Verified companion crime/count/reputation boundaries, two-call hostility isolation, preserved native cleanup and changed-contract rejection (not live multiplayer).");
    }
}
