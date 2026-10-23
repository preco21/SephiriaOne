using System.Reflection;
using HarmonyLib;

internal static class GameIceWeaponTargetingTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Native(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Native(type), name);
        List<CodeInstruction> Code(string type, string name) => PatchProcessor.GetOriginalInstructions(Method(type, name)).ToList();
        bool Calls(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Ice Weapon targeting audit: " + why); }
        var effects = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        MethodInfo Iterator(string type, string method) => (MethodInfo)AccessTools.Method(effects, "Iterator").Invoke(null, new object[] { Native(type), method })!;
        var selectors = ((IEnumerable<MethodInfo>)AccessTools.Method(effects, "Selectors").Invoke(null, null)!).ToArray();
        foreach (var pair in new[] { ("Charm_IceSpear", "SearchTarget"), ("Charm_IceSpear", "SearchTargetByWeaponDirection"),
            ("Charm_IceHammer", "SearchTarget"), ("Charm_Guillotine", "FindTargetsInRange") })
            Require(selectors.Contains(Method(pair.Item1, pair.Item2)), pair + " uses the common hostile-pair adapter");

        var spear = PatchProcessor.GetOriginalInstructions(Iterator("Charm_IceSpear", "FireSpearCoroutine")).ToList();
        Require(spear.Any(i => Calls(i, "Charm_IceSpear", "SearchTarget")) &&
            spear.Any(i => Calls(i, "Charm_IceSpear", "SearchTargetByWeaponDirection")),
            "both ordinary and weapon-triggered spear launches retain their patched native search");
        var hammer = PatchProcessor.GetOriginalInstructions(Iterator("Charm_IceHammer", "HammerCoroutine")).ToList();
        Require(hammer.Any(i => Calls(i, "Charm_IceHammer", "SearchTarget")), "hammer launch uses patched selector");
        var update = Code("Charm_IceHammer", "OnUpdate");
        Require(update.Count(i => Calls(i, "UnitAvatar", "get_CurrentLookingPosition")) == 1 &&
            update.Count(i => Calls(i, "Bullet", "set_HomingTargetPos")) == 1 &&
            update.Any(i => i.LoadsConstant("ICEHAMMERSCYTHE")), "scythes are steered by live owner aim");
        var move = Code("BulletMoveModule_IceScythe", "OnMove");
        Require(move.Any(i => Calls(i, "Bullet", "get_HomingTargetPos")) &&
            !move.Any(i => Calls(i, "CombatManager", "ContainsAttackableFaction")),
            "scythe movement consumes aim without a separate faction search");
        Require(Code("Charm_IceHammer", "OnBeginDash").Any(i => Calls(i, "ChargingCharm", "ActivateChargingCharm")),
            "ordinary hammer still needs its native dash activation");
        Require(Code("Charm_IceBow", "FireCastingServer").Any(i => Calls(i, "ChargingCharm", "ActivateChargingCharm")),
            "Frozen Bow still needs native activation; it is not a passive auto-caster");
        Require(Code("Charm_AirSlash", "HandleSwing").Any(i => Calls(i, "ChargingCharm", "ActivateChargingCharm")),
            "Scabbard retains weapon-swing trigger");
        Require(Code("Charm_IceSword", "OnUpdate").Any(i => Calls(i, "Charm_IceSword", "RpcActivateLastMove")) &&
            Code("IceSword", "Update").Any(i => i.operand is FieldInfo f && f.Name == "lastMoveDirection"),
            "Plitvice uses timed orbit/outward motion rather than acquiring an enemy");

        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var pairPolicy = PatchProcessor.GetOriginalInstructions(AccessTools.Method(runtime, "CanAffectTeam"));
        Require(pairPolicy.Any(i => Calls(i, "CombatMatchRuntime", "AllowsPair")) &&
            pairPolicy.Any(i => Calls(i, "FriendlyFireParticipants", "Includes")), "all selectors share mode and participant rules");
        Console.WriteLine("Verified Ice Weapon native activation, separate selectors, manual scythe steering and Plitvice motion contracts (not live Unity behavior).");
    }
}
