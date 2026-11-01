using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameMaxHpTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Check(bool value, string why) { if (!value) throw new Exception("PvP Max HP contract: " + why); }
        var hooks = addon.GetType("SephiriaOne.PvpMaxHpHooks");
        Check(hooks != null, "native mutation/save guards are present");
        bool Valid(string name, List<CodeInstruction> code, params object[] args) =>
            (bool)AccessTools.Method(hooks!, name).Invoke(null, new object[] { code }.Concat(args).ToArray())!;
        List<CodeInstruction> Copy(IEnumerable<CodeInstruction> code) => code.Select(i => new CodeInstruction(i)).ToList();
        var unit = game.GetType("UnitAvatar", true)!;
        var spawner = game.GetType("PlayerSpawner", true)!;
        var bone = game.GetType("WeaponAddonGreatsword_BoneBlood", true)!;
        var hp = AccessTools.Field(unit, "hp");
        var maximum = AccessTools.PropertyGetter(unit, "MaxHp");
        Check((bool)AccessTools.Method(hooks!, "ValidateNativeFields").Invoke(null, null)!, "native fields, SyncVars and guest HP UI are compatible");
        foreach (var getter in new[]
        {
            AccessTools.PropertyGetter(bone, "Avatar"),
            AccessTools.PropertyGetter(game.GetType("StatusInstance", true), "CurrentTarget")
        })
        {
            Check(getter != null && !getter.IsStatic && getter.ReturnType == unit && getter.GetParameters().Length == 0, "native target getter has the expected signature");
            var callbackType = typeof(Func<,>).MakeGenericType(getter!.DeclaringType!, unit);
            Check(Delegate.CreateDelegate(callbackType, getter) != null, "protected/private target getters support cached framework delegates");
        }

        foreach (bool percent in new[] { false, true })
        {
            var method = AccessTools.DeclaredMethod(unit, percent ? "AddMaxHpPercent" : "AddMaxHp", new[] { typeof(float), typeof(bool) });
            var code = PatchProcessor.GetOriginalInstructions(method).ToList();
            Check(Valid("ValidateAdd", code, percent), "current native maximum mutation contract");
            var setter = AccessTools.PropertySetter(unit, percent ? "NetworkfinalMaxHp" : "NetworkmaxHp");
            int at = code.FindIndex(i => i.Calls(setter));
            var changed = Copy(code); changed[at - 1] = new CodeInstruction(OpCodes.Sub);
            Check(!Valid("ValidateAdd", changed, percent), "changed maximum arithmetic fails closed");
            changed = Copy(code); changed.Add(new CodeInstruction(code[at]));
            Check(!Valid("ValidateAdd", changed, percent), "duplicate maximum writer fails closed");
            changed = Copy(code); changed.RemoveAt(code.FindIndex(i => i.Calls(maximum)));
            Check(!Valid("ValidateAdd", changed, percent), "missing incoming HP ratio read fails closed");
        }

        var curse = game.GetType("StatusInstance_HPCurse", true)!;
        foreach (bool apply in new[] { false, true })
        {
            var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(curse, apply ? "ApplyStatusInner" : "RemoveStatusInner")).ToList();
            Check(Valid("ValidateCurse", code, apply), "current curse mutation contract");
            var setter = AccessTools.PropertySetter(unit, "NetworkcursedMaxHp");
            int at = code.FindIndex(i => i.Calls(setter));
            var changed = Copy(code); changed.RemoveAt(at);
            Check(!Valid("ValidateCurse", changed, apply), "missing curse maximum writer fails closed");
            changed = Copy(code); changed.Add(new CodeInstruction(code[at]));
            Check(!Valid("ValidateCurse", changed, apply), "duplicate curse writer fails closed");
        }
        foreach (string name in new[] { "Engage", "Disengage", "Update" })
        {
            var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(bone, name)).ToList();
            Check(Valid("ValidateBone", code, name), "current BoneBlood " + name + " contract");
            int at = code.FindIndex(i => i.Calls(AccessTools.PropertySetter(unit, "NetworkcursedMaxHp")));
            var changed = Copy(code); changed.RemoveAt(at);
            Check(!Valid("ValidateBone", changed, name), "missing BoneBlood " + name + " writer fails closed");
        }

        var save = AccessTools.DeclaredMethod(spawner, "SaveCurrentSessionData", Type.EmptyTypes);
        var original = PatchProcessor.GetOriginalInstructions(save).ToList();
        Check(Valid("ValidateSave", original), "legacy and current native saves each have one HP read");
        var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks!, "RewriteSave").Invoke(null, new object[] { Copy(original) })!).ToList();
        Check(rewritten.Count == original.Count, "save rewrite keeps instruction layout");
        var changedPairs = original.Zip(rewritten).Where(p => p.First.opcode != p.Second.opcode || !Equals(p.First.operand, p.Second.operand)).ToArray();
        Check(changedPairs.Length == 2 && changedPairs.All(p => p.First.opcode == OpCodes.Ldfld && Equals(p.First.operand, hp)), "only the two saved HP reads change");
        Check(changedPairs.All(p => p.Second.opcode == OpCodes.Call && p.Second.operand is MethodInfo m &&
            m.Name == "NativeHpForSave" && m.IsStatic && m.ReturnType == typeof(float) && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { unit })), "save adapter preserves stack and native HP units");
        Check(original.Zip(rewritten).All(p => p.First.labels.SequenceEqual(p.Second.labels) && p.First.blocks.SequenceEqual(p.Second.blocks)), "save rewrite preserves branch and exception metadata");
        int hpAt = original.FindIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, hp));
        var altered = Copy(original); altered.RemoveAt(hpAt);
        Check(!Valid("ValidateSave", altered), "missing save HP read fails closed");
        altered = Copy(original); altered.Add(new CodeInstruction(OpCodes.Ldfld, hp));
        Check(!Valid("ValidateSave", altered), "third save HP read fails closed");
        altered = Copy(original); altered[hpAt + 1] = new CodeInstruction(OpCodes.Nop);
        Check(!Valid("ValidateSave", altered), "a read no longer immediately saved as float fails closed");

        var initAdapter = addon.GetType("SephiriaOne.NativePlayerInitialization", true)!;
        var initialize = (MethodInfo)AccessTools.Method(initAdapter, "ResolveInitialize").Invoke(null, new object[] { spawner })!;
        Check(initialize.ReturnType == typeof(bool) && !initialize.IsStatic, "shared initialization boundary is supported");
        var initCode = PatchProcessor.GetOriginalInstructions(initialize).ToList();
        Check(initCode.Any(i => i.Calls(AccessTools.DeclaredMethod(unit, "SetHp", new[] { typeof(float) }))) &&
            !initCode.Any(i => i.Calls(AccessTools.PropertySetter(unit, "NetworkmaxHp"))), "initialization restores HP after native stat sources");
        foreach (string name in new[] { "MaxHP", "FinalHP" })
        {
            var status = game.GetType("StatusInstance_" + name, true)!;
            var mutation = AccessTools.DeclaredMethod(unit, name == "MaxHP" ? "AddMaxHp" : "AddMaxHpPercent", new[] { typeof(float), typeof(bool) });
            foreach (string action in new[] { "ApplyStatusInner", "RemoveStatusInner" })
                Check(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(status, action)).Count(i => i.Calls(mutation)) == 1, "equipment/status changes converge at scoped mutation");
        }
        var revive = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(unit, "Revive", new[] { typeof(float) })).ToList();
        Check(revive.Any(i => i.Calls(AccessTools.PropertySetter(unit, "Networkhp"))) && revive.Any(i => i.operand is MethodInfo m && m.Name == "TakeRemoteInventory"), "native revival owns HP and inventory recovery");
        foreach (string finalizer in new[] { "AfterNative", "AfterInitialize" })
        {
            var code = PatchProcessor.GetOriginalInstructions(AccessTools.Method(hooks!, finalizer)).ToList();
            Check(!code.Any(i => i.operand is MethodInfo m && m.Name == "op_Inequality" && m.DeclaringType?.FullName == "UnityEngine.Object"),
                "a destroyed Unity avatar still leaves its native scope in " + finalizer);
        }
        Console.WriteLine("Verified PvP Max HP native mutation/curse/BoneBlood scopes, two save adapters, SyncVars, guest UI and initialization contracts; malformed contracts fail closed.");
    }
}
