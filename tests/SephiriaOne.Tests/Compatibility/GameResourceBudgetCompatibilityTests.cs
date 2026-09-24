using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameResourceBudgetCompatibilityTests
{
    // Read and rewrite installed IL without patching or executing game methods.
    public static void Run(Assembly game, Assembly addon)
    {
        Type inventory = game.GetType("GridInventory", true)!;
        Type player = game.GetType("PlayerAvatar", true)!;
        Type unit = game.GetType("UnitAvatar", true)!;
        FieldInfo capacity = AccessTools.Field(inventory, "CurrentInventoryStorage");
        if (capacity?.FieldType != typeof(short) || AccessTools.Field(inventory, "Width")?.FieldType != typeof(byte))
            throw new Exception("Native inventory storage/width types changed.");
        var nativeFruit = AccessTools.DeclaredMethod(unit, "GetCustomStatUnsafe", new[] { typeof(string) });
        VerifyRewrite(game, addon, "PlayerSpawner", "Initialize", "InventoryResourceHooks", "GuardCapacityRead", "ReadCapacity",
            instruction => instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, capacity));
        VerifyRewrite(game, addon, "DungeonManager", "LoadStageAndMove", "ResourceBudgetHooks", "GuardFruitRead", "ReadFruitGrantStat",
            instruction => instruction.Calls(nativeFruit), "FRUITCOUNT");

        var localResize = AccessTools.DeclaredMethod(inventory, "LocalAddStorage", new[] { typeof(short) });
        var resizeInstructions = PatchProcessor.GetOriginalInstructions(localResize).ToList();
        foreach (string call in new[] { "set_NetworkCurrentInventoryStorage", "RpcChangeInventoryStorage", "RpcChangeInventoryHeight" })
            if (!resizeInstructions.Any(i => i.operand is MethodInfo method && method.Name == call))
                throw new Exception("Native slot resize notification changed: " + call);

        MethodInfo talentSetter = AccessTools.PropertySetter(player, "NetworkmaxPassivePoint");
        if (talentSetter == null || !PatchProcessor.GetOriginalInstructions(talentSetter).Any(i => i.operand is MethodInfo method && method.Name == "GeneratedSyncVarSetter"))
            throw new Exception("Talent budgets no longer use the native SyncVar setter.");
        Type savedTalent = player.GetNestedType("PassiveStatSaveData", BindingFlags.Public)!;
        MethodInfo load = AccessTools.DeclaredMethod(player, "LoadPassiveStatOnServer", new[] { savedTalent.MakeArrayType() });
        if (load == null || !load.IsPublic || load.ReturnType != typeof(void) ||
            savedTalent.GetField("id")?.FieldType != typeof(ulong) || savedTalent.GetField("point")?.FieldType != typeof(int))
            throw new Exception("The native saved-talent boundary changed.");
        Console.WriteLine("Verified installed-game talent, fruit, inventory capacity and notification contracts.");
    }

    private static void VerifyRewrite(Assembly game, Assembly addon, string nativeType, string nativeMethod,
        string hookType, string rewriteName, string adapterName, Func<CodeInstruction, bool> candidate, string? precedingLiteral = null)
    {
        MethodInfo native = AccessTools.DeclaredMethod(game.GetType(nativeType, true), nativeMethod);
        Type hooks = addon.GetType("SephiriaOne." + hookType, true)!;
        MethodInfo rewrite = AccessTools.DeclaredMethod(hooks, rewriteName);
        MethodInfo adapter = AccessTools.DeclaredMethod(hooks, adapterName);
        List<CodeInstruction> Read() => PatchProcessor.GetOriginalInstructions(native).ToList();
        int Match(List<CodeInstruction> code) => code.FindIndex(i => candidate(i) && (precedingLiteral == null ||
            code.IndexOf(i) > 0 && code[code.IndexOf(i) - 1].opcode == OpCodes.Ldstr && Equals(code[code.IndexOf(i) - 1].operand, precedingLiteral)));
        List<CodeInstruction> Rewrite(List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();

        var original = Read();
        int index = Match(original);
        if (index < 0) throw new Exception("Installed native resource consumer disappeared: " + nativeType + "." + nativeMethod);
        int labels = original[index].labels.Count;
        int blocks = original[index].blocks.Count;
        var rewritten = Rewrite(original);
        var replacements = rewritten.Where(i => i.Calls(adapter)).ToList();
        if (replacements.Count != 1 || replacements[0].labels.Count != labels || replacements[0].blocks.Count != blocks)
            throw new Exception("Resource replacement/branch metadata mismatch: " + adapterName);

        foreach (bool duplicate in new[] { false, true })
        {
            var changed = Read();
            int match = Match(changed);
            if (duplicate)
            {
                if (precedingLiteral != null) changed.Add(new CodeInstruction(OpCodes.Ldstr, precedingLiteral));
                changed.Add(new CodeInstruction(changed[match].opcode, changed[match].operand));
            }
            else changed.RemoveAt(match);
            bool rejected = false;
            try { _ = Rewrite(changed); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Changed resource boundary accepted: " + adapterName + (duplicate ? " (duplicate)" : " (missing)"));
        }
        Console.WriteLine("Verified installed-game resource consumer: " + nativeType + "." + nativeMethod);
    }
}
