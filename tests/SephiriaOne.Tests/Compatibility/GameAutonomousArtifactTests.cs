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
        var classifier = Code(catalog, "Index");
        foreach (var (type, id) in ids)
        {
            var native = game.GetType(type, true)!;
            var code = type == "Charm_FireFeather" ? Code(native, "OnUpdate") :
                PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredConstructor(native)).ToList();
            Require(code.Any(i => i.LoadsConstant(id)) && classifier.Any(i => i.LoadsConstant(id)), "Native and catalog agree on " + id);
        }
        Require(classifier.Count(i => i.operand is string s && s.StartsWith("Charm_")) == 7 &&
            classifier.Any(i => i.operand is FieldInfo f && f.Name == "fromType") &&
            classifier.Any(i => i.operand is FieldInfo f && f.Name == "damageType"), "Classifier is an exact seven-source/from-type/damage-type allowlist");
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
        var cloud = Code(runtime, "DarkCloudInterval");
        Require(Calls(Code(runtime, "BeforeDarkCloudStrike"), "FriendlyFireRuntime", "DarkCloudInterval") &&
            Calls(cloud, "Math", "Max") && Calls(cloud, "PvpDotSettings", "Interval") && Calls(cloud, "PvpArtifactRateSettings", "get_Enabled"),
            "Cloud composes max interval at its whole-strike boundary");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var build = Code(panel, "BuildPvpArtifactRate");
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
