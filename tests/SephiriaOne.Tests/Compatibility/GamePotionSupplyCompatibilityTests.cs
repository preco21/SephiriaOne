using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePotionSupplyCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool condition, string detail) { if (!condition) throw new Exception("Large Potion contract: " + detail); }
        var inventory = game.GetType("GridInventory", true)!;
        var metadata = game.GetType("ItemMetadata", true)!;
        var own = game.GetType("NewItemOwnInstance", true)!;
        // Shared native add/permission/temporary-sync contracts are also covered
        // by GameRabbitLevelUpCompatibilityTests in this same installed-game run.
        foreach (var pair in new[] { (own, "EntityID", typeof(int)), (own, "Quantity", typeof(sbyte)),
            (metadata, "entityID", typeof(int)), (metadata, "quantity", typeof(sbyte)) })
            Require(AccessTools.Field(pair.Item1, pair.Item2)?.FieldType == pair.Item3, "ownership field " + pair.Item2);
        var subBag = AccessTools.Field(inventory, "subBagMatrix");
        Require(subBag != null && subBag.IsPublic && subBag.IsInitOnly &&
            subBag.FieldType.GetGenericTypeDefinition().FullName == "Mirror.SyncDictionary`2" &&
            subBag.FieldType.GetGenericArguments().SequenceEqual(new[] { typeof(sbyte), metadata }), "sub-bag ownership and stock sync");
        var init = PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(inventory)).ToList();
        int load = init.FindLastIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, subBag));
        Require(load >= 0 && init.Skip(load + 1).Take(2).Any(i => i.operand is MethodInfo m && m.Name == "InitSyncObject"), "sub-bag sync registration");
        var database = game.GetType("ItemDatabase", true)!;
        Require(AccessTools.DeclaredMethod(database, "GenerateInstanceID", new[] { typeof(Random) })?.ReturnType == typeof(int), "native item instance IDs");
        var item = game.GetType("ItemEntity", true)!;
        Require(AccessTools.Field(item, "id")?.FieldType == typeof(int) && AccessTools.Field(item, "type")?.FieldType == game.GetType("EItemType"), "native item identity");
        var grant = addon.GetType("SephiriaOne.LargePotionGrant", true)!;
        Require((int)grant.GetField("ItemId", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()! == 1, "audited native Large Potion ID");
        Console.WriteLine("Verified native Large Potion inventory identity, sub-bag ownership and stock-client sync contracts (asset ID/key separately audited; not live guest gameplay).");
    }
}
