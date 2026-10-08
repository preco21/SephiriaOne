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
        VerifyAlerts(game, addon);
        VerifySharedHealVisuals(game, addon);
        GameRabbitTensionCompatibilityTests.Run(game, addon);
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

        var death = PatchProcessor.GetOriginalInstructions(Method("UnitAvatar", "Die", typeof(int), Type("DamageInstance"))).ToList();
        int deadWrite = death.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_NetworkIsDead");
        int deathEvent = death.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnDie");
        if (deadWrite < 0 || deathEvent <= deadWrite)
            throw new Exception("Native death must publish IsDead before notifying death observers.");
        var forceDeath = Method("UnitAvatar", "ForceDie");
        var cancel = Method("UnitAvatar", "CancelCurrentAction");
        var hit = Method("UnitAvatar", "StartHitFeedback", typeof(int), typeof(float), typeof(float));
        var cancelItem = Method("ItemController", "CancelAction");
        var itemCleanup = PatchProcessor.GetOriginalInstructions(cancelItem).ToList();
        var regeneration = PatchProcessor.GetOriginalInstructions(effect).ToList();
        int drinkEvent = regeneration.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == Type("PotionEffect") && m.Name == "CreateEffect_OnDrink");
        int regenerationHeal = regeneration.FindIndex(i => i.operand is MethodInfo m && m.Name == "HealPercent");
        if (!Calls(forceDeath, cancel) || !Calls(hit, cancel) ||
            !death.Any(i => Equals(i.operand, hit)) ||
            !PatchProcessor.GetOriginalInstructions(cancel).Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "IAvatarStuckModule" && m.Name == "CancelAction") ||
            !itemCleanup.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "NetworkServer" && m.Name == "Destroy") ||
            !itemCleanup.Any(i => i.operand is MethodInfo m && m.Name == "set_NetworkcurrentWieldingItem") ||
            drinkEvent < 0 || regenerationHeal <= drinkEvent)
            throw new Exception("Re-audit native death/wield cleanup and Survival-before-healing order.");

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

    private static void VerifyAlerts(Assembly game, Assembly addon)
    {
        var alert = addon.GetType("SephiriaOne.NativePlayerAlert", true)!;
        if (!(bool)AccessTools.DeclaredMethod(alert, "ValidateTransport")!.Invoke(null, null)!)
            throw new Exception("Guest alert RPC does not match installed serialization.");
        var unit = game.GetType("UnitAvatar", true)!;
        var nativeSend = AccessTools.DeclaredMethod(unit, "RpcShowDamageParticle")!;
        var send = AccessTools.DeclaredMethod(alert, "Show")!;
        MethodInfo[] Writes(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method)
            .Where(i => i.operand is MethodInfo m && m.Name.StartsWith("Write", StringComparison.Ordinal) && m.Name != "WriteSystemMessage")
            .Select(i => (MethodInfo)i.operand).ToArray();
        if (!Writes(send).SequenceEqual(Writes(nativeSend)))
            throw new Exception("Addon alert must use exactly the native payload serializers in order.");
        var target = AccessTools.Method(unit, "SendTargetRPCInternal")!;
        var targetCode = PatchProcessor.GetOriginalInstructions(target).ToList();
        if (!targetCode.Any(i => i.operand is FieldInfo f && f.DeclaringType?.FullName == "Mirror.RpcMessage" && f.Name == "functionHash") ||
            !targetCode.Any(i => i.operand is MethodInfo m && m.Name == "Send" && m.IsGenericMethod &&
                m.GetGenericArguments().Single().FullName == "Mirror.RpcMessage") ||
            targetCode.Any(i => i.operand is MethodInfo m && m.Name.Contains("SendTo")))
            throw new Exception("Targeted feedback must use native RPC dispatch to one connection.");
        var receive = AccessTools.DeclaredMethod(unit, "UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar")!;
        var receiveCode = PatchProcessor.GetOriginalInstructions(receive).ToList();
        if (!receiveCode.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "UI_DamageParticle" && m.Name == "SetDamage") ||
            receiveCode.Any(i => i.operand is MethodInfo m && (m.Name.StartsWith("set_Network", StringComparison.Ordinal) || m.Name.Contains("Heal") || m.Name.Contains("PotionDrinkEvent"))))
            throw new Exception("Guest floating feedback must remain presentation only.");
        var connect = AccessTools.DeclaredMethod(game.GetType("UI_SystemMessage", true), "Connect")!;
        if (!PatchProcessor.GetOriginalInstructions(connect).Any(i => i.operand is MethodInfo m && m.Name == "add_OnWriteSystemMessage"))
            throw new Exception("Host system-message UI subscription changed.");
        Console.WriteLine("Verified local system-message UI and private guest floating-text payload, receiver and Mirror dispatch contracts (not live UI).");
    }

    private static void VerifySharedHealVisuals(Assembly game, Assembly addon)
    {
        var unit = game.GetType("UnitAvatar", true)!;
        var hooks = addon.GetType("SephiriaOne.RabbitPotionNativeHooks", true)!;
        var validate = AccessTools.DeclaredMethod(hooks, "ValidateHealVisual")!;
        if (validate == null || !(bool)validate.Invoke(null, null)!)
            throw new Exception("Installed green healing particle RPC contract is unavailable.\n" +
                string.Join("\n", new[] { "RpcBloodFestivalHealFx", "InvokeUserCode_RpcBloodFestivalHealFx", "UserCode_RpcBloodFestivalHealFx" }
                    .Select(name => name + ":\n" + string.Join("\n", PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(unit, name))))));
        var send = AccessTools.DeclaredMethod(unit, "RpcBloodFestivalHealFx")!;
        var receive = AccessTools.DeclaredMethod(unit, "InvokeUserCode_RpcBloodFestivalHealFx")!;
        var body = AccessTools.DeclaredMethod(unit, "UserCode_RpcBloodFestivalHealFx")!;
        var registration = PatchProcessor.GetOriginalInstructions(unit.TypeInitializer!).ToList();
        int native = registration.FindIndex(i => Equals(i.operand, "System.Void UnitAvatar::RpcBloodFestivalHealFx()"));
        if (native < 0 || !registration.Skip(native + 1).Take(5).Any(i => Equals(i.operand, receive)) ||
            !registration.Skip(native + 1).Take(6).Any(i => i.operand is MethodInfo m && m.Name == "RegisterRpc"))
            throw new Exception("Unmodified guests must have the native green FX receiver registered.");
        foreach (string change in new[] { "hash", "name", "channel", "owner", "payload", "reader", "gameplay", "prefab" })
        {
            var senderCode = PatchProcessor.GetOriginalInstructions(send).ToList();
            var receiveCode = PatchProcessor.GetOriginalInstructions(receive).ToList();
            var bodyCode = PatchProcessor.GetOriginalInstructions(body).ToList();
            int dispatch = senderCode.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            switch (change)
            {
                case "hash": senderCode.Single(i => Equals(i.operand, 184881409)).operand = 123; break;
                case "name": senderCode.Single(i => i.opcode == System.Reflection.Emit.OpCodes.Ldstr).operand = "different"; break;
                case "channel": senderCode[dispatch - 2].opcode = System.Reflection.Emit.OpCodes.Ldc_I4_1; break;
                case "owner": senderCode[dispatch - 1].opcode = System.Reflection.Emit.OpCodes.Ldc_I4_0; break;
                case "payload": senderCode.Insert(0, new(System.Reflection.Emit.OpCodes.Call, AccessTools.DeclaredMethod(unit, "HealPercent", new[] { typeof(float) }))); break;
                case "reader": receiveCode.Insert(0, new(System.Reflection.Emit.OpCodes.Call, typeof(string).GetMethod("IsNullOrEmpty")!)); break;
                case "gameplay": bodyCode.Insert(0, new(System.Reflection.Emit.OpCodes.Call, AccessTools.DeclaredMethod(unit, "HealPercent", new[] { typeof(float) }))); break;
                case "prefab": bodyCode.RemoveAll(i => i.operand is FieldInfo f && f.Name == "bloodFestivalHealFxPrefab"); break;
            }
            if ((bool)AccessTools.DeclaredMethod(hooks, "ValidateHealVisualCode")!.Invoke(null, new object[] { senderCode, receiveCode, bodyCode })!)
                throw new Exception("Unsafe shared-heal visual contract accepted: " + change);
        }
        Console.WriteLine("Verified native shared-heal green FX broadcast, zero payload, guest registration and visual-only receiver; changed contracts rejected (not live rendering).");
    }
}
