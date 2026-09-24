using System.Reflection;
using HarmonyLib;

internal static class GameStartingResourceCompatibilityTests
{
    // Reads IL only. Does not patch or execute a native game method.
    public static void Run(Assembly game, Assembly addon)
    {
        Type hooks = addon.GetType("SephiriaOne.StartingResourceHooks", true)!;
        foreach (Type type in addon.GetTypes().Where(t => t.FullName!.StartsWith("SephiriaOne.StartingResourceTranspilers")))
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (method.GetMethodBody() != null && PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is MethodInfo called &&
                    called.DeclaringType?.Assembly.GetName().Name == "0Harmony" && called.Name == "LoadsConstant" &&
                    called.GetParameters().Any(p => p.ParameterType == typeof(string))))
                    throw new Exception("Starting transpiler references LoadsConstant(string), absent in the installed HarmonyX runtime.");
        foreach (var specification in new[]
        {
            ("PlayerSpawner", "Initialize", "InitializePatch", new[] { "InitializeMoney", "InitializeDice" }),
            ("DungeonManager", "LoadStageAndMove", "DeparturePatch", new[] { "PlanDepartureMoney", "ApplyDepartureMoney" })
        })
        {
            MethodInfo native = AccessTools.DeclaredMethod(game.GetType(specification.Item1), specification.Item2);
            MethodInfo rewrite = AccessTools.DeclaredMethod(hooks, specification.Item3);
            var original = PatchProcessor.GetOriginalInstructions(native).ToList();
            var rewritten = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { original })!).ToList();
            foreach (string name in specification.Item4)
            {
                MethodInfo adapter = AccessTools.DeclaredMethod(hooks, name);
                if (rewritten.Count(i => i.Calls(adapter)) != 1)
                    throw new Exception("Starting-resource replacement missing: " + name);
            }
            var changed = PatchProcessor.GetOriginalInstructions(native).ToList();
            int remove = changed.FindIndex(i => i.operand is MethodInfo method && method.Name == "AddMoney");
            if (remove < 0) throw new Exception("Native money call disappeared.");
            changed.RemoveAt(remove);
            bool rejected = false;
            try { _ = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { changed })!).ToList(); }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Changed native starting-resource boundary was accepted.");
            Console.WriteLine("Verified installed-game starting-resource boundary: " + specification.Item1 + "." + specification.Item2);
        }
        foreach (string field in new[] { "maxRerollDice", "rerollDice" })
        {
            Type player = game.GetType("PlayerAvatar", true)!;
            MethodInfo setter = AccessTools.PropertySetter(player, "Network" + field);
            if (setter == null || !PatchProcessor.GetOriginalInstructions(setter).Any(i => i.operand is MethodInfo method && method.Name == "GeneratedSyncVarSetter"))
                throw new Exception("Starting dice no longer use a native SyncVar: " + field);
        }
        MethodInfo moneySetter = AccessTools.PropertySetter(game.GetType("UnitAvatar", true), "NetworkcurrentMoney");
        if (moneySetter == null || !PatchProcessor.GetOriginalInstructions(moneySetter).Any(i => i.operand is MethodInfo method && method.Name == "GeneratedSyncVarSetter"))
            throw new Exception("Starting leaves no longer use a native SyncVar.");
    }
}
