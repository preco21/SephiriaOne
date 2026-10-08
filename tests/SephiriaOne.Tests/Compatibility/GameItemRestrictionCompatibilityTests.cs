using System.Reflection;
using HarmonyLib;

internal static class GameItemRestrictionCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), name)).ToList();
        bool Calls(List<CodeInstruction> code, string type, string name) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name);
        bool Key(List<CodeInstruction> code, string key) => code.Any(i => Equals(i.operand, key));
        void Require(bool value, string message) { if (!value) throw new Exception("Item restriction contract: " + message); }
        var hooks = addon.GetType("SephiriaOne.ItemRestrictionHooks", true)!;
        Require((bool)AccessTools.Method(hooks, "ValidateContracts").Invoke(null, null)!, "installed metadata/save signatures rejected");
        foreach (string method in new[] { "AddStartingItem", "RestockStartingItem", "Update" })
        {
            var code = Code("GridInventory", method);
            Require(Calls(code, "DungeonManager", "BoundOnServer") && Calls(code, "DungeonManager", "OwnRestrictionOnServer"), "starting grant/retry/restock restriction changed: " + method);
        }
        var fountain = Code("PlayerSpawner", "AddDimensionPocketItemsOnServer");
        Require(Calls(fountain, "DungeonManager", "BoundOnServer") && !Calls(fountain, "DungeonManager", "OwnRestrictionOnServer"), "Fountain is owner-bound, not sale-restricted");
        var saleSurfaces = Type("UI_ShopPanel").GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetMethodBody() != null).Select(m => PatchProcessor.GetOriginalInstructions(m).ToList()).ToList();
        Require(saleSurfaces.Count(c => Key(c, "OwnRestriction") && Calls(c, "DungeonManager", "GetGlobalItemStatValue")) == 3,
            "all three stock guest sale surfaces must read synchronized restrictions");
        foreach (string method in new[] { "LocalThrowItem", "UserCode_CmdThrowAllTempStorage__Vector3" })
        {
            var code = Code("GridInventory", method);
            Require(Key(code, "Bound") && Calls(code, "Item", "set_NetworkisBound"), "native drops must derive binding from shared metadata");
            Require(Key(code, "Destructible"), "intrinsic destruction rules remain in native drop path");
        }
        Require(Code("Item", "Acquire").Any(i => i.operand is FieldInfo f && f.Name == "isBound"), "stock guest acquisition reads binding SyncVar");
        Require(Calls(Code("UnitAvatar", "ServerSellToShop"), "DungeonManager", "UnboundOnServer") &&
            Calls(Code("UnitAvatar", "ServerSellSubBagItemToShop"), "DungeonManager", "UnboundOnServer"), "sales retire original binding");
        var save = Code("DungeonManager", "SaveCurrentSessionData");
        Require(Key(save, "GlobalItemStatCount") && !save.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "SaveManager" && m.Name == "Save"),
            "native metadata serialization must finish before disk flushing");
        var runtime = addon.GetType("SephiriaOne.ItemRestrictionRuntime", true)!;
        var postfix = AccessTools.Method(runtime, "SaveNativeRestrictions");
        Require(postfix.GetParameters().Length == 1 && postfix.GetParameters()[0].Name == "__instance", "save hook must receive exact DungeonManager instance");
        var ground = PatchProcessor.GetOriginalInstructions(AccessTools.Method(runtime, "RefreshGround")).ToList();
        Require(Calls(ground, "NetworkIdentity", "RemoveClientAuthority") && Calls(ground, "NetworkIdentity", "AssignClientAuthority") && Calls(ground, "Item", "set_NetworkisBound"),
            "ground items need native binding and ownership synchronization in both directions");
        Console.WriteLine("Verified native starting/Fountain restrictions, all stock guest sale surfaces, owner-bound drops, save boundary and ground authority contracts (not live multiplayer).");
    }
}
