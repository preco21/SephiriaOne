using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireArtifactTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = addon.GetType("SephiriaOne.FriendlyFireArtifactHooks", true)!;
        Type Native(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Native(type), name);
        void Require(bool value, string why) { if (!value) throw new Exception("Artifact hostility contract: " + why); }
        bool Calls(CodeInstruction i, string type, string method) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method;
        MethodInfo[] Registry(string name) => ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, name).Invoke(null, null)!).ToArray();
        List<CodeInstruction> Rewrite(string rewrite, MethodInfo method, List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, rewrite).Invoke(null,
                rewrite == "RewriteNearestCalls" ? new object[] { code, method } : new object[] { code })!).ToList();
        void Check(MethodInfo method, string rewrite, string helper, int helperCount, int added = 0)
        {
            if (method == null) throw new Exception("Artifact hostility entry point missing: " + rewrite);
            var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            var mapped = Rewrite(rewrite, method, original);
            Require(mapped.Count == original.Count + added && mapped.Count(i => Calls(i, "FriendlyFireRuntime", helper)) == helperCount,
                method + " preserves native body and expected helper count");
            // Removing an audited call/field must not silently broaden another site.
            int anchor = rewrite == "RewriteHoming" || rewrite == "RewriteDagger" || rewrite == "RewriteNearestMask"
                ? original.FindIndex(i => Calls(i, "UnitAvatar", "GetHostileFactionLayers"))
                : original.FindIndex(i => rewrite == "RewriteBattle" ? Calls(i, "UnitAvatar", "get_IsInBattle") :
                    rewrite == "RewriteNearestCalls" ? Calls(i, "PlayerInputController", "SearchTargetNearestPoint") :
                    rewrite == "RewriteDamageProc" ? Calls(i, "CombatBehaviour", "ApplyDamage") : Calls(i, "UnitAvatar", "ApplyDebuff"));
            Require(anchor >= 0, method + " native anchor exists");
            original.RemoveAt(anchor);
            bool rejected = false;
            try { Rewrite(rewrite, method, original); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, method + " rejects a changed native body");
        }
        var readers = Registry("BattleReaders"); var nearest = Registry("NearestCallers"); var procs = Registry("DamageProcs");
        Require(readers.Length == 6 && nearest.Length == 4 && procs.Length == 6, "complete audited registry, with no global battle patch");
        foreach (var m in readers) Check(m, "RewriteBattle", "ArtifactInBattle", 1);
        foreach (var m in nearest) Check(m, "RewriteNearestCalls", "ArtifactNearestPoint", m.DeclaringType?.Name == "Charm_GuardCounter" ? 1 : 2);
        foreach (var m in procs) Check(m, "RewriteDamageProc", "ApplyArtifactDamage", 1);
        Check(Method("PlayerInputController", "SearchTargetNearestPoint"), "RewriteNearestMask", "ArtifactNearestMask", 1);
        Check(Method("DaggerGrowthBullet", "HitCheck"), "RewriteDagger", "ItemHitTarget", 1);
        Check(Method("Bullet", "Update"), "RewriteHoming", "ArtifactHomingTarget", 1, 2);
        Check(Method("Charm_AttackChim", "HandleAddedDebuffOnTarget"), "RewriteDebuffProc", "ApplyArtifactDebuff", 1);

        var bat = PatchProcessor.GetOriginalInstructions(Method("Charm_IceBat", "OnUpdate")).ToList();
        Require(bat.Any(i => Calls(i, "CharacterDebuff", "CompareID")) && bat.Any(i => Calls(i, "CharacterDebuff", "get_NetworkAttacker")) &&
            bat.Any(i => i.LoadsConstant(144f)) && bat.Any(i => Calls(i, "Charm_IceBat", "RpcCreateIceBatBullet")),
            "bat retains owned frostbite, 12-unit range and native projectile RPC");
        var bullet = PatchProcessor.GetOriginalInstructions(Method("Charm_IceBat_Projectile", "ApplyDamageOnServer"));
        Require(bullet.Any(i => Calls(i, "CombatBehaviour", "ApplyDamage")), "bat impact reaches central damage rules");
        var homing = Rewrite("RewriteHoming", Method("Bullet", "Update"), PatchProcessor.GetOriginalInstructions(Method("Bullet", "Update")).ToList());
        Require(homing.Count(i => Calls(i, "FriendlyFireRuntime", "ArtifactHomingLost")) == 1 &&
            homing.Any(i => Calls(i, "Bullet", "set_HomingTarget")), "native homing clear/reacquisition stays in the same update");
        var battle = PatchProcessor.GetOriginalInstructions(Method("PlayerBattleChecker", "Update"));
        Require(battle.Any(i => i.operand is MethodInfo m && m.Name == "OverlapCircle"), "native combat checker remains separately owned");
        Console.WriteLine("Verified 15 artifact selectors, six offense-only combat gates, scoped native aim, artifact homing/loss, bounded proc callsites and owned-frostbite/RPC contracts (not live multiplayer).");
    }
}
