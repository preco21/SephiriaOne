using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameRequitalCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Requital native contract: " + why); }
        var inventory = game.GetType("GridInventory", true)!;
        var metadata = game.GetType("ItemMetadata", true)!;
        var pending = game.GetType("GridInventory+StartingItemAdditionFailedInfo", true)!;
        var hooks = addon.GetType("SephiriaOne.RequitalHooks", true)!;
        var runtime = addon.GetType("SephiriaOne.RequitalRuntime", true)!;
        Require((bool)AccessTools.Method(hooks, "ValidateNative").Invoke(null, null)!, "installed ownership/restock contract accepted");
        Require((int)AccessTools.Field(runtime, "ItemId").GetRawConstantValue()! == 2100, "exact audited Requital identity");
        foreach (string name in new[] { "instanceID", "entityID", "playerIdx" })
            Require(AccessTools.Field(pending, name)?.FieldType == typeof(int), "pending " + name);
        Require(AccessTools.Field(pending, "quantity")?.FieldType == typeof(sbyte), "pending quantity");
        var starting = AccessTools.Field(inventory, "startingItems");
        Require(starting.IsPublic && starting.IsInitOnly && starting.FieldType.GetGenericTypeDefinition().FullName == "Mirror.SyncList`1" &&
            starting.FieldType.GetGenericArguments().Single() == metadata, "native synchronized starting metadata");
        var restock = AccessTools.DeclaredMethod(inventory, "RestockStartingItem", new[] { typeof(int) });
        var code = PatchProcessor.GetOriginalInstructions(restock).ToList();
        var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "ReplaceRestockDelivery").Invoke(null, new object[] { code })!).ToList();
        Require(rewritten.Count(i => i.Calls(AccessTools.Method(runtime, "RestockItem"))) == 1, "one guarded delivery replacement");
        Require(rewritten.Any(i => i.operand is MethodInfo m && m.Name == "BoundOnServer") &&
            rewritten.Any(i => i.operand is MethodInfo m && m.Name == "OwnRestrictionOnServer"), "native restock ownership remains");
        var remove = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(inventory, "RemoveStartingItem")).ToList();
        foreach (string name in new[] { "ForceRemoveItem", "RemoveEngravingOnServer", "RemoveFixedEngravingOnServer",
            "DestroyItemInstance", "TryGetSubBagItemByInstanceID" })
            Require(remove.Any(i => i.operand is MethodInfo m && m.Name == name), "exact-ID cleanup includes " + name);
        foreach (string name in new[] { "startingItems", "additionFailedStartingItems", "temporaryInventory" })
            Require(remove.Any(i => i.operand is FieldInfo f && f.Name == name), "cleanup retains " + name);
        var clear = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(inventory, "ForceRemoveAll")).ToList();
        Require(!clear.Any(i => i.operand is FieldInfo f && f.Name == "additionFailedStartingItems"),
            "restart fixture must retain native pending starting deliveries across ForceRemoveAll");
        var dungeon = game.GetType("DungeonManager", true)!;
        Require(PatchProcessor.GetOriginalInstructions(AccessTools.Method(dungeon, "BuildGlobalItemStatKey"))
            .Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "/")), "provenance key delimiter");
        var constructor = PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(inventory)).ToList();
        var tablets = AccessTools.Field(inventory, "stoneTablets");
        int sync = constructor.FindLastIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, tablets));
        Require(sync >= 0 && constructor.Skip(sync + 1).Take(2).Any(i => i.operand is MethodInfo m && m.Name == "InitSyncObject"),
            "stock clients receive native Slate instances through the synchronized dictionary");
        var entry = addon.GetType("SephiriaOne.Entry", true)!;
        foreach (var pair in new[] { ("OnModLoaded", "Initialize"), ("OnDatabasesReady", "Load"), ("OnModUnloaded", "Shutdown") })
            Require(PatchProcessor.GetOriginalInstructions(AccessTools.Method(entry, pair.Item1))
                .Any(i => i.Calls(AccessTools.Method(runtime, pair.Item2))), "Entry lifecycle: " + pair.Item2);
        Console.WriteLine("Verified Requital native restock rewrite, full-inventory queue, exact-ID cleanup, provenance and stock Slate sync contracts (asset separately audited; not live guest gameplay).");
    }
}
