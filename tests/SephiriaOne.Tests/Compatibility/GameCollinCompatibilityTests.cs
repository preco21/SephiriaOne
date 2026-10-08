using System.Reflection;
using HarmonyLib;

internal static class GameCollinCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string method) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Collin native contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.CollinHooks", true)!;
        Require((bool)AccessTools.Method(hooks, "ValidateNative").Invoke(null, null)!, "installed native ownership/restock contract accepted");
        var add = Code("GridInventory", "AddStartingItem");
        int register = add.FindIndex(i => i.operand is FieldInfo f && f.Name == "startingItems");
        int place = add.FindIndex(i => i.operand is MethodInfo m && m.Name == "LocalAddItem");
        Require(register >= 0 && place > register && Calls(add, "ItemDatabase", "GenerateInstanceID"), "native grant registers ID before placement");
        Require(Calls(add, "HorayNetworkAuthenticator", "get_AccessDeny_InDungeon"), "saved dungeon grants skip native placement");
        var restock = Code("GridInventory", "RestockStartingItem");
        Require(Calls(restock, "GridInventory", "AddItem") && Calls(restock, "DungeonManager", "BoundOnServer") && Calls(restock, "DungeonManager", "OwnRestrictionOnServer"), "metadata-only registration gets normal native grant and restrictions");
        var save = Code("DungeonManager", "SaveCurrentSessionData");
        Require(save.Any(i => i.operand is FieldInfo f && f.Name == "globalItemStatTable") && save.Any(i => Equals(i.operand, "GlobalItemStatCount")), "native run save preserves provenance metadata");
        var restartType = Type("HorayNetworkManager").GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.StartsWith("<RestartGameCoroutine>"));
        var restart = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(restartType, "MoveNext"));
        int clear = restart.FindIndex(i => i.operand is FieldInfo f && f.Name == "globalItemStatTable");
        int restockCall = restart.FindIndex(i => i.operand is MethodInfo m && m.Name == "RestartNewGame");
        Require(clear >= 0 && restockCall > clear && restart.Skip(clear).Take(3).Any(i => i.operand is MethodInfo m && m.Name == "Clear"), "fixture must model native metadata clear before fresh restock");
        Require(Calls(Code("Charm_LeadNPC", "OnEnabledEffect"), "NetworkServer", "Spawn") &&
            Calls(Code("Charm_LeadNPC", "OnEnabledEffect"), "UnitAvatar", "SetLeader") &&
            Calls(Code("Charm_LeadNPC", "OnDisabledEffect"), "UnitAvatar", "SetLeader"), "native Collin effect retains stock spawn and leader removal");
        var entry = addon.GetType("SephiriaOne.Entry", true)!;
        foreach (var pair in new[] { ("OnModLoaded", "Initialize"), ("OnDatabasesReady", "OnDatabasesReady"), ("OnModUnloaded", "Shutdown") })
            Require(Calls(PatchProcessor.GetOriginalInstructions(AccessTools.Method(entry, pair.Item1)), "CollinFeature", pair.Item2), "Entry lifecycle missing " + pair.Item2);
        Console.WriteLine("Verified Collin native registration, restrictions, metadata save/restart clear, companion and addon lifecycle contracts (not live gameplay).");
    }
}
