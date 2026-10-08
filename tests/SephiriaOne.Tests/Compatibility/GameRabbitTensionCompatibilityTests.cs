using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameRabbitTensionCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var controller = game.GetType("ItemController", true)!;
        var hooks = addon.GetType("SephiriaOne.RabbitPotionNativeHooks", true)!;
        var use = AccessTools.DeclaredMethod(controller, "LocalUseItemKeyDown", Type.EmptyTypes)!;
        var gate = AccessTools.DeclaredMethod(controller, "IsHostilityBlockingPotion", Type.EmptyTypes)!;
        var validate = AccessTools.DeclaredMethod(hooks, "ValidateTensionShape")!;
        Require((bool)validate.Invoke(null, new object[] { use })!, "Installed Tension/potion admission contract rejected.");
        var original = PatchProcessor.GetOriginalInstructions(use).ToList();
        int gateIndex = original.FindIndex(i => Equals(i.operand, gate));
        int canDrink = original.FindIndex(i => i.operand is MethodInfo m && m.Name == "CanDrink");
        int notice = original.FindIndex(i => i.operand is MethodInfo m && m.Name == "TargetNoticeHostilityCantUsePotion");
        int spawn = original.FindIndex(i => i.operand is MethodInfo m && m.Name == "Spawn" && m.DeclaringType?.Name == "NetworkServer");
        Require(canDrink >= 0 && canDrink < gateIndex && notice > gateIndex && spawn > notice,
            "Tension must remain after native CanDrink and before potion creation/animation.");
        Require(Calls(AccessTools.DeclaredMethod(controller, "UseItemKeyDown")!, use) &&
            Calls(AccessTools.DeclaredMethod(controller, "UserCode_CmdUseItemKeyDown")!, use),
            "Local and unmodified guest input must reach the same server-side Tension boundary.");
        var rewrite = AccessTools.DeclaredMethod(hooks, "AllowRabbitThroughTension")!;
        var rewritten = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { original })!).ToList();
        Require(rewritten.Count == original.Count + 3 && rewritten[gateIndex + 1].opcode == OpCodes.Ldarg_0 &&
            rewritten[gateIndex + 3].operand is MethodInfo filter && filter.Name == "FilterTensionBlock",
            "Only the native Tension result may be filtered, with the exact controller and item.");
        rewritten.RemoveRange(gateIndex + 1, 3);
        Require(rewritten.Zip(original).All(pair => pair.First.opcode == pair.Second.opcode && Equals(pair.First.operand, pair.Second.operand)),
            "Tension exemption changed native potion checks, creation or animation instructions.");
        foreach (string mutation in new[] { "duplicate-gate", "missing-item", "branch" })
        {
            var invalid = PatchProcessor.GetOriginalInstructions(use).ToList();
            if (mutation == "duplicate-gate") invalid.Add(new CodeInstruction(OpCodes.Call, gate));
            else if (mutation == "missing-item") invalid.RemoveAll(i => i.operand is MethodInfo m && m.Name == "FindItem");
            else invalid[gateIndex + 1].opcode = OpCodes.Brtrue;
            try
            {
                ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { invalid })!).ToList();
                throw new Exception("Unsafe Tension shape accepted: " + mutation);
            }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
        }
        Console.WriteLine("Verified native Tension host/guest admission, exact-item filtering, preserved native use instructions and rejection of changed IL (not live gameplay).");
    }

    private static bool Calls(MethodInfo method, MethodInfo target) => PatchProcessor.GetOriginalInstructions(method).Any(i => Equals(i.operand, target));
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
