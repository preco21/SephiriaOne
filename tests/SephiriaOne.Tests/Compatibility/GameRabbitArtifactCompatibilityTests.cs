using System.Reflection;
using HarmonyLib;

internal static class GameRabbitArtifactCompatibilityTests
{
    // Read installed IL only. Pair these native lifecycle assertions with the
    // linked-source registration tests; no simulated inventory implementation.
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name) => AccessTools.DeclaredMethod(Type(type), name)!;
        List<CodeInstruction> Code(string type, string name) => PatchProcessor.GetOriginalInstructions(Method(type, name)).ToList();
        int Call(List<CodeInstruction> code, string type, string name) => code.FindIndex(i =>
            i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name);
        int Field(List<CodeInstruction> code, string name) => code.FindIndex(i => i.operand is FieldInfo f && f.Name == name);
        void Require(bool ok, string message) { if (!ok) throw new Exception("Rabbit artifact contract: " + message); }

        var loader = Code("GameDataLoader", "Awake");
        int costumeLoad = Call(loader, "CostumeDatabase", "Initialize");
        int itemLoad = Call(loader, "ItemDatabase", "Initialize");
        int ready = Call(loader, "HorayModAPI", "NotifyAllModDatabasesReady");
        Require(costumeLoad >= 0 && itemLoad > costumeLoad && ready > itemLoad,
            "database-ready registration must occur after both native databases exist");
        var feature = addon.GetType("SephiriaOne.RabbitStartingArtifactFeature", true)!;
        var entry = addon.GetType("SephiriaOne.Entry", true)!;
        foreach (var pair in new[] { ("OnDatabasesReady", "Apply"), ("OnModUnloaded", "Shutdown") })
            Require(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(entry, pair.Item1))
                .Any(i => Equals(i.operand, AccessTools.DeclaredMethod(feature, pair.Item2))), "Entry missing " + pair.Item2);

        var update = Code("PlayerAvatar", "UpdateCostumeData");
        int remove = Call(update, "GridInventory", "RemoveStartingItem");
        int items = Field(update, "startingItems");
        int add = Call(update, "GridInventory", "AddStartingItem");
        int tracking = update.FindIndex(add + 1, i => i.operand is FieldInfo f && f.Name == "costumeStartingItemInstanceIDs");
        Require(remove >= 0 && items > remove && add > items && tracking > add &&
            update.Take(remove).Any(i => i.operand is MethodInfo m && m.Name == "get_isServer") &&
            update.Skip(remove + 1).Take(items - remove - 1).Any(i => i.operand is MethodInfo m && m.Name == "get_isServer"),
            "native costume changes must remove old instances and grant/track new ones on the server");
        Require(update.Skip(tracking + 1).Take(3).Any(i => i.operand is MethodInfo m && m.Name == "Add"),
            "the returned starting-item instance ID must be added to costume ownership");
        Require(Call(Code("PlayerAvatar", "OnCurrentCostumeChanged"), "PlayerAvatar", "UpdateCostumeData") >= 0 &&
            Call(Code("PlayerSpawner", "Initialize"), "PlayerAvatar", "EquipCostume") >= 0,
            "native initial player setup and costume changes must use the same costume lifecycle");

        var addCode = Code("GridInventory", "AddStartingItem");
        Require(Field(addCode, "startingItems") >= 0 && Call(addCode, "GridInventory", "LocalAddItem") >= 0 &&
            Call(addCode, "DungeonManager", "BoundOnServer") >= 0 &&
            Call(addCode, "DungeonManager", "OwnRestrictionOnServer") >= 0,
            "native grants must remain tracked and bound to their owner");
        Require(AccessTools.Field(Type("GridInventory"), "startingItems").FieldType.GetGenericTypeDefinition().FullName == "Mirror.SyncList`1",
            "starting-item metadata must retain native guest synchronization");
        var removeCode = Code("GridInventory", "RemoveStartingItem");
        foreach (string method in new[] { "FindItemByInstanceID", "ForceRemoveItem", "RemoveFixedEngravingOnServer", "TryGetSubBagItemByInstanceID" })
            Require(Call(removeCode, "GridInventory", method) >= 0, "native cleanup no longer covers " + method);
        Require(Field(removeCode, "additionFailedStartingItems") >= 0 && Field(removeCode, "temporaryInventory") >= 0 &&
            Field(removeCode, "startingItems") >= 0 && Call(removeCode, "Item", "DestroyItemInstance") >= 0,
            "cleanup must remove pending/full-inventory, temporary and dropped costume instances");

        var restart = Code("PlayerSpawner", "RestartNewGame");
        Require(Call(restart, "PlayerSpawner", "Initialize") >= 0 &&
            Call(restart, "GridInventory", "RestockStartingItem") > Call(restart, "PlayerSpawner", "Initialize"),
            "new runs must restock the current starting-item list after player initialization");
        Require(Field(Code("GridInventory", "RestockStartingItem"), "startingItems") >= 0 &&
            Call(Code("GridInventory", "RestockStartingItem"), "GridInventory", "AddItem") >= 0,
            "native restocking must read the current tracked starting items");
        Require(Field(Code("UI_PresetPanel", "OpenCostumePanel"), "isInDungeon") >= 0 &&
            Field(Code("UI_PresetPanel", "LoadCurrentPreset"), "isInDungeon") >= 0,
            "re-audit saved inventory ownership if in-dungeon costume editing becomes available");
        var restartIterator = Type("HorayNetworkManager").GetNestedTypes(BindingFlags.NonPublic)
            .Single(t => t.Name.StartsWith("<RestartGameCoroutine>", StringComparison.Ordinal));
        var reset = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(restartIterator, "MoveNext")).ToList();
        Require(Call(reset, "GridInventory", "ForceRemoveAll") >= 0 &&
            Call(reset, "PlayerSpawner", "RestartNewGame") > Call(reset, "GridInventory", "ForceRemoveAll"),
            "old saved-run item IDs must be cleared before the next costume restock");
        Require(Field(Code("UI_CostumePanel", "UpdateData"), "startingItems") >= 0 &&
            Call(Code("UI_CostumePanel", "UpdateData"), "UI_ItemIcon", "SetItem") >= 0,
            "native host costume preview must display the starting item");
        Console.WriteLine("Verified Rabbit artifact database timing, native costume ownership/removal, server grant, guest metadata, restart restocking and costume preview contracts (not live gameplay).");
    }
}
