using System.Reflection;
using HarmonyLib;

internal static class GameRabbitCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name, params Type[] args) =>
            AccessTools.DeclaredMethod(Type(type), name, args) ?? throw new Exception(type + "." + name + " missing.");
        var drink = Method("WieldingPotion", "Drink", typeof(bool).MakeByRefType(), typeof(int));
        var effect = Method("PotionEffect_Regeneration", "CreateEffect_OnDrink", Type("UnitAvatar"));
        var consume = Method("ItemController", "DrinkPotionAnimation");
        var hooks = addon.GetType("SephiriaOne.RabbitPotionNativeHooks", true)!;
        foreach (var boundary in new[] { ("ValidateDrinkShape", drink), ("ValidateConsumerShape", consume) })
        {
            var validate = AccessTools.DeclaredMethod(hooks, boundary.Item1) ?? throw new Exception("Rabbit shape guard missing: " + boundary.Item1);
            if (!(bool)validate.Invoke(null, new object[] { boundary.Item2 })!)
                throw new Exception("Installed potion contract changed: " + boundary.Item2.Name);
        }
        var capture = AccessTools.DeclaredMethod(hooks, "CaptureHealCall")!;
        var rewritten = ((IEnumerable<CodeInstruction>)capture.Invoke(null,
            new object[] { PatchProcessor.GetOriginalInstructions(effect) })!).ToList();
        if (rewritten.Count(i => i.operand is MethodInfo m && m.DeclaringType == hooks && m.Name == "HealAndCapture") != 1)
            throw new Exception("Exactly one native potion healing call must be wrapped.");

        var healPercent = Method("UnitAvatar", "HealPercent", typeof(float));
        var healBody = Method("UnitAvatar", "HealPercent", typeof(float), typeof(bool), typeof(bool));
        if (!Calls(healPercent, healBody) ||
            !PatchProcessor.GetOriginalInstructions(healBody).Any(i => i.operand is MethodInfo m && m.Name == "set_Networkhp"))
            throw new Exception("Shared percentage healing must retain native HP replication.");
        var onDrink = Method("PotionEffect", "CreateEffect_OnDrink", Type("UnitAvatar"));
        if (!PatchProcessor.GetOriginalInstructions(onDrink).Any(i => i.operand is MethodInfo m && m.Name == "ReceivePotionDrinkEvent"))
            throw new Exception("Native successful potion-use event changed.");

        var panel = Type("UI_CostumePanel");
        var update = Method("UI_CostumePanel", "UpdateData", Type("CostumeEntity"));
        if (!update.IsPrivate || update.ReturnType != typeof(void) ||
            AccessTools.Field(panel, "tooltipEffectText")?.FieldType.Name != "TextMeshProUGUI" ||
            Method("UI_CostumePanel", "OnClosed").ReturnType != typeof(void) ||
            !PatchProcessor.GetOriginalInstructions(update).Any(i => i.operand is MethodInfo m && m.Name == "set_text"))
            throw new Exception("Native costume description contract changed.");
        foreach (string name in new[] { "RabbitPotionFeature", "RabbitDescriptionFeature" })
        {
            var feature = addon.GetType("SephiriaOne." + name, true)!;
            foreach (var pair in new[] { ("OnModLoaded", "Initialize"), ("OnModUnloaded", "Shutdown") })
                if (!Calls(AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.Entry"), pair.Item1), AccessTools.DeclaredMethod(feature, pair.Item2)))
                    throw new Exception("Missing rabbit lifecycle: " + name + "." + pair.Item2);
        }
        Console.WriteLine("Verified installed rabbit potion consumer/capture, native healing replication, costume tooltip and addon lifecycle contracts (not live multiplayer/UI).");
    }

    private static bool Calls(MethodInfo caller, MethodInfo target) =>
        PatchProcessor.GetOriginalInstructions(caller).Any(i => Equals(i.operand, target));
}
