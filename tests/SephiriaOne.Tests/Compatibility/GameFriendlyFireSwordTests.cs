using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireSwordTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Native(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Native(type), name);
        List<CodeInstruction> Code(string type, string name) => PatchProcessor.GetOriginalInstructions(Method(type, name)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string name) => code.Any(i =>
            i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name);
        void Require(bool value, string why) { if (!value) throw new Exception("Secondary sword contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.FriendlyFireArtifactHooks", true)!;
        var rewrite = AccessTools.Method(hooks, "RewriteIceSword");
        List<CodeInstruction> Map(List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        var sword = Code("IceSword", "Update");
        var mapped = Map(sword);
        Require(Calls(mapped, "CombatBehaviour", "ApplyDamage") && Calls(mapped, "Charm_IceSword", "RequestHitSword") &&
            Calls(mapped, "HorayPhysics2D", "OverlapCircle") && Calls(mapped, "Timer", "Update") &&
            mapped.Any(i => i.LoadsConstant("FROSTRELIC")) &&
            Calls(mapped, "UnitAvatar", "GetHostileFactionLayers") &&
            mapped.Any(i => i.operand is FieldInfo f && f.Name == "isLastMove") &&
            mapped.Any(i => i.operand is FieldInfo f && f.Name == "isServerObject") &&
            mapped.Any(i => i.operand is FieldInfo f && f.Name == "attackIntervalTime"),
            "orbit and outward flight retain native overlap, cadence, server guard, hit attribution, elemental flag and hit RPC");
        Require(PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(Native("IceSword")))
            .Any(i => i.LoadsConstant("Charm_PlitviceWaterDrop")), "native Plitvice damage identity");
        // Inline mask and the shared helper's native fallback are equivalent.
        var contains = Code("CombatManager", "ContainsAttackableFaction");
        Require(Calls(contains, "RuntimeFactionManager", "FindFactionLayer") && contains.Count(i => i.opcode == OpCodes.And) == 1,
            "native ContainsAttackableFaction is the same faction-layer intersection");
        foreach (string anchor in new[] { "parentCharm", "get_NetworkAvatar", "get_Instance", "FindFactionLayer", "faction" })
        {
            var changed = sword.Select(i => new CodeInstruction(i)).ToList();
            int index = changed.FindIndex(i => i.operand is MemberInfo m && m.Name == anchor);
            Require(index >= 0, "inline selector anchor exists: " + anchor);
            // parentCharm occurs earlier too: remove the anchor nearest the mask.
            int layer = changed.FindIndex(i => i.operand is MethodInfo m && m.Name == "FindFactionLayer");
            index = changed.FindLastIndex(layer, i => i.operand is MemberInfo m && m.Name == anchor);
            changed.RemoveAt(index);
            bool rejected = false;
            try { Map(changed); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, "reject changed source/mask contract: " + anchor);
        }
        var duplicate = sword.Select(i => new CodeInstruction(i)).ToList();
        duplicate.Add(new CodeInstruction(OpCodes.Callvirt, Method("RuntimeFactionManager", "FindFactionLayer")));
        bool duplicateRejected = false;
        try { Map(duplicate); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { duplicateRejected = true; }
        Require(duplicateRejected, "reject additional unreviewed inline masks");
        var consumer = sword.Select(i => new CodeInstruction(i)).ToList();
        int consumerIndex = consumer.FindIndex(i => i.operand is MethodInfo m && m.Name == "FindFactionLayer") + 2;
        consumer[consumerIndex].opcode = OpCodes.Pop; consumer[consumerIndex].operand = null;
        bool consumerRejected = false;
        try { Map(consumer); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { consumerRejected = true; }
        Require(consumerRejected, "reject a mask consumer that no longer tests zero/nonzero");

        var create = Code("Charm_IceSword", "UserCode_RpcCreateSwords__Int32__Single");
        Require(Calls(create, "IceSword", "Initialize") && Calls(create, "NetworkBehaviour", "get_isServer"),
            "native replicated sword creation initializes only host copies for authoritative damage");
        Require(Calls(Code("Charm_IceSword", "OnFireFrostRelicGlobal"), "UnitAvatar", "UseMp"), "native relic activation retains MP cost");
        Require(Calls(Code("Charm_IceSword", "RequestHitSword"), "Charm_IceSword", "RpcHitSword"), "native successful hit still broadcasts FX");

        var effects = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        List<CodeInstruction> Iterator(string type, string method) => PatchProcessor.GetOriginalInstructions((MethodInfo)
            AccessTools.Method(effects, "Iterator").Invoke(null, new object[] { Native(type), method })!).ToList();
        var search = Code("ComboEffect_FlameSword", "SearchTarget");
        Require(Calls(search, "Vector3", "Distance") && search.Any(i => i.LoadsField(AccessTools.Field(Native("UnitAvatar"), "IsDead"))) &&
            Calls(search, "GameObject", "get_activeSelf"), "Flame Sword keeps radius, dead and inactive-target checks");
        Require(Calls(Iterator("ComboEffect_FlameSword", "AutoSearchNearEnemyAndFireSword"), "ComboEffect_FlameSword", "SearchTarget"),
            "secondary Flame Sword coroutine uses the registered selector");
        Require(Calls(Iterator("ComboEffect_FlameSword", "CreateFlameSword"), "ProjectileSpawner`1", "Spawn"),
            "delayed Flame Sword damage remains native pooled melee collision");
        Require(Calls(Code("FlameSwordReturn", "Update"), "CombatBehaviour", "ApplyDamage"), "return swords enter central damage handling");
        Require(Calls(Code("SpecialProjectile_FlameSword", "OnUpdateServer"), "ProjectileSpawner`1", "Spawn"), "special flame sword retains native child collision creation");
        Require(Calls(Code("IceGuillotine", "OnGuillotineHit"), "CombatBehaviour", "ApplyDamage"), "Frost execution weapon enters central damage handling");
        Console.WriteLine("Verified native Plitvice/Flame Sword collision, creation, masks, timing and RPC boundaries (not live multiplayer).");
    }
}
