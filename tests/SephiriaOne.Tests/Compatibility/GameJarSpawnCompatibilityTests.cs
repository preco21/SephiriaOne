using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameJarSpawnCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string type, string method) => code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Mystic Jar contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.JarSpawnHooks", true)!;
        var native = Code("MysticPot", "OnStartServer");
        Require((bool)AccessTools.Method(hooks,"Validate").Invoke(null,new object[] { native })!, "installed chance/chapter/visibility IL rejected");
        Require((bool)AccessTools.Method(hooks,"ValidateHidden").Invoke(null,null)!, "hidden rewards must spawn synchronously");
        var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks,"Rewrite").Invoke(null,new object[] { native })!).ToList();
        Require(native.Count == rewritten.Count, "no new draws or branches");
        int differences = 0;
        for (int i = 0; i < native.Count; i++)
        {
            Require(native[i].labels.SequenceEqual(rewritten[i].labels) && native[i].blocks.SequenceEqual(rewritten[i].blocks), "native branch/EH labels preserved");
            if (native[i].opcode != rewritten[i].opcode || !Equals(native[i].operand,rewritten[i].operand))
            {
                differences++;
                Require(native[i].operand is FieldInfo f && f.Name == "appearRate" && rewritten[i].operand is MethodInfo m &&
                    m.Name == "Chance" && rewritten[i].opcode == OpCodes.Call, "only chance read may change");
            }
        }
        Require(differences == 1, "exactly one field read replaced");
        var sync = Code("MysticPot","SerializeSyncVars");
        Require(sync.Any(i => i.operand is FieldInfo f && f.Name == "isGenerated") && Calls(sync,"NetworkWriterExtensions","WriteBool"), "native visibility written to network");
        Require(Code("MysticPot","DeserializeSyncVars").Any(i => i.operand is FieldInfo f && f.Name == "_Mirror_SyncVarHookDelegate_isGenerated"), "guests consume visibility hook");
        Require(Calls(Code("MysticPot","HandleIsGeneratedChanged"),"GameObject","SetActive"), "stock client hides rejected jars");
        var hidden = Code("HiddenRoomRewardSpawner","OnStartServer");
        Require(Calls(hidden,"Random","Next") && hidden.Any(i => Equals(i.operand,"MysticPot")), "native hidden reward selection retained");
        Console.WriteLine("Verified Mystic Jar single-read patch, native chapter/seeded roll, guest visibility SyncVar and synchronous hidden-room exclusion (not live gameplay).");
    }
}
