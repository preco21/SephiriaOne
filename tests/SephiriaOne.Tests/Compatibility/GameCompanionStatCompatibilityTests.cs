using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SephiriaOne;

internal static class GameCompanionStatCompatibilityTests
{
    internal static void Run(Assembly game)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Companion stat native contract: " + why); }
        List<CodeInstruction> Code(string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.Method(game.GetType(type, true)!, method)).ToList();
        bool Calls(CodeInstruction code, string name) => code.operand is MethodInfo method && method.Name == name;
        void OwnerRead(List<CodeInstruction> code, string key, string owner, string getter)
        {
            int index = code.FindIndex(i => i.LoadsConstant(key));
            Require(index > 0 && index + 1 < code.Count && Calls(code[index - 1], owner) && Calls(code[index + 1], getter),
                key + " must read the live owner's stat");
        }
        var attack = Code("UnitAvatar", "GetBonusAttackSpeed");
        OwnerRead(attack, "FOLLOWERATTACKSPEED", "get_NetworkLeader", "GetCustomStatUnsafe");
        Require(attack.Any(i => i.opcode == OpCodes.Add), "Owner attack speed remains additive to companion's own speed");
        var damage = Code("UnitAvatar", "ApplyDamage");
        foreach (string key in new[] { "FOLLOWERCRITICAL", "FOLLOWERCRITICALCONTRIBUTE", "FOLLOWERDAMAGE", "FOLLOWERDEFENSE" })
            OwnerRead(damage, key, "get_NetworkLeader", key == "FOLLOWERCRITICAL" ? "GetCustomStat" : "GetCustomStatUnsafe");
        int critical = damage.FindIndex(i => i.LoadsConstant("FOLLOWERCRITICAL"));
        Require(damage[critical + 2].opcode == OpCodes.Conv_R4 && damage[critical + 3].LoadsConstant(100f) &&
            damage[critical + 4].opcode == OpCodes.Div, "Companion critical chance uses hundredths of percentage points");
        foreach (string type in new[] { "Charm_LeadNPC", "Charm_SummonUnit", "Charm_MiniBallista" })
        {
            var update = Code(type, "OnUpdate");
            OwnerRead(update, "FOLLOWERREVIVE", "get_NetworkAvatar", "GetCustomStatUnsafe");
            int revive = update.FindIndex(i => i.LoadsConstant("FOLLOWERREVIVE"));
            Require(update[revive - 3].LoadsConstant(1f) && update[revive - 2].opcode == OpCodes.Ldarg_0 && update[revive + 2].opcode == OpCodes.Conv_R4 &&
                update[revive + 3].LoadsConstant(100f) && update[revive + 4].opcode == OpCodes.Div && update[revive + 5].opcode == OpCodes.Add,
                "Companion revive rate remains 1 + owner bonus / 100: " + type);
            Require(update.Any(i => Calls(i, "get_deltaTime")) && update.Any(i => Calls(i, "Update")), "Native timer consumes live rate: " + type);
        }
        var units = game.GetType("UnitAvatar", true)!;
        Require(new[] { "customStats", "calculatedBonusStats", "customStatsAmp" }.All(field =>
            AccessTools.Field(units, field)?.FieldType.Name.StartsWith("SyncDictionary") == true), "Owner inputs use native replicated dictionaries");
        Require(StatCatalog.All.Where(s => s.IsCompanion).All(s => s.MenuId == ""), "Companion group does not claim C-menu visibility");
        Console.WriteLine("Verified six native companion owner stats, fractional critical units, live speed/damage/defense consumers and three revival timers (not live multiplayer).");
    }
}
