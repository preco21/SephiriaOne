using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameCriticalDamageTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var damage = game.GetType("DamageInstance", true)!;
        var apply = AccessTools.DeclaredMethod(avatar, "ApplyDamage");
        var hook = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireCriticalDamageHooks", true)!, "Rewrite");
        var native = PatchProcessor.GetOriginalInstructions(apply, out var generator).ToList();
        var rate = AccessTools.DeclaredField(damage, "criticalDamageRate");
        var multiplier = AccessTools.DeclaredField(damage, "criticalDamageMultiplier");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)hook.Invoke(null, new object[] { code, generator })!).ToList();
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        void Check(bool pass, string why) { if (!pass) throw new Exception("Critical damage contract: " + why); }
        var patched = Rewrite(native);
        int gate = native.FindIndex(i => i.LoadsField(multiplier)) - 1;
        int insert = patched.FindIndex(i => Call(i, "FriendlyFireRuntime", "LimitCriticalDamage"));
        Check(patched.Count == native.Count + 7 && insert == gate + 2 &&
            patched.Count(i => Call(i, "FriendlyFireRuntime", "LimitCriticalDamage")) == 1,
            "one limiter after companion aggregation, before special multiplier");
        Check(patched[insert + 1].IsStloc() && patched[insert + 1].operand is LocalBuilder { LocalType: var type } && type == typeof(float),
            "fractional curve is kept in a dedicated float local");
        int multiply = native.FindLastIndex(i => i.LoadsField(multiplier)), store = multiply + 3;
        int replacedLoads = 0;
        for (int n = 0; n < native.Count; n++)
        {
            int p = n + (n >= gate ? 4 : 0) + (n > store ? 3 : 0);
            if (native[n].opcode != patched[p].opcode || !Equals(native[n].operand, patched[p].operand))
            {
                Check(native[n].IsLdloc() && patched[p].IsLdloc() && Equals(patched[p].operand, patched[insert + 1].operand),
                    "only multiplier and two critical consumers change their source local");
                replacedLoads++;
            }
            int entry = n == gate ? gate : p;
            Check(native[n].labels.SequenceEqual(patched[entry].labels) && native[n].blocks.SequenceEqual(patched[entry].blocks),
                "native branches/exception boundaries are preserved");
        }
        Check(replacedLoads == 3 && patched.Count(i => i.LoadsField(rate)) == 1 &&
            !patched.Any(i => i.StoresField(rate)), "no stat/attack writes; native snapshot and side effects remain intact");
        Check(Call(patched[store + 3], "Mathf", "RoundToInt"), "native special-multiplier rounding is retained");
        void Reject(List<CodeInstruction> bad, string why)
        {
            bool rejected = false;
            try { Rewrite(bad); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, why);
        }
        void RejectAt(int index, CodeInstruction replacement, string why)
        { var bad = native.Select(i => new CodeInstruction(i)).ToList(); bad[index] = replacement; Reject(bad, why); }
        RejectAt(native.FindIndex(i => i.LoadsField(rate)), new(OpCodes.Nop), "missing critical snapshot rejected");
        RejectAt(gate + 2, new(OpCodes.Ldc_R4, 2f), "changed native multiplier identity rejected");
        RejectAt(multiply + 1, new(OpCodes.Add), "changed multiplier arithmetic rejected");
        RejectAt(multiply + 2, new(OpCodes.Nop), "changed integer rounding rejected");
        Reject(native.Concat(native).ToList(), "ambiguous native boundary rejected");
        Reject(patched, "duplicate patch rejected");
        var clone = native.Select(i => new CodeInstruction(i)).ToList();
        var label = generator.DefineLabel(); clone[gate].labels.Add(label);
        clone[gate].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        var annotated = Rewrite(clone);
        Check(annotated[gate].labels.Contains(label) && annotated[gate].blocks.Count == 1 && annotated[gate + 4].labels.Count == 0,
            "branch/exception entry executes initialization before native multiplier");

        var pipeline = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireTranspiler", true)!, "Rewrite");
        var combined = ((IEnumerable<CodeInstruction>)pipeline.Invoke(null, new object[] { native, generator })!).ToList();
        int amp = combined.FindIndex(i => Call(i, "FriendlyFireRuntime", "LimitAmplification"));
        int critical = combined.FindIndex(i => Call(i, "FriendlyFireRuntime", "LimitCriticalDamage"));
        int scale = combined.FindIndex(i => Call(i, "FriendlyFireRuntime", "Scale"));
        Check(amp >= 0 && critical > amp && scale > critical, "critical limit composes before final scale/defenses/budget");

        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Panel(string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, method)).ToList();
        bool Text(IEnumerable<CodeInstruction> code, string text) => code.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, text));
        Check(Text(Panel("BuildPvpBalanceEditor"), "/one friendlyfire critical-damage") &&
            Text(Panel("BuildPvpBalanceEditor"), "Additional bonus; native +50% preserved."), "UI names the actual bonus units");
        Check(Text(Panel("ApplyFriendlyCriticalDamage"), "/one friendlyfire critical-damage limits ") &&
            Panel("ApplyFriendlyCriticalDamage").Count(i => Call(i, "TMP_InputField", "get_text")) == 2,
            "paired inputs apply atomically through shared command");
        Check(Panel("RefreshPvpBalance").Any(i => i.operand is FieldInfo f && f.Name == "friendlyCriticalDamageDraft") &&
            Panel("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyCriticalDamage")), "draft survives refresh and clears on scope change");
        Console.WriteLine("Verified critical snapshot/leader boundary, fractional curve, native multiplier/rounding, execution, preserved IL/labels, drift rejection and paired UI (not live Unity).");
    }
}
