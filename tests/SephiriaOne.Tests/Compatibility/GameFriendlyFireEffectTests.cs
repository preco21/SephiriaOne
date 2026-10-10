using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireEffectTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string method) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method);
        void Require(bool condition, string why) { if (!condition) throw new Exception("Friendly-fire effect contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        var selectors = ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, "Selectors").Invoke(null, null)!).ToArray();
        Require(selectors.Length == 15 && selectors.All(m => m != null), "all audited native item selectors resolve");
        foreach (var selector in selectors)
        {
            var code = PatchProcessor.GetOriginalInstructions(selector).ToList();
            var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "RewriteSelector").Invoke(null, new object[] { code })!).ToList();
            Require(code.Count == rewritten.Count && Calls(rewritten, "FriendlyFireRuntime", "ItemTarget"), selector + " retains the native body and replaces only its filter");
            Require(rewritten.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "FriendlyFireRuntime") == 1, "exactly one target helper");
            var changed = code.Select(i => new CodeInstruction(i)).ToList();
            changed.RemoveAt(changed.FindIndex(i => i.operand is MethodInfo m && m.Name == "GetHostileFactionLayers"));
            bool rejected = false;
            try { _ = AccessTools.Method(hooks, "RewriteSelector").Invoke(null, new object[] { changed }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, selector + " rejects a changed target contract");
        }
        var chakram = Code("Charm_FireChakram", "OnUpdate");
        var mappedChakram = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "RewriteChakram").Invoke(null, new object[] { chakram })!).ToList();
        Require(Calls(mappedChakram, "FriendlyFireRuntime", "ItemTargetMask") && Calls(mappedChakram, "UnitAvatar", "ApplyDebuff") &&
            Calls(mappedChakram, "CombatBehaviour", "ApplyDamage") && Calls(mappedChakram, "UnitAvatar", "GetHostileFactionLayers"),
            "chakram changes only the victim mask and retains attack mask/damage/debuff calls");
        var apply = Code("UnitAvatar", "ApplyDebuff");
        Require(apply.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "DEBUFFIMMUNITY")) &&
            Calls(apply, "CharacterDebuff", "InitializeAndSpawn") && Calls(apply, "CharacterDebuff", "AddStack") &&
            !Calls(apply, "CombatManager", "ContainsAttackableFaction"), "native immunity and stacking have no player-faction veto");
        Require(Calls(Code("CharacterDebuff", "InitializeAndSpawn"), "NetworkServer", "Spawn"), "debuffs replicate using stock native prefabs");
        var update = Code("CharacterDebuff", "Update");
        Require(Calls(update, "CharacterDebuff", "OnUpdate_Server") && Calls(update, "CharacterDebuff", "Destroy"), "native Update owns ticks and expiry");
        var destroy = Code("CharacterDebuff", "Destroy");
        Require(Calls(destroy, "CharacterDebuff", "RemoveStatus") && Calls(destroy, "CharacterDebuff", "DestroyInner"), "native ending removes statuses before expiry damage");
        Require(Calls(Code("CharacterDebuff", "AddStack"), "CharacterDebuff", "RefreshDurationTimer"), "stack scope contains synchronous electric refresh");
        Require(Code("UnitAvatar", "Die").Any(i => i.operand is MethodInfo m && m.Name == "SetLeader"), "companion death can clear ownership before lingering ticks");
        Require(Calls(Code("WeaponAddonCommon_DebuffAttack", "InflictDebuff"), "UnitAvatar", "ApplyDebuff") &&
            Calls(Code("ComboEffect_Debuff", "OnAttackUnit"), "UnitAvatar", "ApplyDebuff"), "native on-hit effect routes remain active");
        foreach (string type in new[] { "CharacterDebuff_Burn", "CharacterDebuff_Poison", "CharacterDebuff_Frostbite", "CharacterDebuff_Plasma" })
            Require(Calls(Code(type, "OnUpdate_Server"), "CombatBehaviour", "ApplyDamage"), type + " ticks pass through shared damage scaling");
        Require(Calls(Code("CharacterDebuff_Electric", "RefreshDurationTimer"), "CharacterDebuff_Electric", "Attack") &&
            Calls(Code("CharacterDebuff_Electric", "Attack"), "CombatBehaviour", "ApplyDamage"), "electric stacking can damage synchronously inside ApplyDebuff");
        var explosion = Code("Charm_BurnExplosion", "CreateExplosion");
        Require(explosion.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "BURN")) &&
            Calls(explosion, "CombatBehaviour", "ApplyDamage") && Calls(explosion, "Charm_Basic", "get_NetworkAvatar"), "burning-death explosion uses its item owner");
        Require(PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(Type("Charm_BurnExplosion"))).Any(i =>
            i.opcode == OpCodes.Ldstr && Equals(i.operand, "Charm_BurnExplosion")), "audited explosion identifier");
        Require(Code("PlayerAvatar", "get_Name").Any(i => i.operand is FieldInfo f && f.Name == "playerNameSource"), "PvP name source is the runtime player name");
        GameFriendlyFireArtifactTests.Run(game, addon);
        Console.WriteLine("Verified audited burn/debuff target filters, stock debuff replication, nested electric damage, burn explosion identity and native player names (not live multiplayer).");
    }
}
