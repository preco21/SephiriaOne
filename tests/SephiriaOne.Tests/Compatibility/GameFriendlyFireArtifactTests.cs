using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireArtifactTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = addon.GetType("SephiriaOne.FriendlyFireArtifactHooks", true)!;
        var targeting = addon.GetType("SephiriaOne.FriendlyFireTargetingHooks", true)!;
        Type Native(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Native(type), name);
        void Require(bool value, string why) { if (!value) throw new Exception("Artifact hostility contract: " + why); }
        bool Calls(CodeInstruction i, string type, string method) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method;
        MethodInfo[] Registry(string name) => ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, name).Invoke(null, null)!).ToArray();
        List<CodeInstruction> Rewrite(string rewrite, MethodInfo method, List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)AccessTools.Method(rewrite is "RewriteHoming" or "RewriteNearestMask" ? targeting : hooks, rewrite).Invoke(null,
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
        foreach (var m in nearest) Check(m, "RewriteNearestCalls", "OffensiveNearestPoint", m.DeclaringType?.Name == "Charm_GuardCounter" ? 1 : 2);
        foreach (var m in procs) Check(m, "RewriteDamageProc", "ApplyArtifactDamage", 1);
        var cloud = Method("ComboEffect_DarkCloud", "FireLightning");
        var cloudCode = PatchProcessor.GetOriginalInstructions(cloud).ToList();
        bool CloudValid(IEnumerable<CodeInstruction> code) => (bool)AccessTools.Method(hooks, "ValidateDarkCloudStrike").Invoke(null, new object[] { code })!;
        Require(cloud.ReturnType == typeof(void) && !cloud.IsStatic &&
            cloud.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { Native("UnitAvatar"), typeof(int) }) &&
            CloudValid(cloudCode), "Dark Cloud limiter binds the native player strike, callback and FX contract");
        foreach (string anchor in new[] { "get_Networkavatar", "ApplyDamage", "Invoke", "TargetCreateFx", "isPlayerCombo", "OnAttackUnit" })
        {
            var changed = cloudCode.Where(i => !(i.operand is MemberInfo member && member.Name == anchor)).ToList();
            Require(!CloudValid(changed), "Dark Cloud limiter rejects missing native anchor: " + anchor);
        }
        var callbackMoved = cloudCode.Select(i => new CodeInstruction(i)).ToList();
        int invokeIndex = callbackMoved.FindIndex(i => i.operand is MethodInfo m && m.Name == "Invoke");
        var callbackInstruction = callbackMoved[invokeIndex]; callbackMoved.RemoveAt(invokeIndex); callbackMoved.Insert(0, callbackInstruction);
        Require(!CloudValid(callbackMoved), "Dark Cloud limiter rejects callback-before-damage contract changes");
        Require(PatchProcessor.GetOriginalInstructions(Method("ComboEffect_DarkCloud", "UseCloudToTarget"))
            .Any(i => i.Calls(cloud)), "Targeted staff discharges pass through the same strike boundary");
        var effectHooks = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        var coroutine = (MethodInfo)AccessTools.Method(effectHooks, "Iterator").Invoke(null,
            new object[] { Native("ComboEffect_DarkCloud"), "UseCloudCoroutine" })!;
        Require(PatchProcessor.GetOriginalInstructions(coroutine).Any(i => i.Calls(cloud)),
            "Automatic and multishot cloud discharges pass through the same strike boundary");
        Require(PatchProcessor.GetOriginalInstructions(Method("Charm_BlazingStormcloud", "InflictBurn"))
            .Any(i => Calls(i, "UnitAvatar", "ApplyDebuff")), "Native cloud attack callback can apply burn after damage");
        Check(Method("PlayerInputController", "SearchTargetNearestPoint"), "RewriteNearestMask", "OffensiveNearestMask", 1);
        Check(Method("DaggerGrowthBullet", "HitCheck"), "RewriteDagger", "ItemHitTarget", 1);
        Check(Method("Bullet", "Update"), "RewriteHoming", "HomingTargetAllowed", 1, 2);
        Check(Method("Charm_AttackChim", "HandleAddedDebuffOnTarget"), "RewriteDebuffProc", "ApplyArtifactDebuff", 1);

        var bat = PatchProcessor.GetOriginalInstructions(Method("Charm_IceBat", "OnUpdate")).ToList();
        Require(bat.Any(i => Calls(i, "CharacterDebuff", "CompareID")) && bat.Any(i => Calls(i, "CharacterDebuff", "get_NetworkAttacker")) &&
            bat.Any(i => i.LoadsConstant(144f)) && bat.Any(i => Calls(i, "Charm_IceBat", "RpcCreateIceBatBullet")),
            "bat retains owned frostbite, 12-unit range and native projectile RPC");
        var bullet = PatchProcessor.GetOriginalInstructions(Method("Charm_IceBat_Projectile", "ApplyDamageOnServer"));
        Require(bullet.Any(i => Calls(i, "CombatBehaviour", "ApplyDamage")), "bat impact reaches central damage rules");
        var homing = Rewrite("RewriteHoming", Method("Bullet", "Update"), PatchProcessor.GetOriginalInstructions(Method("Bullet", "Update")).ToList());
        Require(homing.Count(i => Calls(i, "FriendlyFireRuntime", "HomingTargetLost")) == 1 &&
            homing.Any(i => Calls(i, "Bullet", "set_HomingTarget")), "native homing clear/reacquisition stays in the same update");
        var battle = PatchProcessor.GetOriginalInstructions(Method("PlayerBattleChecker", "Update"));
        Require(battle.Any(i => i.operand is MethodInfo m && m.Name == "OverlapCircle"), "native combat checker remains separately owned");
        Console.WriteLine("Verified 15 artifact selectors, six offense-only combat gates, scoped native aim, artifact homing/loss, bounded proc callsites and owned-frostbite/RPC contracts (not live multiplayer).");
    }
}
