using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameHitFrequencyTests
{
    internal static void Run(Assembly addon)
    {
        Type Type(string name) => addon.GetType("SephiriaOne." + name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Call(CodeInstruction i, string type, string method) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method;
        void Require(bool value, string why) { if (!value) throw new Exception("PvP hit frequency: " + why); }
        var limit = Code("FriendlyFireRuntime", "LimitBalancedDamage");
        int frequency = limit.FindIndex(i => Call(i, "FriendlyFireHitFrequency", "Next"));
        int budget = limit.FindIndex(i => Call(i, "FriendlyFireDamageBudget", "Take"));
        Require(frequency >= 0 && budget > frequency && limit.Take(frequency).Any(i => Call(i, "DebuffPulse", "Ready")),
            "pulse admission precedes frequency scaling, which precedes the shared damage budget");
        Require(Code("FriendlyFireRuntime", "Scale").Any(i => Call(i, "FriendlyFireSettings", "get_HitFrequency")),
            "runtime reads the effective current policy");
        var panel = Code("SettingsPanel", "BuildPvpRapidEditor");
        Require(panel.Any(i => i.LoadsConstant("Hit-frequency scaling")) && panel.Any(i => i.LoadsConstant("/one friendlyfire hit-frequency")) &&
            panel.Any(i => Call(i, "SettingsPanel", "AddCheckbox")), "new checkbox uses the shared host command and checkbox refresh lifecycle");
        var record = Code("SessionSettings", "RecordFriendlyFire");
        Require(record.Any(i => Call(i, "SessionSettings", "set_CombatFrequencyRevision")) &&
            record.Any(i => Call(i, "SessionSettings", "set_CombatBalanceRevision")), "independent frequency revision coexists with balance revision");
        var frequencyType = Type("FriendlyFireHitFrequency");
        Require(frequencyType.IsValueType && frequencyType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Count(f => f.FieldType.IsArray) == 1,
            "rate storage is a bounded per-pair ring, not unbounded hit history");
        Console.WriteLine("Verified hit-frequency policy/UI wiring and pulse -> frequency -> damage-budget ordering (not live Unity gameplay).");
    }
}
