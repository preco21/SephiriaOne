using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SephiriaOne;

internal static class ChoiceTranspilerTests
{
    // A tiny fixture with the same loop anchors; its own ceiling prevents a
    // failed test from hanging if the guard is absent or a branch is misplaced.
    private sealed class Sephirite
    {
        public bool NetworkisGenerated { get; set; }
        public int Attempts;
        public int Rewards;
        public int Available;

        public void GenerateItems(int requested)
        {
            var pool = new Dictionary<int, int>();
            for (int k = 0; k < Available; k++) pool[k] = k;
            for (int i = 0; i < requested; i++)
            {
                if (++Attempts > 100) throw new Exception("Unbounded item generation");
                if (pool.Count == 0) { i--; continue; }
                pool.Remove(pool.Keys.First());
                Rewards++;
            }
            NetworkisGenerated = true;
        }
    }

    private static bool ItemGuard(Dictionary<int, int> pool, Sephirite owner, ref int attempts)
        => ++attempts <= 32 && pool.Count > 0;

    private static IEnumerable<CodeInstruction> ItemPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        => ChoiceTranspilers.Items(instructions, generator, original, AccessTools.Method(typeof(ChoiceTranspilerTests), nameof(ItemGuard)));

    private sealed class WeightedEntitySelector<T>
    {
        private readonly List<T> entries;
        public WeightedEntitySelector(IEnumerable<T> entries) => this.entries = entries.ToList();
        public T GetRandom()
        {
            return entries[0];
        }
        public void RemoveItem(T item) => entries.Remove(item);
    }

    private static int GenerateMiracles(int available, int requested)
    {
        var fullPool = Enumerable.Range(0, available).ToList();
        var weighted = new WeightedEntitySelector<int>(fullPool);
        var selected = new List<long>();
        for (int i = 0; i < requested; i++)
        {
            int choice = weighted.GetRandom();
            selected.Add(choice);
            weighted.RemoveItem(choice);
        }
        GC.KeepAlive(fullPool);
        return selected.Count;
    }

    private static int MiracleGuard(int requested, List<int> fullPool) => Math.Min(requested, fullPool.Count);
    private static IEnumerable<CodeInstruction> MiraclePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        => ChoiceTranspilers.Miracles(instructions, generator, original, AccessTools.Method(typeof(ChoiceTranspilerTests), nameof(MiracleGuard)));

    internal static int Run()
    {
        var method = AccessTools.Method(typeof(Sephirite), nameof(Sephirite.GenerateItems));
        var guard = AccessTools.Method(typeof(ChoiceTranspilerTests), nameof(ItemGuard));
        var instructions = PatchProcessor.GetOriginalInstructions(method, out var generator);
        var rewritten = ChoiceTranspilers.Items(instructions, generator, method, guard).ToList();
        if (rewritten.Count(x => x.Calls(guard)) != 1) throw new Exception("Item exhaustion guard must be inserted once");
        int checks = 1;
        var harmony = new Harmony("SephiriaOne.Tests.ChoiceGuards");
        try
        {
            harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ChoiceTranspilerTests), nameof(ItemPatch)));
            foreach (var test in new (int Available, int Requested, int Expected)[] { (2, 5, 2), (0, 5, 0), (8, 3, 3) })
            {
                var fixture = new Sephirite { Available = test.Available };
                fixture.GenerateItems(test.Requested);
                if (fixture.Rewards != test.Expected || !fixture.NetworkisGenerated)
                    throw new Exception("Guard must finish normally with available rewards");
                checks++;
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }

        try
        {
            ChoiceTranspilers.Items(new[] { new CodeInstruction(OpCodes.Ret) }, generator, method, guard).ToList();
            throw new Exception("Changed game method was silently accepted");
        }
        catch (InvalidOperationException) { checks++; }

        var miracleMethod = AccessTools.Method(typeof(ChoiceTranspilerTests), nameof(GenerateMiracles));
        var miracleGuard = AccessTools.Method(typeof(ChoiceTranspilerTests), nameof(MiracleGuard));
        var miracleCode = PatchProcessor.GetOriginalInstructions(miracleMethod, out var miracleGenerator);
        var rewrittenMiracles = ChoiceTranspilers.Miracles(miracleCode, miracleGenerator, miracleMethod, miracleGuard).ToList();
        if (rewrittenMiracles.Count(x => x.Calls(miracleGuard)) != 1) throw new Exception("Miracle pool limit must be inserted once");
        checks++;
        try
        {
            harmony.Patch(miracleMethod, transpiler: new HarmonyMethod(typeof(ChoiceTranspilerTests), nameof(MiraclePatch)));
            if (GenerateMiracles(2, 8) != 2 || GenerateMiracles(0, 3) != 0 || GenerateMiracles(8, 3) != 3)
                throw new Exception("Miracle guard must stop at distinct available candidates");
            checks++;
        }
        finally { harmony.UnpatchAll(harmony.Id); }
        try
        {
            ChoiceTranspilers.Miracles(new[] { new CodeInstruction(OpCodes.Ret) }, miracleGenerator, miracleMethod, miracleGuard).ToList();
            throw new Exception("Changed miracle method was silently accepted");
        }
        catch (InvalidOperationException) { checks++; }
        return checks;
    }
}
