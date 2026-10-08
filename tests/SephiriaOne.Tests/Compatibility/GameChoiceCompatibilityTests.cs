using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using SephiriaOne;

internal static class GameChoiceCompatibilityTests
{
    // Inspect installed code without starting Unity or running any game method.
    internal static void Run(string managed, string addonPath)
    {
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var addon = Assembly.LoadFrom(Path.GetFullPath(addonPath));
        GameLifecycleCompatibilityTests.Run(game, addon);
        GameNameCompatibilityTests.Run(game, addon);
        GamePanelCompatibilityTests.Run(game, addon);
        GamePanelLocalizationTests.Run(game, addon);
        GameStartingResourceCompatibilityTests.Run(game, addon);
        GameResourceBudgetCompatibilityTests.Run(game, addon);
        GameDisconnectCompatibilityTests.Run(game, addon);
        GameRabbitCompatibilityTests.Run(game, addon);
        GameRabbitArtifactCompatibilityTests.Run(game, addon);
        GameRabbitLevelUpCompatibilityTests.Run(game, addon);
        GameMerchantCompatibilityTests.Run(game, addon);
        GameItemRestrictionCompatibilityTests.Run(game, addon);
        GameFriendlyFireCompatibilityTests.Run(game, addon);
        GameJarSpawnCompatibilityTests.Run(game, addon);
        GameEventSpawnCompatibilityTests.Run(game, addon);
        GameBatCompatibilityTests.Run(game, addon);
        GameCollinCompatibilityTests.Run(game, addon);
        var guards = addon.GetType("SephiriaOne.ChoiceSafety", throwOnError: true)!;
        var freshness = AccessTools.DeclaredMethod(guards, "BeforeGeneration");
        if (freshness == null || !freshness.IsStatic || freshness.ReturnType != typeof(void) || freshness.GetParameters().Length != 0 ||
            !PatchProcessor.GetOriginalInstructions(freshness).Any(i => i.operand is MethodInfo m && m.Name == "BeforeNativeRead"))
            throw new Exception("Candidate generation no longer uses the shared freshness boundary.");
        foreach (var target in new[] { ("Sephirite", "GenerateItems", "CanRollItem"), ("MiracleSelector2", "GenerateMiracles", "LimitMiracles") })
        {
            var method = AccessTools.Method(game.GetType(target.Item1), target.Item2);
            var guard = AccessTools.Method(guards, target.Item3);
            var instructions = PatchProcessor.GetOriginalInstructions(method, out var generator);
            var rewritten = (target.Item1 == "Sephirite"
                ? ChoiceTranspilers.Items(instructions, generator, method, guard)
                : ChoiceTranspilers.Miracles(instructions, generator, method, guard)).ToList();
            if (rewritten.Count(x => x.Calls(guard)) != 1) throw new Exception("Guard mismatch: " + target.Item1);
            Console.WriteLine("Verified installed-game guard: " + target.Item1 + "." + target.Item2);
        }
        using var resource = addon.GetManifestResourceStream("SephiriaOne.Dependencies.0Harmony.dll");
        using var license = addon.GetManifestResourceStream("SephiriaOne.Dependencies.Harmony.LICENSE");
        if (resource == null || resource.Length < 1000000 || license == null || !new StreamReader(license).ReadToEnd().Contains("MIT License"))
            throw new Exception("Missing embedded Harmony runtime/license");
        Console.WriteLine("Verified embedded Harmony runtime and license.");
    }
}
