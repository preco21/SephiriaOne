using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameWhiteBreadTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("White Bread contract: " + why); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        bool Field(CodeInstruction i, OpCode op, string name) => i.opcode == op && i.operand is FieldInfo f && f.DeclaringType == Type("UnitAvatar") && f.Name == name;
        var native = Code("UnitAvatar", "ApplyDamage");
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireWhiteBreadHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) => ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        int read = native.FindIndex(i => Field(i, OpCodes.Ldfld, "isHitInvincibleEnabled"));
        var patched = Rewrite(native);
        Require(read > 0 && patched.Count == native.Count + 8 &&
            patched.Count(i => Call(i, "PvpWhiteBread", "IsHitInvincible")) == 1,
            "only the one native hit-window admission read gets an adapter");
        for (int i = 0; i < native.Count; i++)
        {
            var mapped = patched[i <= read ? i : i + 8];
            Require(native[i].opcode == mapped.opcode && Equals(native[i].operand, mapped.operand), "all native instructions stay intact");
            var entry = patched[i <= read + 1 ? i : i + 8];
            Require(native[i].labels.SequenceEqual(entry.labels) && native[i].blocks.SequenceEqual(entry.blocks), "branch and exception entry labels stay correct");
        }
        Require(patched[read + 1].opcode == OpCodes.Ldarg_0 &&
            Field(patched[read + 3], OpCodes.Ldfld, "invincibleTimer") &&
            Field(patched[read + 5], OpCodes.Ldfld, "invincibleType") &&
            Field(patched[read + 7], OpCodes.Ldfld, "restoreFallingTimer") &&
            Call(patched[read + 8], "PvpWhiteBread", "IsHitInvincible"), "adapter receives exact victim and its native timer/fall fields");
        int dodge = native.FindIndex(i => i.operand is FieldInfo f && f.Name == "isDodgeInvincibleApplied");
        int before = native.FindIndex(i => i.operand is FieldInfo f && f.Name == "OnAttackUnitBeforeOperation");
        Require(before < read && read < dodge, "admission happens after callbacks and before unrelated dodge protection");
        foreach (var pair in new[] { ("OnEnabledEffect", 1, 1, 0), ("OnDisabledEffect", 1, 0, 1), ("OnUpdatedLevel", 2, 1, 1) })
        {
            var code = Code("Charm_HitInvincible", pair.Item1);
            Require(code.Count(i => Field(i, OpCodes.Stfld, "invincibleTime")) == pair.Item2 &&
                code.Count(i => i.opcode == OpCodes.Add) == pair.Item3 && code.Count(i => i.opcode == OpCodes.Sub) == pair.Item4 &&
                code.Any(i => i.operand is FieldInfo f && f.Name == "addTimeByLevel"),
                "native artifact contribution remains additive and owned by " + pair.Item1);
        }
        var update = Code("UnitAvatar", "Update");
        Require(update.Count(i => Field(i, OpCodes.Ldfld, "invincibleTime")) == 1 &&
            update.Any(i => Field(i, OpCodes.Ldfld, "invincibleTimer")) &&
            update.Any(i => Field(i, OpCodes.Stfld, "isHitInvincibleEnabled")), "native Update continues aging and clearing the original window");
        Require(Code("Charm_Basic", "Disconnect").Any(i => Call(i, "Charm_Basic", "DisableEffect")) &&
            Code("Charm_Basic", "DisableEffect").Any(i => Call(i, "GridInventory", "RemoveUniqueEffect")) &&
            Code("Charm_Basic", "EnableEffect").Any(i => Call(i, "GridInventory", "RegisterUniqueEffect")),
            "unique-effect lookup follows native activation/removal/disconnect");
        void Reject(IEnumerable<CodeInstruction> code, string why)
        {
            bool rejected = false;
            try { Rewrite(code); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        Reject(native.Where(i => !Field(i, OpCodes.Ldfld, "isHitInvincibleEnabled")), "missing native read rejects installation");
        Reject(native.Concat(new[] { native[read] }), "duplicate native reads reject ambiguity");
        var changed = native.Select(i => new CodeInstruction(i)).ToList();
        changed[read - 1].opcode = OpCodes.Ldarg_1;
        Reject(changed, "wrong read owner rejects installation");
        changed = native.Select(i => new CodeInstruction(i)).ToList();
        changed[read + 1].opcode = OpCodes.Brtrue;
        Reject(changed, "changed native branch semantics reject installation");
        Console.WriteLine("Verified White Bread native admission, unchanged timers/protection, additive artifact lifecycle and changed-IL rejection (not live Unity).");
    }
}
