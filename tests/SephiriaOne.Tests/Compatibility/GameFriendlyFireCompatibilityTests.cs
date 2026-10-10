using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string owner, string method) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Friendly fire contract: " + why); }
        var transpiler = addon.GetType("SephiriaOne.FriendlyFireTranspiler", true)!;
        var apply = Code("UnitAvatar", "ApplyDamage");
        Require((bool)AccessTools.Method(transpiler,"Validate").Invoke(null,new object[] { apply })!, "native ally/shield anchors");
        var hooks = addon.GetType("SephiriaOne.FriendlyFireHooks", true)!;
        Require((bool)AccessTools.Method(hooks, "ValidateReceivedDamage").Invoke(null, new object[] { apply })!, "three native received-damage accounting calls precede death");
        var changedAccounting = apply.Select(i => new CodeInstruction(i)).ToList();
        changedAccounting.RemoveAt(changedAccounting.FindIndex(i => i.operand is MethodInfo m && m.Name == "AddReceivedDamage"));
        Require(!(bool)AccessTools.Method(hooks, "ValidateReceivedDamage").Invoke(null, new object[] { changedAccounting })!, "changed accounting path rejects KDA hooks");
        Require(Calls(Code("PlayerAvatar", "AddReceivedDamage"), "UnitAvatar", "AddReceivedDamage"), "player accounting reaches patched base hook");
        Require(AccessTools.Field(Type("PlayerSpawner"), "steamID")?.FieldType == typeof(ulong), "session account identity retains native ulong representation");
        Require(Calls(Code("UnitAvatar", "Revive"), "UnitAvatar", "set_NetworkIsDead"), "native revival marks a new living avatar");
        var protection = Code("PlayerAvatar", "HandleBeforeAttack");
        Require((bool)AccessTools.Method(hooks,"ValidatePlayerProtection").Invoke(null,new object[] { protection })!, "native second player-protection callback");
        var changedProtection = protection.Select(i => new CodeInstruction(i)).ToList();
        changedProtection.RemoveAt(changedProtection.FindIndex(i => i.operand is MethodInfo m && m.Name == "BreakShieldOfReason"));
        Require(!(bool)AccessTools.Method(hooks,"ValidatePlayerProtection").Invoke(null,new object[] { changedProtection })!, "changed protection callback fails closed");
        var awake = Code("PlayerAvatar", "Awake");
        Require(awake.Any(i => i.opcode == OpCodes.Ldftn && i.operand is MethodInfo m && m.Name == "HandleBeforeAttack") &&
            Calls(awake, "UnitAvatar", "add_OnAttackUnitBeforeOperation"), "native player registers before-attack protection");
        int beforeAttack = apply.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnAttackUnitBeforeOperation");
        int guard = apply.FindIndex(i => i.operand is FieldInfo f && f.Name == "isGuardEnabled");
        int failureRead = apply.FindIndex(guard + 1, i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.DeclaringType == Type("DamageInstance") && f.Name == "failed");
        int hpWrite = apply.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_Networkhp");
        Require(beforeAttack >= 0 && beforeAttack < guard && guard < failureRead && failureRead < hpWrite,
            "guard feedback precedes consumption of player veto and authoritative HP write");
        Require(Calls(Code("UnitAvatar", "set_Networkhp"), "NetworkBehaviour", "GeneratedSyncVarSetter"), "native health uses stock SyncVar replication");
        var relation = Code("UnitAI_NewBasic", "GetRelation");
        Require((bool)AccessTools.Method(hooks,"ValidateCompanionRelation").Invoke(null,new object[] { relation })!, "native companion relation contract");
        foreach (string consumer in new[] { "SearchTarget", "OnAIUpdate", "OnFoundTarget" })
            Require(Calls(Code("UnitAI_NewBasic", consumer), "UnitAI_NewBasic", "GetRelation"), "relation query controls " + consumer);
        Require(Type("UnitAI_WeaselKnight").BaseType == Type("UnitAI_NewBasic"), "Collin inherits shared targeting");
        var aiUpdate = Code("UnitAI_NewBasic", "OnAIUpdate");
        Require((bool)AccessTools.Method(hooks,"ValidateCompanionUpdate").Invoke(null,new object[] { aiUpdate, Code("UnitAI_NewBasic", "SetTarget") })!, "native battle activation and virtual target-loss cleanup");
        Require(Calls(aiUpdate, "UnitAvatar", "StopBattle") && Calls(aiUpdate, "UnitAI_NewBasic", "OnAIUpdate_FollowLeader"), "nonhostile target returns to following owner");
        Require(Calls(Code("UnitAI_WeaselKnight", "OnAIUpdate_FollowLeader"), "Unit_WeaponEquipped", "StopAttack"), "Collin stops attacks on the next nonhostile AI update");
        Require(Calls(Code("UnitAI_WeaselKnight", "OnAIUpdate_FoundEnemy"), "Unit_WeaponEquipped", "StartAttack"), "Collin uses native weapon attacks");
        Require(Calls(Code("UnitAI_Archer", "OnLostTarget"), "Unit_Archer", "StopAttack") &&
            Calls(Code("UnitAI_SoulWeapon_Archer", "OnLostTarget"), "Unit_SoulWeapon_Archer", "StopAttack"), "native target loss releases both archer variants' held attacks");
        var search = Code("UnitAI_NewBasic", "SearchTarget");
        Require(search.Any(i => i.operand is FieldInfo f && f.Name == "IsDead") && Calls(search, "UnitAvatar", "get_IsInvulnerable") &&
            search.Any(i => i.operand is FieldInfo f && f.Name == "sightRadius"), "native target validity and sight range stay in force");
        var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(transpiler,"Rewrite").Invoke(null,new object[] { apply })!).ToList();
        Require(rewritten.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "FriendlyFireRuntime") == 5, "exactly three guard helpers, one scale and one numeric check");
        Require(Calls(apply,"UnitAvatar","UseShield") && Calls(apply,"UnitAvatar","UseMp") && Calls(apply,"UnitAvatar","Die"), "native shield/MP/death handling");
        Require(Calls(Code("MeleeCollision","Attack"),"CombatBehaviour","ApplyDamage"), "server melee delivers damage");
        var bulletMethods = Type("Bullet").GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Require(bulletMethods.Any(m => m.GetMethodBody() != null && Calls(PatchProcessor.GetOriginalInstructions(m),"CombatBehaviour","ApplyDamage")), "native projectile collision delivers damage");
        Require(AccessTools.DeclaredMethod(Type("PlayerAvatar"),"ApplyDamage") == null && AccessTools.DeclaredMethod(Type("PlayerAvatar"),"Die") == null, "players use patched base damage/death");
        var death = Code("UnitAvatar","Die");
        Require(Calls(death,"UnitAvatar","set_NetworkIsDead") && Calls(death,"UnitAvatar","RpcDie"), "death uses stock replication");
        Require(Calls(Code("DungeonManager","Chat"),"DungeonManager","RpcChat"), "host chat broadcasts");
        Require(Calls(Code("DungeonManager","UserCode_RpcChat__PlayerAvatar__String__String"),"GameLogWriter","WriteLog"), "stock clients render kill chat");
        Console.WriteLine("Verified native friendly-fire damage/player-veto anchors, shared companion AI and Collin stop/start paths, HP/death replication and stock guest chat (not live multiplayer).");
        GameFriendlyFireReflectionTests.Run(game);
        GameFriendlyFireScaleTests.Run(addon);
        GameFriendlyFireEffectTests.Run(game, addon);
        GameReviveAllTests.Run(game, addon);
        GameDeathmatchTests.Run(game, addon);
    }
}
