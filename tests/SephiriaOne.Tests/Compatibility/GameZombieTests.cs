using System.Reflection;
using System.Reflection.Emit;
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
        Require(Calls("CombatMatchRuntime", "TryRoleSettings", "CombatMatchRuntime", "TryActiveZombieRole") &&
            Calls("CombatMatchRuntime", "IsActiveZombie", "CombatMatchRuntime", "TryActiveZombieRole") &&
            Calls("CombatMatchRuntime", "TryActiveZombieRole", "CombatMatchRuntime", "TryZombieIndex") &&
            Calls("CombatMatchRuntime", "TryRoleSettings", "SessionSettings", "get_ZombieModifiers"),
            "Shared modifiers select either active team through the same receipt-bound roster lookup");
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
        Require(Calls("SettingsPanel", "RefreshParticipants", "CombatMatchRuntime", "ZombieFactionMarker"), "Participant rows use live receipt-bound faction markers");
        Require(Calls("SettingsPanel", "RefreshZombie", "ZombieSurvivalCommand", "DescribeHumanModifier") &&
            Calls("ZombieSurvivalCommand", "DescribeModifiers", "ZombieSurvivalCommand", "DescribeHumanModifier"),
            "Panel and command status share the effective Human protection readout");
        Require(Calls("CombatMatchRuntime", "BroadcastRespawnCountdown", "ZombieSurvivalMessages", "Conversion") &&
            !Calls("CombatMatchRuntime", "InfectZombie", "CombatMatchRuntime", "Broadcast") &&
            !Calls("CombatMatchRuntime", "ZombieRevived", "CombatMatchRuntime", "Broadcast"), "Shared countdown owns the single conversion notice");
        var speedUpdate = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.ZombieMovementSpeed", true)!, "Update");
        var avatar = speedUpdate.GetParameters()[0].ParameterType.Assembly.GetType("UnitAvatar", true)!;
        var speed = AccessTools.DeclaredField(avatar, "moveSpeed");
        var setter = AccessTools.PropertySetter(avatar, "NetworkmoveSpeed");
        List<CodeInstruction> Native(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, name)).ToList();
        var set = PatchProcessor.GetOriginalInstructions(setter).ToList();
        Require(speed.FieldType == typeof(float) && set.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, speed)) &&
            set.Any(i => i.LoadsConstant(8589934592L)) && set.Any(i => i.opcode == OpCodes.Ldnull) &&
            set.Count(i => i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter") == 1,
            "Base movement uses a native float SyncVar setter with no callbacks");
        Require(Native("SerializeSyncVars").Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, speed)) == 2 &&
            Native("DeserializeSyncVars").Count(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, speed)) == 2,
            "Both initial and incremental stock guest state carry base speed");
        foreach (string method in new[] { "MoveUpdate", "RunUpdate" })
            Require(Native(method).Any(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, speed)) &&
                Native(method).Any(i => i.operand is MethodInfo m && m.Name == "GetMoveSpeedMultiplier"),
                "Native movement composes base speed with existing modifiers: " + method);
        Require(Code("ZombieMovementSpeed", "Update").Any(i => i.Calls(setter)) &&
            Calls("CombatMatchRuntime", "UpdateZombieMovement", "CombatMatchRuntime", "IsZombieVisualCurrent") &&
            Calls("CombatMatchRuntime", "ClearZombie", "ZombieMovementSpeed", "Update"),
            "Movement uses replicated values, existing role/receipt gating and common cleanup");
        Require(panel.Any(i => Equals(i.operand, "[Z] Movement speed: 80%.")), "Zombie panel discloses the fixed movement rule");
        Console.WriteLine("Verified Zombie base movement replication, native walk/run composition and owned role cleanup (not live movement).");
        Console.WriteLine("Verified Zombie shared hostility, role modifiers, native callback and panel wiring (not live multiplayer).");
    }
}
