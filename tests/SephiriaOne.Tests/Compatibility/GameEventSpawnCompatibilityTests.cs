using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
internal static class GameEventSpawnCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Random event contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.EventSpawnHooks", true)!;
        foreach (string type in new[] { "StageEntity_Choice", "StageEntity_GrasslandTown" })
        {
            var method = AccessTools.DeclaredMethod(game.GetType(type, true), "GenerateStage");
            var code = PatchProcessor.GetOriginalInstructions(method);
            Require((bool)AccessTools.Method(hooks, "Validate").Invoke(null, new object[] { code })!, type + " IL rejected");
            var result = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "Rewrite").Invoke(null, new object[] { code })!).ToList();
            Require(result.Count == code.Count + 2, "Only two threshold calls added");
            var original = result.Where(i => !(i.operand is MethodInfo m && m.DeclaringType == hooks)).ToList();
            Require(original.Count == code.Count, "Exactly two addon calls");
            for (int n = 0; n < code.Count; n++)
                Require(code[n].opcode == original[n].opcode && Equals(code[n].operand, original[n].operand) &&
                    code[n].labels.SequenceEqual(original[n].labels) && code[n].blocks.SequenceEqual(original[n].blocks), "Every original instruction and label preserved");
        }
        var network = game.GetType("Mirror.GeneratedNetworkCode", true)!;
        foreach (string operation in new[] { "_Write_FloorData", "_Read_FloorData" })
        {
            var method = network.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Single(m => m.Name.Contains(operation));
            Require(PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is FieldInfo f && f.Name == "randomRoomCount"), "FloorData event count must be serialized natively");
        }
        foreach (string method in new[] { "SerializeSyncVars", "DeserializeSyncVars" })
            Require(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("FloorGenerator", true), method))
                .Count(i => i.operand is FieldInfo f && f.Name == "randomRoomCount") == 2, "Floor generator count is replicated in initial and incremental state");
        Require(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("FloorGenerator", true), "Connect"))
            .Any(i => i.operand is MethodInfo m && m.Name == "set_NetworkrandomRoomCount"), "FloorData count feeds native generator SyncVar");
        foreach (string type in new[] { "EnhancedProceduralFloorGenerator", "LibraryFloorGenerator" })
        {
            var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType(type, true), "LoadAllRoomPresetData"));
            Require(code.Any(i => i.operand is FieldInfo f && f.Name == "randomRoomCount") &&
                code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "Mathf" && m.Name == "Min"), "Native room selection consumes synchronized count and caps to room pool");
        }
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var editor = PatchProcessor.GetOriginalInstructions(AccessTools.Method(panel, "BuildSpawnEditor"));
        Require(editor.Any(i => Equals(i.operand, "/one events")) && editor.Any(i => Equals(i.operand, "/one jars")), "Spawns selector keeps separate commands");
        Console.WriteLine("Verified both native event generators, exact two-call rewrite, native FloorData/room-count serialization and room selection (not live multiplayer).");
    }
}
