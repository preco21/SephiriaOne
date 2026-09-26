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
        if (!(bool)AccessTools.DeclaredMethod(hooks, "ValidateMpSetter")!.Invoke(null, null)!)
            throw new Exception("MP fee requires the native callback-free synchronized setter.");
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
        var guard = AccessTools.DeclaredMethod(hooks, "GuardCompletedDrink")!;
        var guarded = ((IEnumerable<CodeInstruction>)guard.Invoke(null,
            new object[] { PatchProcessor.GetOriginalInstructions(consume) })!).ToList();
        if (guarded.Count(i => i.operand is MethodInfo m && m.DeclaringType == hooks && m.Name == "DrinkAtCompletion") != 1)
            throw new Exception("Exactly one completed native drink must be guarded.");
        foreach (string mutation in new[] { "catch", "cleanup", "event" })
        {
            var unsafeCode = PatchProcessor.GetOriginalInstructions(consume).ToList();
            if (mutation == "catch") foreach (var instruction in unsafeCode) instruction.blocks.Clear();
            else if (mutation == "cleanup") unsafeCode.RemoveAll(i => i.operand is MethodInfo m && m.Name == "RpcWieldItem");
            else unsafeCode.RemoveAll(i => i.operand is FieldInfo f && f.Name == "OnDrinkPotionServerside");
            try
            {
                ((IEnumerable<CodeInstruction>)guard.Invoke(null, new object[] { unsafeCode })!).ToList();
                throw new Exception("Unsafe consumer " + mutation + " shape accepted.");
            }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
        }
        var survival = Method("PassiveObject_PotionAndRandomStat", "HandleDrinkPotion", Type("PotionEffect"));
        var enable = Method("PassiveObject_PotionAndRandomStat", "OnEffectEnabled", Type("PlayerAvatar"), typeof(bool));
        if (!survival.IsPrivate || survival.ReturnType != typeof(void) ||
            AccessTools.Field(Type("PassiveObject_PotionAndRandomStat"), "player")?.FieldType != Type("PlayerAvatar") ||
            !PatchProcessor.GetOriginalInstructions(survival).Any(i => i.operand is MethodInfo m && m.Name == "AddOrphanedStatusInstance") ||
            !PatchProcessor.GetOriginalInstructions(enable).Any(i => i.operand is MethodInfo m && m.Name == "add_OnDrinkPotion"))
            throw new Exception("Survival targeted callback contract changed.");
        var useMp = PatchProcessor.GetOriginalInstructions(Method("UnitAvatar", "UseMp", typeof(int))).ToList();
        int mpCallback = useMp.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnMpUsedServerside");
        int mpWrite = useMp.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_Networkmp");
        if (mpCallback < 0 || mpWrite <= mpCallback)
            throw new Exception("Re-audit native UseMp callback ordering before changing fee semantics.");

        var healPercent = Method("UnitAvatar", "HealPercent", typeof(float));
        var healBody = Method("UnitAvatar", "HealPercent", typeof(float), typeof(bool), typeof(bool));
        if (!Calls(healPercent, healBody) ||
            !PatchProcessor.GetOriginalInstructions(healBody).Any(i => i.operand is MethodInfo m && m.Name == "set_Networkhp"))
            throw new Exception("Shared percentage healing must retain native HP replication.");
        var share = AccessTools.DeclaredMethod(hooks, "Share")!;
        if (!Calls(share, healPercent)) throw new Exception("Rabbit sharing must retain the HP-only recipient path.");
        foreach (var hpOnly in new[] { share, healPercent, healBody, AccessTools.PropertySetter(Type("UnitAvatar"), "Networkhp")! })
            if (PatchProcessor.GetOriginalInstructions(hpOnly).Any(i =>
                i.operand is MethodInfo m && (m.Name.Contains("PotionDrinkEvent") || m.Name == "CreateEffect_OnDrink" ||
                    m.Name == "HandleDrinkPotion" || m.Name == "AddOrphanedStatusInstance") ||
                i.operand is FieldInfo f && (f.Name == "OnDrinkPotion" || f.Name == "OnDrinkPotionServerside")))
                throw new Exception("Shared HP healing unexpectedly invokes potion/talent events: " + hpOnly.Name);
        var passiveEvents = PatchProcessor.GetOriginalInstructions(enable).Where(i =>
            i.operand is MethodInfo m && m.Name.StartsWith("add_", StringComparison.Ordinal)).ToList();
        if (passiveEvents.Count != 1 || ((MethodInfo)passiveEvents[0].operand).Name != "add_OnDrinkPotion")
            throw new Exception("Survival must subscribe only to the drink event, never shared HP changes.");
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
        Console.WriteLine("Verified installed rabbit potion catch/cleanup guard, synchronized MP fee, targeted Survival callback, HP-only sharing without recipient potion events, healing replication, costume tooltip and addon lifecycle contracts (not live multiplayer/UI).");
    }

    private static bool Calls(MethodInfo caller, MethodInfo target) =>
        PatchProcessor.GetOriginalInstructions(caller).Any(i => Equals(i.operand, target));
}
