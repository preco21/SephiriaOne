using System.Reflection;
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
        Console.WriteLine("Verified native friendly-fire damage anchors, melee/projectile delivery, death replication and stock guest chat (not live multiplayer).");
    }
}
