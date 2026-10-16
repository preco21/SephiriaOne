using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameEvasionTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage")).ToList();
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireEvasionHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("Evasion contract: " + why); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        bool Text(CodeInstruction i, string text) => i.opcode == OpCodes.Ldstr && Equals(i.operand, text);
        var patched = Rewrite(native);
        int cap = patched.FindIndex(i => Call(i, "FriendlyFireRuntime", "EvasionChance"));
        Require(patched.Count == native.Count + 1 && patched.Count(i => Call(i, "FriendlyFireRuntime", "EvasionChance")) == 1 && cap > 0,
            "exactly one scalar chance adapter is added");
        int formula = native.FindIndex(i => Call(i, "Mathf", "Log"));
        int ignore = native.FindIndex(i => Text(i, "IGNOREEVASION"));
        Require(cap == formula + 4 && cap < ignore && native[cap].IsStloc(), "cap precedes native lower clamp and attacker anti-Evasion");
        for (int i = 0; i < native.Count; i++)
        {
            var mapped = patched[i < cap ? i : i + 1];
            Require(native[i].opcode == mapped.opcode && Equals(native[i].operand, mapped.operand), "every native instruction remains intact");
            var entry = patched[i <= cap ? i : i + 1];
            Require(native[i].labels.SequenceEqual(entry.labels) && native[i].blocks.SequenceEqual(entry.blocks), "branch/exception boundaries remain intact");
        }
        Require(native.Count(i => Call(i, "Random", "Range")) == patched.Count(i => Call(i, "Random", "Range")), "no extra or removed random rolls");
        int absolute = native.FindIndex(i => i.operand is FieldInfo f && f.Name == "absoluteEvasionPercent");
        int barrier = native.FindIndex(i => Text(i, "ABSOLUTEEVASION"));
        int consume = native.FindIndex(i => Text(i, "USE_ABSOLUTEEVASION_STAT"));
        // A separate dash-invulnerability path invokes OnEvade earlier too.
        int evade = consume < 0 ? -1 : native.FindIndex(consume, i => i.operand is FieldInfo f && f.Name == "OnEvade");
        Require(ignore < absolute && absolute < barrier && barrier < consume && consume < evade,
            "per-hit item dodge, guaranteed barrier consumption and OnEvade remain separate native paths");
        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        Reject(native.Where(i => !Call(i, "Mathf", "Log")), "missing native formula rejects installation");
        Reject(native.Concat(new[] { native[formula] }), "duplicate formula cannot be patched ambiguously");
        Reject(native.Where(i => !Text(i, "IGNOREEVASION")), "changed anti-Evasion ordering requires reaudit");
        Reject(native.Where(i => !Call(i, "Random", "Range")), "missing native roll requires reaudit");
        Reject(native.Select(i => i.LoadsConstant(6200f) ? new CodeInstruction(OpCodes.Ldc_R4, 6000f) : i), "formula scale changes require reaudit");
        Reject(native.Select(i => i.LoadsConstant(0.8f) ? new CodeInstruction(OpCodes.Ldc_R4, 0.9f) : i), "formula multiplier changes require reaudit");
        Reject(native.Select(i => i.LoadsConstant(10000f) ? new CodeInstruction(OpCodes.Ldc_R4, 20000f) : i), "native stat clamp changes require reaudit");
        var changed = native.Select(i => new CodeInstruction(i)).ToList();
        changed[cap].opcode = OpCodes.Pop; changed[cap].operand = null;
        Reject(changed, "missing chance local cannot corrupt the native evaluation stack");
        Console.WriteLine("Verified PvP Evasion cap at native formula only, anti-Evasion ordering, unchanged item dodge/roll/callback paths and changed-IL rejection (not live Unity).");
    }
}
