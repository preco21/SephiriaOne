using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameDefensiveEffectTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string owner, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(owner), method)).ToList();
        var hooks = addon.GetType("SephiriaOne.FriendlyFireDefensiveEffectHooks", true)!;
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var avatar = Type("UnitAvatar");
        var controller = Type("WeaponControllerSimple");
        var counter = AccessTools.DeclaredField(avatar, "isCounterInvincibleApplied");
        var callback = AccessTools.DeclaredField(avatar, "OnCounter");
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage"), out var generator).ToList();
        var rewrite = AccessTools.DeclaredMethod(hooks, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();

        Require(counter is { FieldType: not null } && counter.FieldType == typeof(sbyte),
            "native counter admission reads the signed byte counter");
        foreach (var field in new[]
        {
            ("PlayerAvatar", "weaponController", controller),
            ("WeaponControllerSimple", "furyInvincibleTimer", typeof(float)),
            ("WeaponControllerSimple", "isFuryInvincibleApplied", typeof(bool))
        })
        {
            var actual = AccessTools.DeclaredField(Type(field.Item1), field.Item2);
            Require(actual is { IsPrivate: true, IsStatic: false } && actual.FieldType == field.Item3,
                "read-only native access retains the exact " + field.Item1 + "." + field.Item2 + " field contract");
        }
        foreach (var signature in new[]
        {
            new[] { Type("PlayerAvatar"), controller },
            new[] { controller, typeof(float) },
            new[] { controller, typeof(bool) }
        })
        {
            var accessors = hooks.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(AccessTools.FieldRef<,>) &&
                    f.FieldType.GetGenericArguments().SequenceEqual(signature)).ToArray();
            Require(accessors.Length == 1 && accessors[0].IsInitOnly,
                "private native controller state uses a single cached readonly field accessor");
        }
        var counterAdapter = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(runtime, "CounterAllowed")).ToList();
        Require(!counterAdapter.Any(i => i.opcode.Name?.StartsWith("stind", StringComparison.Ordinal) == true ||
            i.opcode == OpCodes.Stobj || i.opcode == OpCodes.Stfld),
            "counter admission never writes through native field references or mutates controller timers");

        int read = native.FindIndex(i => i.LoadsField(counter));
        int onCounter = native.FindIndex(i => i.LoadsField(callback));
        int parry = native.FindIndex(i => Field(i, OpCodes.Ldfld, "UnitAvatar", "parryInvincibleApplied"));
        Require(read > 0 && native[read - 1].opcode == OpCodes.Ldarg_0 && native[read + 1].LoadsConstant(0) &&
            (native[read + 2].opcode == OpCodes.Ble || native[read + 2].opcode == OpCodes.Ble_S) &&
            read < onCounter && onCounter < parry,
            "positive counter admission precedes its own callback and the distinct paid parry branch");

        void CheckRewrite(List<CodeInstruction> original)
        {
            var patched = Rewrite(original);
            int insertion = read + 1;
            Require(patched.Count == original.Count + 1 && patched.Count(i => Call(i, "FriendlyFireRuntime", "CounterAllowed")) == 1 &&
                Call(patched[insertion], "FriendlyFireRuntime", "CounterAllowed") && patched[insertion + 1].LoadsConstant(0),
                "only one scalar adapter is inserted after the counter read and before its positive-count comparison");
            var adapter = (MethodInfo)patched[insertion].operand;
            Require(adapter.IsStatic && adapter.ReturnType == typeof(int) &&
                adapter.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(int) }),
                "counter adapter preserves the native evaluation stack");
            for (int n = 0; n < original.Count; n++)
            {
                int mappedIndex = n < insertion ? n : n + 1;
                var mapped = patched[mappedIndex];
                Require(mapped.opcode == original[n].opcode && Equals(mapped.operand, original[n].operand),
                    "every native callback, evasion, protection, shield, damage and death instruction remains intact");
                var entry = n == insertion ? patched[insertion] : mapped;
                Require(entry.labels.SequenceEqual(original[n].labels) && entry.blocks.SequenceEqual(original[n].blocks),
                    "branch and exception entry metadata reaches the entire adapted operation");
                if (n == insertion)
                    Require(mapped.labels.Count == 0 && mapped.blocks.Count == 0,
                        "entry metadata is transferred once rather than duplicated or allowed to bypass the adapter");
            }
        }
        CheckRewrite(native);
        var labeled = native.Select(i => new CodeInstruction(i)).ToList();
        labeled[read + 1].labels.Add(generator.DefineLabel());
        labeled[read + 1].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        CheckRewrite(labeled);
        Require(labeled[read + 1].labels.Count == native[read + 1].labels.Count + 1 &&
            labeled[read + 1].blocks.Count == native[read + 1].blocks.Count + 1,
            "rewriting never mutates the caller's instruction metadata");

        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        void RejectAt(int index, CodeInstruction replacement, string why)
        {
            var changed = native.Select(i => new CodeInstruction(i)).ToList();
            changed[index] = replacement;
            Reject(changed, why);
        }
        Reject(native.Where(i => !i.LoadsField(counter)), "missing counter admission requires a fresh audit");
        Reject(native.Concat(new[] { native[read] }), "duplicate counter reads cannot be patched ambiguously");
        Reject(native.Where(i => !i.LoadsField(callback)), "missing OnCounter boundary cannot be mistaken for ordinary evasion");
        Reject(native.Concat(new[] { native[onCounter] }), "ambiguous OnCounter boundaries require a fresh audit");
        RejectAt(read - 1, new(OpCodes.Ldarg_1), "changed counter owner rejects installation");
        RejectAt(read + 1, new(OpCodes.Ldc_I4_1), "changed counter threshold rejects installation");
        RejectAt(read + 2, new(OpCodes.Bge, native[read + 2].operand), "reversed counter comparison rejects installation");

        NativeCounterTiming(game, Code);
        NativeEvadeRestore(game, hooks, runtime, Code, native);
        Console.WriteLine("Verified native counter-only admission, timer/producer contracts, exact evasion-refund callback, unchanged native IL/metadata and changed-IL rejection (not live Unity or multiplayer).");
    }

    private static void NativeCounterTiming(Assembly game, Func<string, string, List<CodeInstruction>> code)
    {
        int counterKind = Convert.ToInt32(Enum.Parse(game.GetType("EDodgeInvincibleType", true)!, "Counter"));
        var start = code("WeaponControllerSimple", "StartFuryInvincible");
        int activeRead = start.FindIndex(i => Field(i, OpCodes.Ldfld, "WeaponControllerSimple", "isFuryInvincibleApplied"));
        int activation = start.FindIndex(i => Call(i, "UnitAvatar", "StartDodgeInvincible"));
        int reset = start.FindLastIndex(i => Field(i, OpCodes.Stfld, "WeaponControllerSimple", "furyInvincibleTimer"));
        Require(start.Any(i => Text(i, "FURYINVINCIBLETIME")) && start.Any(i => Text(i, "FURYINVINCIBLETIMEPERCENT")) &&
            start.Any(i => i.LoadsConstant(10f)) && start.Any(i => i.LoadsConstant(100f)) &&
            start.Any(i => Field(i, OpCodes.Stfld, "WeaponControllerSimple", "furyInvincibleTime")),
            "native offensive counter duration retains additive tenths and percentage amplification");
        Require(activeRead >= 0 && activation > activeRead && reset > activation && start[activation - 1].LoadsConstant(counterKind),
            "counter activation has a separate native lifetime from ordinary dodge and parry");
        var alreadyActive = start[activeRead + 1];
        Require((alreadyActive.opcode == OpCodes.Brtrue || alreadyActive.opcode == OpCodes.Brtrue_S) && alreadyActive.operand is Label,
            "already-active counter windows skip duplicate activation");
        int repeatedEntry = start.FindIndex(i => i.labels.Contains((Label)alreadyActive.operand));
        Require(repeatedEntry > activation && repeatedEntry < reset &&
            start.Skip(repeatedEntry).Take(reset - repeatedEntry).Any(i => Call(i, "HorayNetworkManager", "CalculateLatency")),
            "every repeated action still resets the native timer with its latency offset");

        var update = code("WeaponControllerSimple", "Update");
        int timerRead = update.FindIndex(i => Field(i, OpCodes.Ldfld, "WeaponControllerSimple", "furyInvincibleTimer"));
        int timerWrite = update.FindIndex(i => Field(i, OpCodes.Stfld, "WeaponControllerSimple", "furyInvincibleTimer"));
        int expiration = update.FindIndex(i => Field(i, OpCodes.Ldfld, "WeaponControllerSimple", "furyInvincibleTime"));
        int clear = update.FindIndex(i => Field(i, OpCodes.Stfld, "WeaponControllerSimple", "isFuryInvincibleApplied"));
        Require(timerRead >= 0 && timerRead < timerWrite && timerWrite < expiration && expiration < clear &&
            update.Skip(timerRead).Take(timerWrite - timerRead).Any(i => Call(i, "Time", "get_deltaTime")) &&
            update[clear - 1].LoadsConstant(0), "native Update continues aging and clearing the actual counter window");
        Require(update.Skip(clear + 1).Take(8).Any(i => Call(i, "UnitAvatar", "StopDodgeInvincible")),
            "native expiry still releases its counter contribution");
        foreach (var producer in new[] { ("WeaponSimple_Katana", "OnServerReceivedMessage"), ("WeaponSimple_Katana", "StartSpecialAttackAnimation"), ("WeaponSimple_Dagger", "HandleBeginAnimation") })
            Require(code(producer.Item1, producer.Item2).Any(i => Call(i, "WeaponControllerSimple", "StartFuryInvincible")),
                producer.Item1 + "." + producer.Item2 + " still converges on the audited counter timer");
        Require(!code("WeaponSimple_Katana", "OnServerReceivedMessage").Any(i => Call(i, "UnitAvatar", "UseMp")),
            "free sheath counter actions remain distinct from paid parries and special attacks");
        Require(code("Charm_GrowthKatana", "OnEnabledEffect").Any(i => Text(i, "FURYINVINCIBLETIMEPERCENT")) &&
            code("Charm_GrowthKatana", "OnDisabledEffect").Any(i => Text(i, "FURYINVINCIBLETIMEPERCENT")),
            "growth artifact duration contributions retain their native activation and removal");
    }

    private static void NativeEvadeRestore(Assembly game, Type hooks, Type runtime,
        Func<string, string, List<CodeInstruction>> code, List<CodeInstruction> apply)
    {
        var charm = game.GetType("Charm_EvasionRestore", true)!;
        var damage = game.GetType("DamageInstance", true)!;
        var native = code("Charm_EvasionRestore", "HandleEvade");
        var validate = AccessTools.DeclaredMethod(hooks, "ValidateEvadeRestore");
        bool Valid(IEnumerable<CodeInstruction> input) => (bool)validate.Invoke(null, new object[] { input })!;
        Require(Valid(native), "exact native evasion-refund producer is accepted");
        int dash = native.FindIndex(i => Field(i, OpCodes.Ldfld, "UnitAvatar", "CurrentDashModule"));
        int levels = native.FindIndex(i => Field(i, OpCodes.Ldfld, "Charm_EvasionRestore", "restoreDashCountByLevel"));
        int level = native.FindIndex(i => Call(i, "Charm_Basic", "CurrentLevelToIdx"));
        int amount = native.FindIndex(i => Call(i, "ArrayExtensions", "SafeRandomAccess"));
        int restore = native.FindIndex(i => Call(i, "CharacterDash", "RestoreDashCount"));
        Require(native.Count(i => Call(i, "CharacterDash", "RestoreDashCount")) == 1 &&
            dash >= 0 && dash < levels && levels < level && level < amount && amount < restore &&
            !native.Any(i => Call(i, "Timer", "Update") || Call(i, "Timer", "Check")),
            "the artifact makes one unconditional level-based dash refund without a native cooldown");
        foreach (int index in new[] { dash, levels, level, amount, restore })
        {
            Require(!Valid(native.Where((_, i) => i != index)), "missing refund source instruction requires a fresh audit");
            Require(!Valid(native.Concat(new[] { native[index] })), "duplicate refund source instruction rejects ambiguity");
        }
        var reordered = native.Select(i => new CodeInstruction(i)).ToList();
        (reordered[dash], reordered[levels]) = (reordered[levels], reordered[dash]);
        Require(!Valid(reordered), "changed owner/level ordering rejects the callback hook");
        Require(code("Charm_EvasionRestore", "OnEnabledEffect").Any(i => Call(i, "UnitAvatar", "add_OnEvade")) &&
            code("Charm_EvasionRestore", "OnDisabledEffect").Any(i => Call(i, "UnitAvatar", "remove_OnEvade")) &&
            code("Charm_Basic", "Disconnect").Any(i => Call(i, "Charm_Basic", "DisableEffect")),
            "native artifact activation, removal and disconnect still own callback subscription");
        var prefix = AccessTools.DeclaredMethod(runtime, "AllowEvadeRestore", new[] { charm, damage });
        Require(prefix is { IsStatic: true } && prefix.ReturnType == typeof(bool),
            "the prefix can gate only this exact artifact callback and incoming damage instance");
        var install = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(hooks, "Install")).ToList();
        Require(install.Any(i => Text(i, "HandleEvade")) && install.Any(i => Text(i, "AllowEvadeRestore")) &&
            install.Any(i => i.opcode == OpCodes.Ldtoken && Equals(i.operand, charm)) && install.Any(i => Call(i, "Harmony", "Patch")),
            "installation targets the artifact callback rather than the shared OnEvade event or dash refund transport");
        int dashEvasion = apply.FindIndex(i => Text(i, "DASHEVASION"));
        int evadeFromDash = apply.FindIndex(dashEvasion + 1, i => Field(i, OpCodes.Ldfld, "UnitAvatar", "OnEvade"));
        int barrierFlag = apply.FindIndex(i => Text(i, "USE_ABSOLUTEEVASION_STAT"));
        int evadeFromRoll = apply.FindIndex(barrierFlag + 1, i => Field(i, OpCodes.Ldfld, "UnitAvatar", "OnEvade"));
        Require(dashEvasion >= 0 && dashEvasion < evadeFromDash && evadeFromDash < barrierFlag && barrierFlag < evadeFromRoll &&
            apply.Count(i => Field(i, OpCodes.Ldfld, "UnitAvatar", "OnEvade")) == 2,
            "dash invulnerability and later ordinary/absolute evasion invoke distinct native callbacks");
        Require(code("Charm_BrokenSapphire", "OnEvade").Any(i => Text(i, "USE_ABSOLUTEEVASION_STAT")) &&
            code("ActiveSkill_Smokescreen", "TargetUnit_OnEvade").Any(i => Call(i, "UnitAvatar", "ForcedEndBuff")),
            "other OnEvade subscribers still consume finite barriers and smoke charges");
        Require(code("CharacterDash", "RestoreDashCount").Any(i => Call(i, "CharacterDash", "TargetRestoreDashCount")) &&
            code("CharacterDash", "TargetRestoreDashCount").Any(i => Call(i, "NetworkBehaviour", "SendTargetRPCInternal")),
            "allowed refunds retain native stock-client dash charge transport");
    }

    private static bool Call(CodeInstruction instruction, string owner, string method) =>
        instruction.operand is MethodInfo info && info.DeclaringType?.Name == owner && info.Name == method;
    private static bool Field(CodeInstruction instruction, OpCode opcode, string owner, string name) =>
        instruction.opcode == opcode && instruction.operand is FieldInfo field && field.DeclaringType?.Name == owner && field.Name == name;
    private static bool Text(CodeInstruction instruction, string text) => instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, text);
    private static void Require(bool value, string why) { if (!value) throw new Exception("Defensive effect contract: " + why); }
}
