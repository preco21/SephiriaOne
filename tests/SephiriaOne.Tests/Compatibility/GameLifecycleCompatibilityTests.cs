using System.Reflection;
using HarmonyLib;

internal static class GameLifecycleCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        // Inspect native IL without invoking Unity/game code. These are the
        // stable boundaries used by the lifecycle audit and the grant prefix.
        var paths = new[]
        {
            ("UI_PresetPanel", "UpdateCurrentPlayer", new[] { "EquipCostume", "CmdResetPassiveStat", "CmdLoadPassiveStat" }),
            ("PlayerAvatar", "UpdateCostumeData", new[] { "RemoveStatus", "ApplyStatus" }),
            ("PlayerAvatar", "AddPassiveStatOnServer", new[] { "RemoveStatus", "ApplyStatus" }),
            ("PlayerAvatar", "UserCode_CmdResetPassiveStat", new[] { "RemoveStatus" }),
            ("PlayerAvatar", "ServerReceiveHardModeReward", new[] { "set_NetworkmaxPassivePoint" }),
            ("StatusInstance_DimensionPocket", "ApplyStatusInner", new[] { "set_NetworkdimensionPocket" }),
            ("StatusInstance_DimensionPocket", "RemoveStatusInner", new[] { "set_NetworkdimensionPocket" }),
            ("StatusInstance_Custom", "ApplyStatusInner", new[] { "AddCustomStat" }),
            ("StatusInstance_Custom", "RemoveStatusInner", new[] { "AddCustomStat" })
        };
        foreach (var (type, name, expectedCalls) in paths)
        {
            var method = AccessTools.DeclaredMethod(game.GetType(type, true), name);
            if (method == null) throw new Exception("Native lifecycle method changed: " + type + "." + name);
            var calls = PatchProcessor.GetOriginalInstructions(method).Select(i => i.operand).OfType<MethodInfo>().Select(m => m.Name).ToHashSet();
            if (!expectedCalls.All(calls.Contains)) throw new Exception("Native lifecycle calls changed: " + type + "." + name);
        }
        var grant = AccessTools.DeclaredMethod(game.GetType("PlayerSpawner", true), "AddDimensionPocketItemsOnServer", new[] { typeof(int[]) });
        var prefix = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.SessionBoundaryHooks", true), "BeforeFountainGrant");
        if (grant == null || grant.ReturnType != typeof(void) || prefix == null || !prefix.IsStatic || prefix.GetParameters().Length != 0)
            throw new Exception("Fountain grant hook signature changed.");
        if (!PatchProcessor.GetOriginalInstructions(prefix).Any(i => i.operand is MethodInfo m && m.Name == "BeforeNativeRead"))
            throw new Exception("Fountain grant no longer uses the shared freshness boundary.");
        var instructions = PatchProcessor.GetOriginalInstructions(grant);
        if (!instructions.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "KeywordDatabase" && m.Name == "GetConstValue") ||
            !instructions.Any(i => i.operand is FieldInfo f && f.DeclaringType?.Name == "GridInventory" && f.Name == "dimensionPocket"))
            throw new Exception("Native Fountain grant no longer reads the audited capacity and cap.");
        Console.WriteLine("Verified 9 installed-game lifecycle paths and the Fountain grant hook boundary.");
    }
}
