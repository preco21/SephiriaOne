using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameRabbitLevelUpCompatibilityTests
{
    // Read installed IL and pure addon guards only; never initialize Unity or grant an item.
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        MethodInfo Method(string type, string name, params Type[] args) =>
            AccessTools.DeclaredMethod(Type(type), name, args) ?? throw new Exception(type + "." + name + " missing.");
        var hooks = addon.GetType("SephiriaOne.RabbitLevelUpHooks", true)!;
        var earned = Method("LevelController", "LocalAddExp", typeof(int));
        var levelUp = Method("LevelController", "LevelUpOnServer");
        var initialize = Method("LevelController", "Initialize", typeof(int), typeof(int));
        var localInitialize = Method("LevelController", "LocalInitialize", typeof(int), typeof(int));
        var generate = Method("LevelController", "GenerateItem", typeof(int));
        var validate = AccessTools.DeclaredMethod(hooks, "ValidateEarnedLevelShape")!;
        Require(earned.IsPrivate && earned.ReturnType == typeof(void) &&
            (bool)validate.Invoke(null, new object[] { earned })!, "installed earned-level shape was rejected");

        var original = Code(earned);
        int nativeCall = original.FindIndex(i => Equals(i.operand, levelUp));
        Require(original.Count(i => Equals(i.operand, levelUp)) == 1 &&
            original.Any(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "Mirror.NetworkServer" && m.Name == "get_active"),
            "earned experience must remain server-only with one level-up call in its loop");
        int increment = original.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_NetworkcurrentLevel");
        Require(increment >= 4 && increment < nativeCall &&
            original[increment - 1].opcode == OpCodes.Add && original[increment - 2].opcode == OpCodes.Ldc_I4_1 &&
            original[increment - 3].operand is FieldInfo currentLevel && currentLevel.Name == "currentLevel",
            "the native synchronized level must increment before the earned-level reward boundary");

        var wrap = AccessTools.DeclaredMethod(hooks, "WrapEarnedLevel")!;
        var complete = AccessTools.DeclaredMethod(hooks, "CompleteEarnedLevel")!;
        Require(complete.IsStatic && complete.ReturnType == typeof(void) &&
            complete.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { Type("LevelController") }),
            "the replacement must consume the same native LevelController receiver");
        var before = original.Select(i => new CodeInstruction(i)).ToList();
        var rewritten = ((IEnumerable<CodeInstruction>)wrap.Invoke(null, new object[] { original })!).ToList();
        Require(rewritten.Count == before.Count && rewritten.Count(i => Equals(i.operand, complete)) == 1 &&
            !rewritten.Any(i => Equals(i.operand, levelUp)), "exactly one earned-level call must be replaced");
        for (int i = 0; i < before.Count; i++)
        {
            Require(rewritten[i].labels.SequenceEqual(before[i].labels) && rewritten[i].blocks.SequenceEqual(before[i].blocks),
                "the transpiler changed native branch/exception metadata at " + i);
            Require(i == nativeCall
                ? rewritten[i].opcode == OpCodes.Call && Equals(rewritten[i].operand, complete)
                : rewritten[i].opcode == before[i].opcode && SameOperand(rewritten[i].operand, before[i].operand),
                "the transpiler changed unrelated native instructions at " + i);
        }
        Require(Code(complete).Count(i => Equals(i.operand, levelUp)) == 1,
            "the replacement must invoke the original level-up exactly once");
        foreach (string mutation in new[] { "missing", "duplicate", "increment" })
        {
            var unsafeCode = Code(earned);
            if (mutation == "missing") unsafeCode.RemoveAll(i => Equals(i.operand, levelUp));
            else if (mutation == "duplicate") unsafeCode.Add(new CodeInstruction(OpCodes.Call, levelUp));
            else unsafeCode.RemoveAt(increment);
            bool rejected = false;
            try { ((IEnumerable<CodeInstruction>)wrap.Invoke(null, new object[] { unsafeCode })!).ToList(); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "unsafe " + mutation + " earned-level shape was accepted");
        }
        foreach (var unrelated in new[] { initialize, localInitialize, generate })
            Require(!(bool)validate.Invoke(null, new object[] { unrelated })!,
                "initialization or direct item generation must not qualify as an earned level: " + unrelated.Name);
        var restore = Code(AccessTools.DeclaredMethod(Type("PlayerSpawner"), "Initialize"));
        Require(restore.Any(i => Equals(i.operand, initialize)) && restore.Any(i => Equals(i.operand, generate)),
            "saved-level restoration and queued rewards must retain the unpatched initialization/generation paths");
        Require(Code(Method("LevelController", "AddExp", typeof(int))).Any(i => Equals(i.operand, earned)) &&
            Code(Method("LevelController", "UserCode_CmdAddExp__Int32", typeof(int))).Any(i => Equals(i.operand, earned)),
            "host experience and guest experience commands must reach the same native server boundary");

        VerifyInventory(game);
        VerifyCatalog(game, addon);
        VerifyLifecycle(addon, hooks);
        Console.WriteLine("Verified Rabbit earned-level boundary, one-call native IL preservation, initialization exclusions, 19-potion identity/effect guards, native inventory/temporary-storage guest synchronization and lifecycle contracts (not live gameplay or asset initialization).");
    }

    private static void VerifyInventory(Assembly game)
    {
        var inventory = game.GetType("GridInventory", true)!;
        var metadata = game.GetType("ItemMetadata", true)!;
        var add = AccessTools.DeclaredMethod(inventory, "LocalAddItem",
            new[] { typeof(int), typeof(int), typeof(sbyte), typeof(int), typeof(bool), typeof(bool) })!;
        var code = Code(add);
        Require(add.IsPublic && !add.IsStatic && add.ReturnType == typeof(bool) &&
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "Mirror.NetworkServer" && m.Name == "get_active") &&
            code.Any(i => i.operand is FieldInfo f && f.Name == "writePermission"),
            "checked native item addition must retain server and permission gates");
        var writePermission = AccessTools.Field(inventory, "writePermission")!;
        Require(writePermission.IsPrivate && !writePermission.IsStatic && writePermission.FieldType == typeof(bool),
            "borrowing an existing native write scope requires the private instance boolean permission flag");
        foreach (string method in new[] { "MaxStackCount", "SetItem", "NotifyAddItem", "TryAddItemToSubBagFallback" })
            Require(code.Any(i => i.operand is MethodInfo m && m.Name == method), "native item addition no longer covers " + method);
        var permission = inventory.GetNestedType("Permission", BindingFlags.Public | BindingFlags.NonPublic)!;
        Require(AccessTools.Constructor(permission, new[] { inventory }) != null && typeof(IDisposable).IsAssignableFrom(permission),
            "native inventory write permission must remain a disposable scope");
        var constructor = Code(AccessTools.Constructor(inventory));
        foreach (var pair in new[] { ("inventoryMatrix", "Mirror.SyncDictionary`2"), ("temporaryInventory", "Mirror.SyncList`1") })
        {
            var field = AccessTools.Field(inventory, pair.Item1)!;
            Require(field.IsPublic && field.IsInitOnly && field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition().FullName == pair.Item2,
                pair.Item1 + " must remain a native synchronized inventory collection");
            int load = constructor.FindLastIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field));
            Require(load >= 0 && constructor.Skip(load + 1).Take(2).Any(i => i.operand is MethodInfo m && m.Name == "InitSyncObject"),
                pair.Item1 + " must be registered for stock guest synchronization");
        }
        Require(AccessTools.Field(inventory, "temporaryInventory").FieldType.GetGenericArguments().Single() == metadata &&
            AccessTools.Constructor(metadata, new[] { typeof(int), typeof(int), typeof(sbyte) }) != null,
            "overflow must accept the same native item identity and quantity");
        var nativeTemp = Code(AccessTools.DeclaredMethod(game.GetType("PlayerSpawner"), "UserCode_CmdBuyPocketDimensionItem__Int32__Boolean"));
        Require(nativeTemp.Any(i => i.operand is FieldInfo f && f.Name == "temporaryInventory") &&
            nativeTemp.Any(i => i.operand is MethodInfo m && m.Name == "Add" && m.DeclaringType?.FullName?.StartsWith("Mirror.SyncList`1", StringComparison.Ordinal) == true),
            "stock server item grants must still support temporary-inventory overflow");
    }

    private static void VerifyLifecycle(Assembly addon, Type hooks)
    {
        var feature = addon.GetType("SephiriaOne.RabbitLevelUpFeature", true)!;
        var entry = addon.GetType("SephiriaOne.Entry", true)!;
        foreach (var pair in new[] { ("OnModLoaded", "Initialize"), ("OnDatabasesReady", "OnDatabasesReady"), ("OnModUnloaded", "Shutdown") })
            Require(Code(AccessTools.DeclaredMethod(entry, pair.Item1)).Any(i => Equals(i.operand, AccessTools.DeclaredMethod(feature, pair.Item2))),
                "Entry missing Rabbit level-up lifecycle " + pair.Item2);
        var install = Code(AccessTools.DeclaredMethod(hooks, "Install"));
        Require(install.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "LocalAddExp")) == 1 &&
            !install.Any(i => i.opcode == OpCodes.Ldstr && i.operand is string name &&
                (name == "Initialize" || name == "LocalInitialize" || name == "GenerateItem" || name == "LevelUpOnServer")),
            "only the earned experience method may be selected as the hook target");
        var complete = Code(AccessTools.DeclaredMethod(hooks, "CompleteEarnedLevel"));
        Require(complete.Count(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "GridInventory" && m.Name == "LocalAddItem") == 1 &&
            complete.Any(i => i.operand is ConstructorInfo c && c.DeclaringType?.FullName == "GridInventory+Permission") &&
            complete.Any(i => i.operand is FieldInfo f && f.DeclaringType?.Name == "GridInventory" && f.Name == "temporaryInventory") &&
            !complete.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "GridInventory" && m.Name == "AddStartingItem"),
            "level-up delivery must use a checked native inventory grant with stock temporary overflow, without run-restocked ownership");
    }

    private static void VerifyCatalog(Assembly game, Assembly addon)
    {
        var catalog = addon.GetType("SephiriaOne.RabbitLevelUpCatalog", true)!;
        var expected = new (int Id, string Name)[] {
            (28, "NebbiolosStubbornness"), (29, "UgniBlancsMist"), (30, "TempranillosPassion"), (31, "MalbecsDepth"),
            (33, "LargeDicePotion"), (34, "SmallDicePotion"), (35, "FinalDamagePotion"), (38, "PotionOfEnchant"),
            (39, "MushroomSoup"), (40, "PowerPotionMinor"), (41, "FlamePotionMinor"), (42, "FrostPotionMinor"),
            (43, "LightningPotionMinor"), (46, "RandomComboPotion"), (47, "DefensePotion_Big"), (48, "EvasionPotion_Big"),
            (49, "DefensePotion"), (50, "EvasionPotion"), (51, "ElementPotion")
        };
        Require(((int[])AccessTools.Field(catalog, "Ids").GetValue(null)!).SequenceEqual(expected.Select(p => p.Id)) &&
            ((string[])AccessTools.Field(catalog, "Names").GetValue(null)!).SequenceEqual(expected.Select(p => p.Name)),
            "the audited 19 native potion IDs/name keys changed (including hidden 35/46 and excluding HP/MP/random-HP/MP effects)");
        var validate = Code(AccessTools.DeclaredMethod(catalog, "Validate"));
        foreach (string field in new[] { "id", "aName", "type", "activeType", "resourcePrefab" })
            Require(validate.Any(i => i.operand is FieldInfo f && f.DeclaringType?.Name == "ItemEntity" && f.Name == field),
                "catalog validation must verify native ItemEntity." + field);
        Require(validate.Any(i => i.operand is FieldInfo f && f.DeclaringType?.Name == "LocalizedString" && f.Name == "key") &&
            validate.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Item_")) &&
            validate.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "_Name")) &&
            validate.Any(i => i.operand is MethodInfo m && m.Name == "FindItemById") &&
            validate.Any(i => i.operand is MethodInfo m && m.Name == "TryGetComponent" && m.IsGenericMethod &&
                m.GetGenericArguments().Single().Name == "WieldingPotion") &&
            validate.Any(i => i.operand is FieldInfo f && f.DeclaringType?.Name == "WieldingPotion" && f.Name == "effect"),
            "catalog validation must resolve native name keys and actual potion effect components");
        Require(Convert.ToInt32(Enum.Parse(game.GetType("EItemType", true)!, "Potion")) == 2,
            "native potion category changed");
        foreach (string effect in new[] { "PotionEffect_DicePotion", "PotionEffect_Enchant", "PotionEffect_ElementalDamageBoost", "PotionEffect_RandomCombo", "PotionEffect_StatusInstance" })
            Require(game.GetType(effect) != null && validate.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, effect)),
                "catalog expected effect is no longer verified: " + effect);
        var check = AccessTools.DeclaredMethod(catalog, "Validate");
        foreach (string boundary in new[] { "Load", "Pick" })
            Require(Code(AccessTools.DeclaredMethod(catalog, boundary)).Any(i => Equals(i.operand, check)),
                "catalog must validate native assets at " + boundary);
    }

    private static List<CodeInstruction> Code(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();
    private static bool SameOperand(object? left, object? right) => left is Label[] a && right is Label[] b ? a.SequenceEqual(b) : Equals(left, right);
    private static void Require(bool ok, string message) { if (!ok) throw new Exception("Rabbit level-up contract: " + message); }
}
