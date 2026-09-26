using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static partial class RabbitPotionNativeHooks
    {
        [ThreadStatic] private static Stack<DrinkContext> completions;
        private sealed class RejectedDrink : Exception { }

        private static bool ValidateMpSetter()
        {
            MethodInfo setter = AccessTools.PropertySetter(typeof(UnitAvatar), "Networkmp");
            if (setter == null || setter.GetParameters().Length != 1 ||
                setter.GetParameters()[0].ParameterType != typeof(int)) return false;
            var code = PatchProcessor.GetOriginalInstructions(setter).ToList();
            var calls = code.Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).ToList();
            // Mirror's generated setter must have no gameplay callback. A changed contract disables all hooks.
            return calls.Count == 1 && calls[0].operand is MethodInfo method &&
                method.Name == "GeneratedSyncVarSetter" &&
                code[code.IndexOf(calls[0]) - 1].opcode == OpCodes.Ldnull &&
                code.Any(i => i.opcode == OpCodes.Ldflda && i.operand is FieldInfo field &&
                    field.DeclaringType == typeof(UnitAvatar) && field.Name == "mp");
        }

        private static bool ValidateRejectionCleanup(List<CodeInstruction> code, int drink, int decrease)
        {
            int begin = code.FindLastIndex(drink, i => i.blocks.Any(b => b.blockType == ExceptionBlockType.BeginExceptionBlock));
            int caught = code.FindIndex(drink + 1, i => i.blocks.Any(b => b.blockType == ExceptionBlockType.BeginCatchBlock));
            if (begin < 0 || caught <= decrease ||
                code.Skip(begin + 1).Take(caught - begin - 1).Any(i => i.blocks.Count != 0) ||
                !code[caught].blocks.Any(b => b.blockType == ExceptionBlockType.BeginCatchBlock &&
                    (b.catchType == typeof(object) || b.catchType == typeof(Exception)))) return false;
            int end = code.FindIndex(caught, i => i.blocks.Any(b => b.blockType == ExceptionBlockType.EndExceptionBlock));
            if (end < caught || code.Skip(caught).Take(end - caught + 1).Any(i =>
                i.opcode != OpCodes.Pop && i.opcode != OpCodes.Nop && i.opcode != OpCodes.Leave && i.opcode != OpCodes.Leave_S)) return false;
            // The native catch must leave to a path that still unwields the item. Reject unfamiliar shapes.
            var cleanup = code.FindIndex(end + 1, i => i.operand is MethodInfo m &&
                m.DeclaringType == typeof(ItemController) && m.Name == "RpcWieldItem");
            if (cleanup < 0 || code.Skip(end + 1).Take(cleanup - end - 1).Any(i => i.opcode == OpCodes.Ret)) return false;
            var exit = code.Skip(caught).Take(end - caught + 1).LastOrDefault(i =>
                i.opcode == OpCodes.Leave || i.opcode == OpCodes.Leave_S);
            if (!(exit?.operand is Label label)) return false;
            int path = code.FindIndex(end + 1, i => i.labels.Contains(label));
            var seen = new HashSet<int>();
            while (path > end && path <= cleanup && seen.Add(path))
            {
                if (path == cleanup) return true;
                var instruction = code[path];
                if (instruction.opcode == OpCodes.Br || instruction.opcode == OpCodes.Br_S)
                {
                    if (!(instruction.operand is Label target)) return false;
                    path = code.FindIndex(i => i.labels.Contains(target));
                }
                else if (instruction.opcode == OpCodes.Nop || instruction.opcode == OpCodes.Ldarg_0 ||
                    instruction.opcode == OpCodes.Ldc_I4_M1) path++;
                else return false;
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> GuardCompletedDrink(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            if (!ValidateConsumerCode(code))
                throw new InvalidOperationException("Native potion catch/cleanup changed; refusing completion guard.");
            var call = code.Single(i => i.operand is MethodInfo m && m.DeclaringType == typeof(WieldingPotion) &&
                m.Name == nameof(WieldingPotion.Drink));
            call.opcode = OpCodes.Call;
            call.operand = AccessTools.Method(typeof(RabbitPotionNativeHooks), nameof(DrinkAtCompletion));
            return code;
        }

        private static void DrinkAtCompletion(WieldingPotion potion, out bool decreased, int instanceId)
        {
            // This call stays inside the validated native catch-all, before both controller event and decrement.
            // No state is reserved at animation start, so cancellation cannot charge or replay anything.
            DrinkContext context = CaptureContext(potion, instanceId, allowDead: true);
            // A pending animation may reach the native consumer after death. Reject before any
            // event or MP charge rather than falling through to unprotected native consumption.
            if (context != null && context.Player.IsDead) throw new RejectedDrink();
            if (context != null && context.Settings.ConsumeMp && context.Settings.MpCostPerDrink > 0)
            {
                int cost = context.Settings.MpCostPerDrink;
                int balance = context.Player.mp;
                if (balance < cost) throw new RejectedDrink();
                // Fixed fee, including INFINITYMP. UseMp fires arbitrary procs before its subtraction; this
                // SyncVar write deliberately avoids those procs and does not reset native MP regeneration.
                // Never refund after callbacks: doing so could overwrite unrelated native MP changes.
                context.Player.Networkmp = balance - cost;
                if (context.Player.mp != balance - cost) throw new RejectedDrink();
            }
            if (completions == null) completions = new Stack<DrinkContext>();
            completions.Push(context);
            try { potion.Drink(out decreased, instanceId); }
            finally { if (completions.Count != 0) completions.Pop(); }
        }

        private static bool AllowSurvival(PassiveObject_PotionAndRandomStat __instance, PotionEffect effect)
        {
            DrinkContext drink = drinks != null && drinks.Count != 0 ? drinks.Peek() : null;
            return !IsNativeCompletion(drink) ||
                !ReferenceEquals(__instance.player, drink.Player) || !ReferenceEquals(effect, drink.Effect) ||
                !drink.Settings.SuppressSurvival || !SessionSettings.RabbitPotionsForUse.SuppressSurvival || !Current(drink, retainDeath: true);
        }

        private static bool IsNativeCompletion(DrinkContext drink) => drink != null &&
            completions != null && completions.Count != 0 && ReferenceEquals(drink, completions.Peek()?.NativeDrink);
    }
}
