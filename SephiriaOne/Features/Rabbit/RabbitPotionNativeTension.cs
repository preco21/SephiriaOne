using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static partial class RabbitPotionNativeHooks
    {
        private static bool ValidateTensionShape(MethodInfo useItem)
        {
            MethodInfo gate = AccessTools.DeclaredMethod(typeof(ItemController), "IsHostilityBlockingPotion", Type.EmptyTypes);
            if (useItem == null || useItem.IsStatic || useItem.ReturnType != typeof(void) ||
                gate == null || gate.IsStatic || gate.ReturnType != typeof(bool)) return false;
            var checks = PatchProcessor.GetOriginalInstructions(gate).ToList();
            // Tension's native key is HOSTILITY. Refuse to exempt a repurposed
            // general potion restriction after a game update.
            if (checks.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "HOSTILITY")) != 1 ||
                checks.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(BossSpawner) &&
                    m.Name == nameof(BossSpawner.IsBossBattleInProgressOnFloor)) != 1) return false;
            try { FindTensionCall(PatchProcessor.GetOriginalInstructions(useItem).ToList(), out _); return true; }
            catch (InvalidOperationException) { return false; }
        }

        private static int FindTensionCall(List<CodeInstruction> code, out CodeInstruction loadItem)
        {
            var gates = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo m &&
                m.DeclaringType == typeof(ItemController) && m.Name == "IsHostilityBlockingPotion").ToArray();
            var finds = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo m &&
                m.DeclaringType == typeof(GridInventory) && m.Name == nameof(GridInventory.FindItem) &&
                m.ReturnType == typeof(NewItemOwnInstance)).ToArray();
            if (gates.Length != 1 || finds.Length != 1 || finds[0] + 1 >= gates[0] || gates[0] + 1 >= code.Count ||
                code[gates[0]].blocks.Count != 0 || code[gates[0] + 1].blocks.Count != 0 ||
                code[gates[0] + 1].labels.Count != 0 ||
                (code[gates[0] + 1].opcode != OpCodes.Brfalse && code[gates[0] + 1].opcode != OpCodes.Brfalse_S))
                throw new InvalidOperationException("Native Tension branch or selected potion lookup changed.");
            // Keep the exact item already checked by the native CanDrink path.
            // Reading only the current selection could exempt a different item if
            // a native/modded CanDrink callback changes quick slots reentrantly.
            CodeInstruction store = code[finds[0] + 1];
            if (store.opcode == OpCodes.Stloc_0) loadItem = new CodeInstruction(OpCodes.Ldloc_0);
            else if (store.opcode == OpCodes.Stloc_1) loadItem = new CodeInstruction(OpCodes.Ldloc_1);
            else if (store.opcode == OpCodes.Stloc_2) loadItem = new CodeInstruction(OpCodes.Ldloc_2);
            else if (store.opcode == OpCodes.Stloc_3) loadItem = new CodeInstruction(OpCodes.Ldloc_3);
            else if (store.opcode == OpCodes.Stloc || store.opcode == OpCodes.Stloc_S)
                loadItem = new CodeInstruction(store.opcode == OpCodes.Stloc ? OpCodes.Ldloc : OpCodes.Ldloc_S, store.operand);
            else throw new InvalidOperationException("Native selected potion local changed.");
            return gates[0];
        }

        private static IEnumerable<CodeInstruction> AllowRabbitThroughTension(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int gate = FindTensionCall(code, out CodeInstruction loadItem);
            // Keep the native check and every surrounding use/animation restriction.
            // Only filter its Boolean result for this exact server-side attempt.
            code.InsertRange(gate + 1, new[] { new CodeInstruction(OpCodes.Ldarg_0), loadItem,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RabbitPotionNativeHooks), nameof(FilterTensionBlock))) });
            return code;
        }

        private static bool FilterTensionBlock(bool blocked, ItemController controller, NewItemOwnInstance item)
        {
            if (!blocked || !Available || !NetworkServer.active) return blocked;
            try
            {
                if (!SessionSettings.RabbitPotionsForUse.Infinite || item == null || item.Quantity <= 0 ||
                    !CanRetainPotion(item.EntityID)) return blocked;
                PlayerAvatar player = controller?.Avatar as PlayerAvatar;
                if (!PlayerReady(controller, player, player?.spawner)) return blocked;
                int selected = controller.SelectedQuickSlotIdx;
                if (selected < 0 || selected >= controller.quickSlotTable.Count ||
                    !ReferenceEquals(item, player.Inventory.FindItem(player.Inventory.IdxToPos(controller.quickSlotTable[selected].idx)))) return blocked;
                ItemEntity entity = item.Entity;
                if (!entity || entity.type != EItemType.Potion || !entity.resourcePrefab) return blocked;
                PotionEffect effect = entity.resourcePrefab.GetComponent<PotionEffect>();
                return !effect || effect.GetType() != typeof(PotionEffect_Regeneration);
            }
            catch { return blocked; } // A stale item or connection keeps native Tension behavior.
        }
    }
}
