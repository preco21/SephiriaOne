using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameExperienceCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var level = game.GetType("LevelController", true)!;
        var hooks = addon.GetType("SephiriaOne.ExperienceHooks", true)!;
        Require((bool)AccessTools.DeclaredMethod(hooks, "ValidateNative").Invoke(null, null)!, "Native XP compatibility guard");
        var add = AccessTools.DeclaredMethod(level, "AddExp", new[] { typeof(int) });
        var grant = AccessTools.DeclaredMethod(level, "LocalAddExp", new[] { typeof(int) });
        var initialize = AccessTools.DeclaredMethod(level, "LocalInitialize", new[] { typeof(int), typeof(int) });
        var setter = AccessTools.PropertySetter(level, "NetworkcurrentExp");
        foreach (var pair in new[] { ("Exp", "Update"),
            ("UI_DevCommandlinePanel", "OnSubmitCommand"),
            ("PlayerSpawner", "RestorePreservedRejoinables") })
            Require(Code(AccessTools.DeclaredMethod(game.GetType(pair.Item1), pair.Item2)).Any(i => i.Calls(add)),
                "All native source grants converge: " + pair);
        VerifyFountainGrant(game, add);
        Require(Code(grant).Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "FarmAbilityController" && m.Name == "AddExp"),
            "Blocking character XP must also skip its forwarded ability XP");
        Require(Code(initialize).Any(i => i.Calls(setter)) && !Code(initialize).Any(i => i.Calls(grant)),
            "Saved XP initialization remains independent of earned XP");
        Require(Code(setter).Any(i => i.operand is MethodInfo m && m.Name.StartsWith("GeneratedSyncVarSetter")),
            "Native XP changes must retain stock-client replication");
        var avatar = game.GetType("PlayerAvatar", true)!;
        Require(AccessTools.Field(avatar, "currentCostume").FieldType == typeof(string) &&
            Code(AccessTools.DeclaredMethod(level, "Awake")).Any(i => i.operand is MethodInfo m && m.Name == "set_Avatar"),
            "XP boundary reads the current avatar/costume initialized in Awake");

        // XP can only change through initialization/grants and Mirror's generated
        // receive path in the installed controller. Detect a new gameplay writer.
        var exp = AccessTools.Field(level, "currentExp");
        foreach (var method in level.GetMethods(flags | BindingFlags.DeclaredOnly))
        {
            if (method.GetMethodBody() == null) continue;
            var code = Code(method);
            if (code.Any(i => i.Calls(setter)))
                Require(method == grant || method == initialize, "Uncovered XP setter caller: " + method.Name);
            if (code.Any(i => i.opcode == OpCodes.Stfld && Equals(i.operand, exp)))
                Require(method.Name.Contains("Deserialize"), "Uncovered direct XP writer: " + method.Name);
        }
        var feature = addon.GetType("SephiriaOne.ExperienceFeature", true)!;
        var entry = addon.GetType("SephiriaOne.Entry", true)!;
        foreach (var pair in new[] { ("OnModLoaded", "Initialize"), ("OnModUnloaded", "Shutdown") })
            Require(Code(AccessTools.DeclaredMethod(entry, pair.Item1)).Any(i => i.Calls(AccessTools.DeclaredMethod(feature, pair.Item2))),
                "XP hook lifetime: " + pair);
        Console.WriteLine("Verified native XP grant sources, host/stock-client boundary, saved-level exclusion, XP writes, synchronization and hook lifetime (not live multiplayer).");
    }
    private static void VerifyFountainGrant(Assembly game, MethodInfo add)
    {
        var fountain = game.GetType("MagicFountain", true)!;
        var request = fountain.GetNestedType("Request", BindingFlags.NonPublic);
        if (request == null)
        {
            Require(Code(AccessTools.DeclaredMethod(fountain, "Update")).Any(i => i.Calls(add)),
                "Legacy fountain reward converges on AddExp");
            return;
        }
        var server = AccessTools.DeclaredMethod(fountain, "ServerRequest",
            new[] { game.GetType("PlayerAvatar", true)!, request, typeof(int) });
        Require(server != null, "Beta fountain retains its exact server request boundary");
        var code = Code(server!);
        int collect = code.FindIndex(i => i.operand is MethodInfo m &&
            m.DeclaringType?.Name == "MagicFountainProgress" && m.Name == "TryCollect");
        int grant = code.FindIndex(i => i.Calls(add));
        Require(collect >= 0 && grant > collect && code.Count(i => i.Calls(add)) == 1 &&
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "Mirror.NetworkServer" && m.Name == "get_active") &&
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType == fountain && m.Name == "IsInRange"),
            "Fountain collection remains a guarded server grant through AddExp");
        Require(Code(AccessTools.DeclaredMethod(fountain, "OnChoice")).Any(i => i.Calls(server)) &&
            Code(AccessTools.DeclaredMethod(fountain, "UserCode_CmdRequest__Request__Int32__NetworkConnectionToClient"))
                .Any(i => i.Calls(server)), "Host and stock-guest fountain choices converge on the server reward path");
    }
    private static List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception("XP contract: " + message); }
}
