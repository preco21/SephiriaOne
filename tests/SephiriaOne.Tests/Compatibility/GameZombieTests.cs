using System.Reflection;
using HarmonyLib;

internal static class GameZombieTests
{
    internal static void Run(Assembly addon)
    {
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(
            AccessTools.DeclaredMethod(addon.GetType("SephiriaOne." + type, true)!, method)).ToList();
        bool Calls(string type, string method, string owner, string target) =>
            Code(type, method).Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == target);
        void Require(bool value, string why) { if (!value) throw new Exception("Zombie integration: " + why); }
        Require(Calls("CombatMatchRuntime", "AllowsPair", "CombatMatchRuntime", "ZombieAllowsPair") &&
            Calls("FriendlyFireRuntime", "CanAffectTeam", "CombatMatchRuntime", "AllowsPair"), "Zombie policy participates in existing shared hostility boundary");
        Require(Calls("CombatMatchRuntime", "TryRoleSettings", "CombatMatchRuntime", "IsActiveZombie") &&
            Calls("CombatMatchRuntime", "TryRoleSettings", "SessionSettings", "get_ZombieModifiers"), "Shared modifiers select current Zombie role");
        Require(Calls("CombatMatchRuntime", "ConfirmedKill", "CombatMatchRuntime", "ConfirmedZombieKill") &&
            Calls("FriendlyFireRuntime", "FinishDeath", "CombatMatchRuntime", "CompletedMatchDeath") &&
            Calls("CombatMatchRuntime", "Revived", "CombatMatchRuntime", "ZombieRevived"), "Conversion uses completed native death/revival callbacks");
        var panel = Code("SettingsPanel", "BuildZombieEditor");
        Require(panel.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "PanelWidgets" && m.Name == "Input") == 4 &&
            panel.Any(i => i.operand is string s && s == "/one zombie any-death") &&
            Calls("SettingsPanel", "BuildZombieEditor", "SettingsPanel", "BuildParticipantsEditor"), "Zombie panel exposes count, timer, bonuses and shared selection");
        foreach (string suffix in new[] { "Duration", "Count", "Refill", "Incoming" })
            Require(Calls("SettingsPanel", "ApplyZombie" + suffix, "SettingsPanel", "Execute") &&
                Calls("SettingsPanel", "ClearInput", "SettingsPanel", "ClearZombie" + suffix), "Zombie input dispatch/cleanup: " + suffix);
        Console.WriteLine("Verified Zombie shared hostility, role modifiers, native callback and panel wiring (not live multiplayer).");
    }
}
