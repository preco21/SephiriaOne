using System.Reflection;
using HarmonyLib;

internal static class GameAutonomousArtifactTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool ok, string why) { if (!ok) throw new Exception("Autonomous artifact contract: " + why); }
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, name)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string name) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name);
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var catalog = addon.GetType("SephiriaOne.AutonomousArtifactCatalog", true)!;
        var ids = new[] {
            ("Charm_Guillotine", "Charm_Guillotine"), ("Charm_ElectricEarring", "Charm_ElectricEarring"),
            ("Charm_FlameGround_Meteor", "Charm_Meteor"), ("Charm_FireFeather", "Charm_FireFeather"),
            ("Charm_FireChakram", "Charm_Chakram"), ("Charm_EchoOfTheGlacier", "Charm_EchoOfTheGlacier"), ("Charm_IceBat", "Charm_IceBat") };
        var classifier = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(catalog, "Index", new[] { typeof(string) })).ToList();
        var typedClassifier = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(catalog, "Index", new[] { game.GetType("DamageInstance", true)! })).ToList();
        foreach (var (type, id) in ids)
        {
            var native = game.GetType(type, true)!;
            var code = type == "Charm_FireFeather" ? Code(native, "OnUpdate") :
                PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredConstructor(native)).ToList();
            Require(code.Any(i => i.LoadsConstant(id)) && classifier.Any(i => i.LoadsConstant(id)), "Native and catalog agree on " + id);
        }
        Require(classifier.Count(i => i.operand is string s && s.StartsWith("Charm_")) == 7 &&
            typedClassifier.Any(i => i.operand is FieldInfo f && f.Name == "fromType") &&
            typedClassifier.Any(i => i.operand is FieldInfo f && f.Name == "damageType") &&
            Calls(typedClassifier, "AutonomousArtifactCatalog", "Index"), "Classifier is an exact seven-source/from-type/damage-type allowlist");
        foreach (var (type, method) in new[] { ("IceGuillotine", "OnGuillotineHit"), ("Charm_IceBat_Projectile", "ApplyDamageOnServer"),
            ("Charm_EchoOfTheGlacier", "CreateFrostbite"), ("Charm_FireChakram", "OnUpdate") })
        {
            var code = Code(game.GetType(type, true)!, method);
            Require(Calls(code, "DamageInstance", "GetDamage") && Calls(code, "CombatBehaviour", "ApplyDamage"), "Direct emitter enters shared impact hook: " + type);
        }
        var guillotine = Code(game.GetType("IceGuillotine", true)!, "OnGuillotineHit");
        Require(guillotine.Any(i => i.operand is FieldInfo f && f.Name == "isFireRelic") &&
            guillotine.Any(i => i.operand is FieldInfo f && f.Name == "damageId"), "Both elemental variants share the same serialized identity");
        Require(Calls(Code(runtime, "BeforeHit"), "FriendlyFireRuntime", "AutonomousArtifactReady") &&
            Calls(Code(runtime, "AutonomousArtifactReady"), "FriendlyFireRuntime", "CurrentBalanceWindow") &&
            Calls(Code(runtime, "LimitBalancedDamage"), "FriendlyFireRuntime", "CurrentBalanceWindow"), "Early and positive-damage admission use the same pair lifecycle");
        var scope = Code(runtime, "CurrentBalanceWindow");
        foreach (string field in new[] { "CombatEpoch", "ParticipantEpoch", "Revision", "SourceLife", "TargetLife", "SourceFloor", "TargetFloor" })
            Require(scope.Any(i => i.operand is FieldInfo f && f.Name == field), "Artifact clocks share lifecycle boundary " + field);
        Require(Calls(scope, "BalanceWindow", "ResetArtifacts"), "Reset retires artifact clocks with budget and debuffs");
        var cloud = Code(runtime, "BeforeDarkCloudStrike");
        Require(Calls(cloud, "FriendlyFireRuntime", "WithinAutonomousArtifactRange") &&
            Calls(Code(runtime, "BeforeHit"), "FriendlyFireRuntime", "ArtifactHitWithinRange") &&
            Calls(Code(runtime, "HitPermitted"), "FriendlyFireRuntime", "ArtifactHitWithinRange"),
            "Owner range is rechecked before cloud callbacks and at early/final impact boundaries");
        Require(Calls(Code(runtime, "HomingTargetAllowed"), "FriendlyFireRuntime", "AutonomousHomingWithinRange") &&
            Calls(Code(runtime, "HomingTargetLost"), "FriendlyFireRuntime", "AutonomousHomingWithinRange"),
            "Autonomous homing uses live owner range for acquisition and cached targets");
        var artifactHooks = addon.GetType("SephiriaOne.FriendlyFireArtifactHooks", true)!;
        var debuffCallers = ((IEnumerable<MethodInfo>)AccessTools.Method(artifactHooks, "AutonomousDebuffCallers").Invoke(null, null)!).ToArray();
        Require(debuffCallers.Length == 5 && debuffCallers.All(m => m != null), "All audited post-hit autonomous debuff callbacks resolve");
        foreach (var caller in debuffCallers)
        {
            var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(artifactHooks, "RewriteAutonomousDebuff").Invoke(null,
                new object[] { PatchProcessor.GetOriginalInstructions(caller) })!).ToList();
            Require(Calls(rewritten, "FriendlyFireRuntime", "ApplyAutonomousArtifactDebuff") && !Calls(rewritten, "UnitAvatar", "ApplyDebuff"),
                "Post-hit status applies through live range check: " + caller);
        }
        var cloudBody = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("ComboEffect_DarkCloud", true)!, "FireLightning"));
        var cloudRewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(artifactHooks, "RewriteDarkCloudCallback").Invoke(null, new object[] { cloudBody })!).ToList();
        Require(Calls(cloudRewritten, "FriendlyFireRuntime", "InvokeDarkCloudAttack"), "Cloud post-hit callbacks recheck owner radius");
        var rangeHooks = addon.GetType("SephiriaOne.FriendlyFireArtifactRangeHooks", true)!;
        var sender = AccessTools.DeclaredMethod(game.GetType("Charm_Guillotine", true)!, "RpcCreateGuillotine");
        Require((bool)AccessTools.Method(rangeHooks, "ValidateGuillotineSender").Invoke(null,
            new object[] { PatchProcessor.GetOriginalInstructions(sender) })!, "Guillotine range guard precedes native network publication");
        var effectHooks = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        var earring = (MethodInfo)AccessTools.Method(effectHooks, "Iterator").Invoke(null,
            new object[] { game.GetType("Charm_ElectricEarring", true)!, "AttackCoroutine" })!;
        var earringCode = PatchProcessor.GetOriginalInstructions(earring);
        var pruned = ((IEnumerable<CodeInstruction>)AccessTools.Method(rangeHooks, "RewriteEarringTargets").Invoke(null, new object[] { earringCode, earring })!).ToList();
        Require(Calls(pruned, "FriendlyFireRuntime", "PruneAutonomousTargets") && Calls(pruned, "List`1", "get_Count"),
            "Earring uses native count gate after cached-target range pruning");
        Require(Calls(cloud, "PvpDotSettings", "Interval") && !Calls(cloud, "FriendlyFireSettings", "get_ArtifactRate"),
            "Cloud uses its own interval at the whole-strike boundary independently of the artifact limiter");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var build = Code(panel, "BuildPvpArtifactRate");
        Require(Calls(Code(panel, "BuildPvpDotEditor"), "SettingsPanel", "BuildPvpArtifactRange") &&
            Calls(Code(panel, "RefreshPvpDots"), "SettingsPanel", "RefreshPvpArtifactRange") &&
            Calls(Code(panel, "ClearPvpDots"), "SettingsPanel", "ClearPvpArtifactRange"), "Range shares existing panel lifecycle");
        Require(build.Any(i => i.LoadsConstant("Autonomous artifact hit limit")) && build.Any(i => i.LoadsConstant("Minimum hit interval (s)")) &&
            Calls(build, "SettingsPanel", "AddCheckbox"), "Panel has separate toggle and exact interval");
        Require(Calls(Code(panel, "BuildPvpDotEditor"), "SettingsPanel", "BuildPvpArtifactRate") &&
            Calls(Code(panel, "RefreshPvpDots"), "SettingsPanel", "RefreshPvpArtifactRate"), "Artifact block shares existing scroll and refresh");
        foreach (var (name, command) in new[] { ("ApplyPvpArtifactRate", "/one friendlyfire artifact-rate interval "), ("ResetPvpArtifactRate", "/one friendlyfire artifact-rate reset") })
            Require(Code(panel, name).Any(i => i.LoadsConstant(command)) && Calls(Code(panel, name), "SettingsPanel", "Execute") &&
                Calls(Code(panel, name), "SettingsPanel", "ClearPvpArtifactRate"), "Panel routes through validated host commands");
        var refresh = Code(panel, "RefreshPvpArtifactRate");
        Require(Calls(refresh, "SettingsSnapshot", "get_CanMutate") && Calls(refresh, "SettingsSnapshot", "get_FriendlyFireAvailable") &&
            refresh.Any(i => i.operand is FieldInfo f && f.Name == "pvpArtifactDraft") &&
            refresh.Any(i => i.operand is FieldInfo f && f.Name == "pvpArtifactLast"), "Host availability, draft preservation and cached formatting");
        Require(Calls(Code(panel, "ClearPvpDots"), "SettingsPanel", "ClearPvpArtifactRate") &&
            Code(panel, "BuildPage").Any(i => i.operand is FieldInfo f && f.Name == "pvpArtifactInterval") &&
            !Code(panel, "ClearPvpArtifactRate").Any(i => i.operand is FieldInfo f && f.Name == "pvpDotDurationDraft"), "Control lifetime and draft isolation");
        Console.WriteLine("Verified installed autonomous artifact identities/impact paths and shared lifecycle/UI wiring (static contracts, not live multiplayer).");
    }
}
