using System.Reflection;
using HarmonyLib;

internal static class GameFriendlyFireSpellTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Native(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Native(type), name);
        List<CodeInstruction> Code(string type, string name) => PatchProcessor.GetOriginalInstructions(Method(type, name)).ToList();
        bool Calls(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Field(CodeInstruction i, string name) => i.operand is FieldInfo f && f.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Spell hostility contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.FriendlyFireSpellHooks", true)!;
        List<CodeInstruction> Rewrite(string name, List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, name).Invoke(null, new object[] { code })!).ToList();
        void Check(MethodInfo method, string rewrite, string helper)
        {
            var code = PatchProcessor.GetOriginalInstructions(method).ToList();
            var mapped = Rewrite(rewrite, code);
            Require(mapped.Count == code.Count && mapped.Count(i => Calls(i, "FriendlyFireRuntime", helper)) == 1,
                method + " changes only one audited selector");
            int anchor = code.FindIndex(i => rewrite == "RewriteNearest" ? Calls(i, "PlayerInputController", "SearchTargetNearestPoint") :
                Calls(i, "CombatManager", "ContainsAttackableFaction"));
            Require(anchor >= 0, method + " selector exists");
            code.RemoveAt(anchor);
            bool rejected = false;
            try { Rewrite(rewrite, code); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, method + " rejects a changed filter");
        }
        var selectors = ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, "MagicSelectors").Invoke(null, null)!).ToArray();
        Require(selectors.Length == 2, "two audited offensive spell coroutines");
        foreach (var method in selectors) Check(method,
            method.DeclaringType?.DeclaringType == Native("ActiveSkill_FireBulletsToEnemies") ? "RewriteVolley" : "RewriteMagic", "ItemHitTarget");
        var volley = selectors.Single(m => m.DeclaringType?.DeclaringType == Native("ActiveSkill_FireBulletsToEnemies"));
        var volleyCode = PatchProcessor.GetOriginalInstructions(volley).ToList();
        Require(Rewrite("RewriteVolley", volleyCode).Count(i => Calls(i, "FriendlyFireRuntime", "SpellOverlapAvatar")) == 1,
            "only the volley resolves player costume hitboxes");
        volleyCode.RemoveAt(volleyCode.FindIndex(i => i.operand is MethodInfo m && m.Name == "GetComponent" && m.IsGenericMethod && m.GetGenericArguments()[0] == Native("UnitAvatar")));
        bool missingLookupRejected = false;
        try { Rewrite("RewriteVolley", volleyCode); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { missingLookupRejected = true; }
        Require(missingLookupRejected, "volley rejects a changed collider-resolution path");
        Check(Method("ActiveSkill_Ball", "Update"), "RewriteNearest", "OffensiveNearestPoint");
        Check(Method("AutoMagicCaster", "SearchTarget"), "RewriteAuto", "ItemHitTarget");
        Check(Method("DungeonManager", "SearchTargetForChainLightning"), "RewriteChain", "ItemTarget");

        var bolt = Code("ActiveSkill_Bolt", "OnServerUpdate");
        Require(bolt.Any(i => i.LoadsConstant("BOLTMAGICHOMING")) && bolt.Count(i => Calls(i, "Bullet", "TurnOnHoming")) == 2,
            "single and multishot Bolt retain the native homing-stat gate");
        foreach (string spell in new[] { "ActiveSkill_Arrow", "ActiveSkill_ArrowRain", "ActiveSkill_FireBullet" })
        {
            var code = Code(spell, "OnServerUpdate");
            Require(code.Any(i => Field(i, "turnOnHoming")) && code.Any(i => Calls(i, "Bullet", "TurnOnHoming")),
                spell + " owns its native homing gate");
        }
        var update = Code("Bullet", "Update");
        int server = update.FindIndex(i => Calls(i, "NetworkBehaviour", "get_isServer"));
        int acquire = update.FindIndex(i => Calls(i, "CombatManager", "ContainsAttackableFaction"));
        int move = update.FindIndex(i => Calls(i, "BulletMoveModule", "Move"));
        Require(server >= 0 && server < acquire && acquire < move && update.Any(i => Field(i, "isHomingTargetEnabled")) &&
            update.Any(i => Field(i, "autoDetectHomingTarget")) && update.Any(i => Field(i, "homingDetectLimitRange")) &&
            update.Any(i => Field(i, "ignored")), "native host acquisition precedes movement and keeps all homing constraints");
        var despawn = Code("Bullet", "OnDespawn");
        Require(despawn.Any(i => Calls(i, "Bullet", "set_HomingTarget")) && Code("Bullet", "DestroySelf").Any(i => Calls(i, "Bullet", "set_NetworkOwner")),
            "despawn clears the target and native destruction releases its owner");
        var init = Code("Bullet", "Initialize");
        Require(init.Any(i => Field(i, "damageId")) && init.Any(i => Calls(i, "Bullet", "set_FromType")),
            "pooled projectile damage identity is initialized anew");
        var auto = Code("AutoMagicCaster", "Update");
        Require(auto.Any(i => Calls(i, "NetworkServer", "get_active")) && auto.Any(i => Field(i, "aimedEnemy")) &&
            AccessTools.Field(Native("AutoMagicCaster"), "owner").FieldType == Native("UnitAvatar") &&
            AccessTools.Field(Native("AutoMagicCaster"), "aimedEnemy").FieldType == Native("UnitAvatar"),
            "cached automatic target has a host-only update and exact owner/target fields");
        var chain = Code("DungeonManager", "SearchTargetForChainLightning");
        Require(chain.Any(i => i.LoadsConstant(6f)) && chain.Any(i => Field(i, "canBeTarget")) &&
            chain.Any(i => i.operand is MethodInfo m && m.Name == "Contains"), "chain lightning preserves range, targetability and already-hit list");
        Console.WriteLine("Verified spell homing gates, host acquisition/movement, pooled identity reset, five offensive selectors and changed-IL rejection (not live multiplayer).");
    }
}
