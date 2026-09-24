using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class ChoiceSafety
    {
        private const string Owner = "preco21.SephiriaOne.ChoiceSafety";
        private static Harmony harmony;

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                instance.Patch(AccessTools.Method(typeof(Sephirite), nameof(Sephirite.GenerateItems)),
                    prefix: new HarmonyMethod(typeof(ChoiceSafety), nameof(BeforeGeneration)),
                    transpiler: new HarmonyMethod(typeof(ChoiceSafety), nameof(ItemPatch)));
                instance.Patch(AccessTools.Method(typeof(MiracleSelector2), "GenerateMiracles"),
                    prefix: new HarmonyMethod(typeof(ChoiceSafety), nameof(BeforeGeneration)),
                    transpiler: new HarmonyMethod(typeof(ChoiceSafety), nameof(MiraclePatch)));
                harmony = instance;
            }
            catch
            {
                instance.UnpatchAll(Owner);
                throw;
            }
        }

        public static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static void BeforeGeneration() => SessionSettings.BeforeNativeRead("Candidate generation");

        private static IEnumerable<CodeInstruction> ItemPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
            => ChoiceTranspilers.Items(instructions, generator, original, AccessTools.Method(typeof(ChoiceSafety), nameof(CanRollItem)));

        private static IEnumerable<CodeInstruction> MiraclePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
            => ChoiceTranspilers.Miracles(instructions, generator, original, AccessTools.Method(typeof(ChoiceSafety), nameof(LimitMiracles)));

        private static bool CanRollItem(Dictionary<EItemRarity, WeightedItemSelector> pools, Sephirite source, ref int attempts)
        {
            // Native rarity retries do not advance the reward counter. Bound
            // retries as well as exhaustion, including an unreachable rarity.
            if (++attempts > 4096) return false;
            DungeonManager dungeon = DungeonManager.Instance;
            if (source.type == Sephirite.Type.TABLET_BOSS && dungeon &&
                dungeon.hardModeEnvironment.TryGetValue("WEAKBOSSTABLET", out int mode))
            {
                EItemRarity rarity = mode == 2 ? EItemRarity.Common : EItemRarity.Uncommon;
                return pools.TryGetValue(rarity, out WeightedItemSelector pool) && pool.HasItems();
            }
            foreach (WeightedItemSelector pool in pools.Values)
                if (pool.HasItems()) return true;
            return false;
        }

        private static int LimitMiracles(int requested, List<Miracle> available)
            => Math.Min(requested, available.Count);
    }
}
