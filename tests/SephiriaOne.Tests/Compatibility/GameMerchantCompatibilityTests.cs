using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class GameMerchantCompatibilityTests
{
    // Read native IL only: none of these checks instantiate Unity objects or start a server.
    internal static void Run(Assembly game, Assembly addon)
    {
        VerifyDeathBoundary(game);
        VerifyShopIsolation(game);
        VerifyReplication(game);
        VerifyHealthScaling(game, addon);
        VerifyFloorCompletion(game, addon);

        var hooks = addon.GetType("SephiriaOne.MerchantNativeHooks", true)!;
        var validate = RequiredMethod(hooks, "ValidateContracts");
        Require(validate.IsStatic && validate.ReturnType == typeof(bool), "Merchant contract validator signature changed.");
        Require((bool)validate.Invoke(null, null)!, "Installed merchant contracts were rejected by the runtime guard.");
        VerifyPrefix(hooks, "AllowCrime", new[] { game.GetType("UnitAI_NewBasic", true)! }, new[] { "npc" });
        VerifyPrefix(hooks, "HandleDamage", new[] { game.GetType("UnitAI_NewBasic", true)!, game.GetType("DamageInstance", true)! },
            new[] { "__instance", "damage" });
        Console.WriteLine("Verified installed merchant crime boundary, damage-before-death order, native safe stock and guest replication, threefold scaled HP and floor-ready event after native travelers (not live gameplay).");
    }

    private static void VerifyHealthScaling(Assembly game, Assembly addon)
    {
        var unit = game.GetType("UnitAvatar", true)!;
        var baseHp = AccessTools.DeclaredField(unit, "maxHp")!;
        var bonusHp = AccessTools.DeclaredField(unit, "finalMaxHp")!;
        var getter = AccessTools.PropertyGetter(unit, "MaxHp")!;
        var maximum = Code(getter);
        int formulaStart = maximum.FindIndex(i => Equals(i.operand, baseHp));
        int formulaEnd = maximum.FindIndex(formulaStart, i => i.opcode == OpCodes.Ret);
        var formula = maximum.Skip(formulaStart).Take(formulaEnd - formulaStart + 1)
            .Where(i => i.opcode != OpCodes.Ldarg_0 && i.opcode != OpCodes.Nop).ToList();
        Require(formula.Count == 8 && Equals(formula[0].operand, baseHp) && Equals(formula[1].operand, bonusHp) &&
            Equals(formula[2].operand, baseHp) && formula[3].opcode == OpCodes.Mul && formula[4].LoadsConstant(100f) &&
            formula[5].opcode == OpCodes.Div && formula[6].opcode == OpCodes.Add && formula[7].opcode == OpCodes.Ret &&
            maximum.Take(formulaStart).Any(i => i.operand is FieldInfo f && f.Name == "isHPCursed"),
            "Normal MaxHp must remain base HP * (1 + native percentage bonuses / 100); re-audit the threefold multiplier.");

        var addMax = RequiredMethod(unit, "AddMaxHp", typeof(float), typeof(bool));
        var addPercent = RequiredMethod(unit, "AddMaxHpPercent", typeof(float), typeof(bool));
        var increase = Code(addMax);
        int baseWrite = increase.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_NetworkmaxHp");
        int hpWrite = increase.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_Networkhp");
        Require(addMax.GetParameters()[1].DefaultValue is true && baseWrite >= 3 &&
            Equals(increase[baseWrite - 3].operand, baseHp) && increase[baseWrite - 2].opcode == OpCodes.Ldarg_1 &&
            increase[baseWrite - 1].opcode == OpCodes.Add && hpWrite > baseWrite &&
            increase.Skip(baseWrite + 1).Take(hpWrite - baseWrite - 1).Any(i => i.operand is FieldInfo f && f.Name == "isHPCursed") &&
            !increase.Any(i => Equals(i.operand, bonusHp) || i.operand is MethodInfo m && m.Name == "set_NetworkfinalMaxHp"),
            "AddMaxHp must update synchronized base/current HP in the normal branch without replacing native percentage scaling.");
        Require(Code(addPercent).Any(i => i.operand is MethodInfo m && m.Name == "set_NetworkfinalMaxHp"),
            "Native stage and multiplayer HP bonuses must remain percentage contributions.");

        var scaling = Code(RequiredMethod(addon.GetType("SephiriaOne.MerchantRuntime", true)!, "ApplyNativeScaling",
            unit, Assembly.Load("UnityEngine.CoreModule").GetType("UnityEngine.Vector2", true)!));
        int multiply = scaling.FindIndex(i => Equals(i.operand, addMax));
        int lastPercent = scaling.FindLastIndex(i => Equals(i.operand, addPercent));
        int heal = scaling.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == unit && m.Name == "HealPercent");
        Require(scaling.Count(i => Equals(i.operand, addMax)) == 1 && lastPercent >= 0 && multiply > lastPercent &&
            multiply >= 4 && Equals(scaling[multiply - 4].operand, baseHp) && scaling[multiply - 3].LoadsConstant(2f) &&
            scaling[multiply - 2].opcode == OpCodes.Mul && scaling[multiply - 1].LoadsConstant(1) &&
            heal > multiply && scaling[heal - 1].LoadsConstant(100f),
            "Extra merchant HP must triple the fully scaled native maximum using AddMaxHp(maxHp * 2), then heal to full; adding 200 percentage points is not equivalent.");
    }

    private static void VerifyFloorCompletion(Assembly game, Assembly addon)
    {
        var floor = game.GetType("FloorGenerator", true)!;
        var inner = RequiredMethod(floor, "GenerateInner");
        var notify = RequiredMethod(game.GetType("HorayModAPI", true)!, "NotifyFloorAllocatedClientside",
            typeof(string), typeof(string), floor);
        var complete = AccessTools.PropertySetter(floor, "GenerateSuccess")!;
        var generate = IteratorCode(RequiredMethod(floor, "Generate"));
        int innerCall = generate.FindIndex(i => Equals(i.operand, inner));
        int success = generate.FindIndex(i => Equals(i.operand, complete));
        int notification = generate.FindIndex(i => Equals(i.operand, notify));
        Require(innerCall >= 0 && success > innerCall && notification > success &&
            generate.Count(i => Equals(i.operand, notify)) == 1 && generate[success - 1].LoadsConstant(1) &&
            generate.Skip(success + 1).Take(notification - success - 1).All(i =>
                i.opcode.FlowControl != FlowControl.Branch && i.opcode.FlowControl != FlowControl.Cond_Branch && i.opcode != OpCodes.Ret),
            "The SDK floor-ready event must publish GenerateSuccess=true only after native inner generation.");
        // Merely calling GenerateInner creates an iterator. It must be yielded to Unity,
        // then resumed in a later state before the completed-floor notification.
        int yieldReturn = generate.FindIndex(innerCall + 1, i => i.opcode == OpCodes.Ret);
        int stateWrite = generate.FindIndex(innerCall + 1, i => i.opcode == OpCodes.Stfld &&
            i.operand is FieldInfo f && f.Name == "<>1__state");
        Require(generate[innerCall + 1].opcode == OpCodes.Stfld && generate[innerCall + 1].operand is FieldInfo current &&
            current.Name == "<>2__current" && stateWrite > innerCall && stateWrite < yieldReturn && yieldReturn < success &&
            generate[yieldReturn - 1].LoadsConstant(1), "GenerateInner must be awaited as the coroutine's yielded iterator.");
        int resumeState = ReadInt(generate[stateWrite - 1]);
        var dispatch = generate.Single(i => i.opcode == OpCodes.Switch).operand as Label[];
        Require(dispatch != null && resumeState >= 0 && resumeState < dispatch.Length,
            "Native floor coroutine resume-state dispatch changed.");
        int resumed = generate.FindIndex(i => i.labels.Contains(dispatch![resumeState]));
        Require(resumed > yieldReturn && resumed < success,
            "Floor readiness must execute in a state reached after GenerateInner finishes.");

        var travelers = RequiredMethod(game.GetType("DungeonManager", true)!, "SpawnTravelers", floor,
            Assembly.Load("UnityEngine.CoreModule").GetType("UnityEngine.Vector2", true)!);
        var enhanced = IteratorCode(RequiredMethod(game.GetType("EnhancedProceduralFloorGenerator", true)!, "GenerateInner"));
        Require(travelers.ReturnType == typeof(void) && enhanced.Count(i => Equals(i.operand, travelers)) == 1 &&
            !enhanced.Any(i => Equals(i.operand, notify)),
            "Native travelers must spawn synchronously inside GenerateInner before the outer floor-ready event.");
        var feature = addon.GetType("SephiriaOne.MerchantFeature", true)!;
        Require(Code(RequiredMethod(feature, "Initialize")).Any(i => i.operand is MethodInfo m &&
                m.DeclaringType == game.GetType("HorayModAPI") && m.Name == "add_OnFloorAllocatedClientside") &&
            Code(RequiredMethod(feature, "Shutdown")).Any(i => i.operand is MethodInfo m &&
                m.DeclaringType == game.GetType("HorayModAPI") && m.Name == "remove_OnFloorAllocatedClientside"),
            "Extra merchants must use the completed-floor SDK event and release their subscription on unload.");
    }

    private static List<CodeInstruction> IteratorCode(MethodInfo method)
    {
        var state = method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType ??
            throw new Exception(method.DeclaringType!.Name + "." + method.Name + " is no longer an iterator.");
        return Code(RequiredMethod(state, "MoveNext"));
    }

    private static int ReadInt(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldc_I4 || instruction.opcode == OpCodes.Ldc_I4_S) return Convert.ToInt32(instruction.operand);
        for (int value = -1; value <= 8; value++) if (instruction.LoadsConstant(value)) return value;
        throw new Exception("Expected a constant coroutine resume state.");
    }

    private static void VerifyDeathBoundary(Assembly game)
    {
        Type Type(string name) => game.GetType(name, true)!;
        var npc = Type("UnitAI_NewBasic");
        var unit = Type("UnitAvatar");
        var damage = Type("DamageInstance");
        var crime = RequiredMethod(Type("DungeonManager"), "NPCDeadCheckServerside", npc, damage);
        var damaged = RequiredMethod(npc, "OnDamaged", damage);
        var death = RequiredMethod(npc, "OnDie", damage);
        Require(crime.IsPublic && crime.ReturnType == typeof(void) && damaged.IsPrivate && death.IsPrivate,
            "Merchant hooks must target the native NPC callbacks and dedicated crime check.");

        var crimeCode = Code(crime);
        Require(crimeCode.Any(i => i.operand is FieldInfo f && f.DeclaringType == npc && f.Name == "socialID") &&
            crimeCode.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(string) && m.Name == "IsNullOrWhiteSpace") &&
            crimeCode.Any(i => i.operand is MethodInfo m && m.Name == "GetRelationValue") &&
            crimeCode.Any(i => i.LoadsConstant(21)), "Native crime eligibility changed; re-audit the exemption boundary.");
        int buffRead = crimeCode.FindIndex(i => i.operand is FieldInfo f && f.Name == "crimeDebuff" && f.DeclaringType == Type("DungeonManager"));
        var buffCalls = crimeCode.Select((i, index) => (i, index)).Where(p => p.i.operand is MethodInfo m &&
            m.DeclaringType == unit && m.Name == "ApplyBuff" && m.IsGenericMethod &&
            m.GetGenericArguments().SequenceEqual(new[] { Type("CharacterBuff") })).ToList();
        Require(buffRead >= 0 && buffCalls.Count == 1 && buffCalls[0].index > buffRead &&
            crimeCode.Any(i => i.operand is FieldInfo f && f.Name == "MultiplayerList" && f.DeclaringType == Type("PlayerSpawner")) &&
            crimeCode.Count(i => i.operand is MethodInfo m && m.Name == "SetRelationValue") == 2 &&
            crimeCode.Any(i => i.LoadsConstant("RedMerchant")) && crimeCode.Any(i => i.LoadsConstant("CrimeCount")),
            "The crime check must contain the party debuff and merchant hostility consequences together.");

        var deathCode = Code(death);
        int crimeCall = deathCode.FindIndex(i => Equals(i.operand, crime));
        Require(crimeCall > 0 && deathCode.Take(crimeCall).Any(i => i.operand is MethodInfo m && m.Name == "OnLostTarget") &&
            deathCode.Skip(crimeCall + 1).Any(i => i.operand is MethodInfo m && m.Name == "StopBattle"),
            "Skipping the whole NPC death callback would lose native target and battle cleanup.");
        var apply = Code(RequiredMethod(unit, "ApplyDamage", damage));
        int damagedEvent = apply.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnDamagedServerside" && f.DeclaringType == unit);
        int dieCall = apply.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == unit && m.Name == "Die");
        Require(damagedEvent >= 0 && dieCall > damagedEvent,
            "Instance hostility must be handled before a lethal hit invokes the native death check.");
        Require(Code(damaged).Any(i => i.operand is MethodInfo m && m.Name == "SetTempEnemyRelation"),
            "Re-audit native retaliation: the shared faction-hostility write changed.");
        var awake = Code(RequiredMethod(npc, "Awake"));
        Require(awake.Any(i => Equals(i.operand, damaged)) && awake.Any(i => Equals(i.operand, death)) &&
            awake.Any(i => i.operand is MethodInfo m && m.Name == "add_OnDamagedServerside") &&
            awake.Any(i => i.operand is MethodInfo m && m.Name == "add_OnDie"), "Native NPC callback subscriptions changed.");
    }

    private static void VerifyShopIsolation(Assembly game)
    {
        Type Type(string name) => game.GetType(name, true)!;
        var npc = Type("UnitAI_NewBasic");
        var safe = Type("Safe");
        var setSocial = RequiredMethod(npc, "SetSocialID", typeof(string), typeof(string), Type("EPersonality"),
            Type("EFactionAlignment"), typeof(string), Type("EProceduralMerchantType"), typeof(int), Type("ItemMetadata").MakeArrayType());
        var social = Code(setSocial);
        int findSafe = social.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == safe && m.Name == "Find");
        int spawnSafe = social.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "Mirror.NetworkServer" && m.Name == "Spawn");
        int connectSafe = social.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == safe && m.Name == "set_NetworkconnectedMerchant");
        int fillSafe = social.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == safe && m.Name == "GenerateItemInInventory");
        Require(findSafe >= 0 && spawnSafe > findSafe && connectSafe > spawnSafe && fillSafe > connectSafe &&
            social.Any(i => i.LoadsConstant("Mat_TravelerMerchant")) &&
            social.Any(i => i.operand is MethodInfo m && m.DeclaringType == Type("UnitAvatar") && m.Name == "get_RandomID"),
            "Native merchant stock must use the instance RNG and connected, network-spawned safe.");
        var unity = Assembly.Load("UnityEngine.CoreModule");
        var find = Code(RequiredMethod(safe, "Find", unity.GetType("UnityEngine.Vector3", true)!));
        Require(find.Any(i => i.LoadsConstant(10f)) && !find.Any(i =>
                i.operand is FieldInfo f && f.Name == "connectedMerchant" ||
                i.operand is MethodInfo m && m.Name == "get_NetworkconnectedMerchant"),
            "Safe.Find ownership/radius changed; re-audit the no-nearby-safe spawn precondition.");
        Require(Code(RequiredMethod(safe, "GenerateItemInInventory", Type("ItemMetadata").MakeArrayType()))
                .Any(i => i.operand is MethodInfo m && m.Name == "AddItems"), "Native safe stock generation changed.");
        var selling = Code(AccessTools.PropertyGetter(npc, "CurrentSelling")!);
        Require(selling.Any(i => i.operand is MethodInfo m && m.Name == "get_NetworkMySafe") &&
            selling.Any(i => i.operand is FieldInfo f && f.Name == "Inventory" && f.DeclaringType == safe),
            "Merchant trading must use the independently owned safe inventory.");

        var trade = RequiredMethod(safe, "Trade", unity.GetType("UnityEngine.GameObject", true)!);
        var tradeCode = Code(trade);
        Require(tradeCode.Any(i => i.operand is FieldInfo f && f.DeclaringType == Type("UnitAvatar") && f.Name == "IsDead") &&
            tradeCode.Any(i => i.operand is MethodInfo m && m.DeclaringType == Type("UI_InventoryViewer") && m.Name == "Open"),
            "Native dead-merchant stock must remain lootable through the safe.");
        var table = Code(RequiredMethod(Type("StallTable"), "Interaction", unity.GetType("UnityEngine.GameObject", true)!));
        int battleCheck = table.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == Type("UnitAvatar") && m.Name == "get_IsInBattle");
        Require(battleCheck >= 0 && table.FindIndex(i => Equals(i.operand, trade)) > battleCheck &&
            table.Skip(battleCheck + 1).Any(i => i.opcode.FlowControl == FlowControl.Cond_Branch),
            "The native stall must retain its battle-state guard before allowing stock interaction.");
        VerifyTradeFallback(game, trade);
        var tradeCallback = Type("FunctionNode_Trade").GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Single(m => Code(m).Any(i => i.operand is MethodInfo called && called.DeclaringType == Type("SocialIDDatabase") && called.Name == "FindByName"));
        VerifyTradeFallback(game, tradeCallback);
        var talk = Code(RequiredMethod(npc, "Talk", Type("UnitAvatar")));
        Require(talk.Any(i => i.operand is FieldInfo f && f.Name == "roleName") &&
            talk.Any(i => i.operand is MethodInfo m && m.Name == "FindRoleByName") &&
            !talk.Any(i => i.operand is MethodInfo m && m.DeclaringType == Type("SocialIDDatabase") && m.Name == "FindByName"),
            "A unique addon social ID must retain native dialogue lookup by role name.");
        Require(Code(AccessTools.PropertySetter(npc, "CanTalk")!).Any(i =>
                i.operand is MethodInfo m && m.Name == "set_NetworkcanTalkCount") &&
            Code(RequiredMethod(Type("Interactable_NPC"), "IsInteractable", unity.GetType("UnityEngine.GameObject", true)!))
                .Any(i => i.operand is FieldInfo f && f.DeclaringType == npc && f.Name == "roleName"),
            "Hostile encounters require native synchronized conversation disabling and the empty-role guard.");
    }

    private static void VerifyTradeFallback(Assembly game, MethodInfo method)
    {
        var code = Code(method);
        int lookup = code.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == game.GetType("SocialIDDatabase") && m.Name == "FindByName");
        int field = code.FindIndex(lookup + 1, i => i.operand is FieldInfo f && f.DeclaringType == game.GetType("SocialIDEntity") && f.Name == "tradeType");
        int guard = code.FindIndex(lookup + 1, i => i.opcode == OpCodes.Brfalse || i.opcode == OpCodes.Brfalse_S);
        int fallback = Convert.ToInt32(game.GetType("ETradeType", true)!.GetField("BuyAndSell")!.GetRawConstantValue());
        Require(lookup >= 0 && field > lookup && guard > lookup && guard < field &&
            code.Take(lookup).TakeLast(8).Any(i => i.LoadsConstant(fallback)) &&
            code[guard].operand is Label skip && code.Skip(field + 1).Any(i => i.labels.Contains(skip)),
            "Unknown addon social IDs must safely use BuyAndSell in " + method.DeclaringType!.Name + "." + method.Name);
    }

    private static void VerifyReplication(Assembly game)
    {
        foreach (var pair in new[] { ("UnitAI_NewBasic", "socialID"), ("UnitAI_NewBasic", "roleName"),
            ("UnitAI_NewBasic", "MySafe"), ("UnitAI_NewBasic", "canTalkCount"), ("Safe", "connectedMerchant"), ("UnitAvatar", "faction"),
            ("UnitAvatar", "defaultNameKey"), ("UnitAvatar", "randomID"), ("UnitAvatar", "isInBattle"),
            ("UnitAvatar", "maxHp"), ("UnitAvatar", "finalMaxHp"), ("UnitAvatar", "hp") })
        {
            var type = game.GetType(pair.Item1, true)!;
            var field = AccessTools.DeclaredField(type, pair.Item2)!;
            var setter = AccessTools.PropertySetter(type, "Network" + pair.Item2);
            Require(field != null && setter != null && field.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "Mirror.SyncVarAttribute") &&
                Code(setter).Any(i => i.operand is MethodInfo m && m.Name.StartsWith("GeneratedSyncVarSetter", StringComparison.Ordinal)),
                "Unmodified guests require the existing native SyncVar contract: " + pair.Item1 + "." + pair.Item2);
            Require(Code(RequiredMethod(type, "DeserializeSyncVars", Assembly.Load("Mirror").GetType("Mirror.NetworkReader", true)!, typeof(bool)))
                    .Any(i => Equals(i.operand, field)),
                "Guest receiver no longer reads native merchant field " + pair.Item1 + "." + pair.Item2);
            var getter = AccessTools.PropertyGetter(type, "Network" + pair.Item2);
            Require(Code(RequiredMethod(type, "SerializeSyncVars", Assembly.Load("Mirror").GetType("Mirror.NetworkWriter", true)!, typeof(bool)))
                    .Any(i => Equals(i.operand, field) || getter != null && Equals(i.operand, getter)),
                "Host serializer no longer publishes native merchant field " + pair.Item1 + "." + pair.Item2);
        }
        var changeFaction = Code(RequiredMethod(game.GetType("UnitAvatar", true)!, "ChangeFaction", typeof(string)));
        int remove = changeFaction.FindIndex(i => i.operand is MethodInfo m && m.Name == "RemoveCreatureFromFaction");
        int publish = changeFaction.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_Networkfaction");
        int register = changeFaction.FindIndex(i => i.operand is MethodInfo m && m.Name == "RegisterCreatureToFaction");
        Require(remove >= 0 && publish > remove && register > publish,
            "Instance-only retaliation requires native synchronized faction membership updates.");
    }

    private static void VerifyPrefix(Type hooks, string name, Type[] args, string[] names)
    {
        var method = RequiredMethod(hooks, name, args);
        Require(method.IsStatic && method.ReturnType == typeof(bool) && method.GetParameters().Select(p => p.Name).SequenceEqual(names),
            "Merchant Harmony prefix must retain its narrow native argument binding: " + name);
    }

    private static MethodInfo RequiredMethod(Type type, string name, params Type[] args) =>
        AccessTools.DeclaredMethod(type, name, args) ?? throw new Exception(type.Name + "." + name + " missing.");
    private static List<CodeInstruction> Code(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method).ToList();
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
