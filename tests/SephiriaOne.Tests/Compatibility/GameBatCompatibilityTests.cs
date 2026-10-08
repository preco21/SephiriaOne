using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameBatCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string method) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method);
        void Require(bool condition, string why) { if (!condition) throw new Exception("Bat native contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.BatCostumeHooks", true)!;
        var unload = PatchProcessor.GetOriginalInstructions(AccessTools.Method(addon.GetType("SephiriaOne.Entry"), "OnModUnloaded"));
        int batCleanup = unload.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "BatCostumeFeature" && m.Name == "Shutdown");
        int choiceCleanup = unload.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ChoiceFeature" && m.Name == "Shutdown");
        Require(batCleanup >= 0 && choiceCleanup > batCleanup, "Bat restoration retires its fault before shared Choice cleanup");
        var batUnload = PatchProcessor.GetOriginalInstructions(AccessTools.Method(addon.GetType("SephiriaOne.BatCostumeFeature"), "Shutdown"));
        Require(batUnload.FindIndex(i => i.operand is MethodInfo m && m.Name == "BeforeBatShutdown") <
            batUnload.FindIndex(i => i.operand is MethodInfo m && m.Name == "RestoreBat"), "Mixed journal guard precedes Bat mutations during unload");
        Require((bool)AccessTools.Method(hooks, "ValidateNative").Invoke(null, null)!, "installed native status contract accepted");
        var original = Code("PlayerAvatar", "UpdateCostumeData");
        var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "Rewrite").Invoke(null, new object[] { original })!).ToList();
        int at = original.FindIndex(i => i.operand is MethodInfo m && m.Name == "CreateStatusEntity");
        Require(rewritten.Count == original.Count + 2 && rewritten[at].opcode == OpCodes.Ldarg_0 && rewritten[at + 1].opcode == OpCodes.Ldarg_1, "exact owner and costume argument supplied");
        for (int i = 0; i < original.Count; i++)
        {
            var current = rewritten[i < at ? i : i + 2];
            if (i == at) { Require(current.operand is MethodInfo m && m.Name == "Create" && m.DeclaringType?.Name == "BatCostumeRuntime", "only factory redirected"); continue; }
            Require(current.opcode == original[i].opcode && Equals(current.operand, original[i].operand) &&
                current.labels.SequenceEqual(original[i].labels) && current.blocks.SequenceEqual(original[i].blocks), "all native equip/remove instructions preserved");
        }
        Require(Calls(original, "StatusInstance", "ClearTarget") && Calls(original, "StatusInstance", "RemoveStatus"), "native removal retains status lifetime");
        Require(Code("PlayerAvatar", "OnCurrentCostumeChanged").Any(i => i.operand is MethodInfo m && m.Name == "UpdateCostumeData"), "native costume change drives factory");
        Require(Calls(Code("StatusInstance_HPSteal", "ApplyStatusInner"), "UnitAvatar", "AddCustomStat") &&
            Calls(Code("StatusInstance_HPSteal", "RemoveStatusInner"), "UnitAvatar", "AddCustomStat"), "native apply/remove change synchronized stat");
        Require(Type("UnitAvatar").GetField("customStats")!.FieldType.Name.StartsWith("SyncDictionary"), "customStats uses native Mirror dictionary");
        Require(Code("UnitAvatar", "GetRawStatUnsafe").Any(i => i.operand is FieldInfo f && f.Name == "customStats") &&
            Calls(Code("AvatarStatsHooker", "HookStat"), "UnitAvatar", "GetCustomStat"), "native effective stats/UI read synced values");
        Require(!Code("PlayerSpawner", "SaveCurrentSessionData").Any(i => i.operand is FieldInfo f && (f.Name == "customStats" || f.Name == "costumeStats")), "run save does not serialize projected costume/raw stat instances");
        var altered = original.Where(i => !(i.operand is MethodInfo m && m.Name == "ClearTarget")).ToList();
        bool rejected = false;
        try { AccessTools.Method(hooks, "Rewrite").Invoke(null, new object[] { altered }); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
        Require(rejected, "changed native ownership rejected");
        Console.WriteLine("Verified Bat costume status ownership, native apply/remove, replicated stat/UI reads and run save boundary (not live gameplay).");
    }
}
