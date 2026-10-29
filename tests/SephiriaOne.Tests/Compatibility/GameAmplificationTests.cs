using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameAmplificationTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        GameCriticalDamageTests.Run(game, addon);
        GameElementalStatTests.Run(game, addon);
        var avatar = game.GetType("UnitAvatar", true)!;
        var damage = game.GetType("DamageInstance", true)!;
        var stat = game.GetType("ECustomStat", true)!;
        int allDamage = Convert.ToInt32(Enum.Parse(stat, "AllDamageBonus"));
        var getStat = AccessTools.DeclaredMethod(avatar, "GetCustomStat", new[] { stat });
        var getLeader = AccessTools.PropertyGetter(avatar, "NetworkLeader");
        var damageField = AccessTools.DeclaredField(damage, "damage");
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "ApplyDamage"), out var generator).ToList();
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireAmplificationHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code })!).ToList();
        void Require(bool value, string why) { if (!value) throw new Exception("All Damage Amplified contract: " + why); }
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        int[] Reads(IReadOnlyList<CodeInstruction> code) => Enumerable.Range(1, code.Count - 1)
            .Where(i => code[i].Calls(getStat) && code[i - 1].LoadsConstant(allDamage)).ToArray();
        var reads = Reads(native);
        Require(reads.Length == 2, "exactly the attacker and optional leader read the enum stat");
        int own = reads[0], leader = reads[1];
        Require(native[own - 2].IsLdloc() && native[own + 1].IsStloc(), "attacker read initializes an integer local");
        int source = Local(native[own - 2]), bonus = Local(native[own + 1]);
        Require(bonus >= 0 && native[leader - 4].IsLdloc() && Local(native[leader - 4]) == bonus &&
            native[leader - 3].IsLdloc() && Local(native[leader - 3]) == source && native[leader - 2].Calls(getLeader) &&
            native[leader + 1].opcode == OpCodes.Add && native[leader + 2].IsStloc() && Local(native[leader + 2]) == bonus,
            "the same attacker's leader contributes to the same local before the limit");
        var uses = Enumerable.Range(0, native.Count).Where(i => native[i].IsLdloc() && Local(native[i]) == bonus).ToArray();
        Require(uses.Length == 2 && uses[0] == leader - 4, "combined bonus has one summation load and one damage consumption");
        int consume = uses[1], insertion = consume + 2;
        Require(native[consume - 2].opcode == OpCodes.Ldarg_1 && native[consume - 1].LoadsField(damageField) &&
            native[consume + 1].opcode == OpCodes.Conv_R4 && native[consume + 2].LoadsConstant(100f) &&
            native[consume + 3].opcode == OpCodes.Div && native[consume + 4].opcode == OpCodes.Mul,
            "native bonus is consumed as damage.damage times combined bonus percent");

        void CheckRewrite(List<CodeInstruction> original)
        {
            var patched = Rewrite(original);
            Require(patched.Count == original.Count + 1 && patched.Count(i => Call(i, "FriendlyFireRuntime", "LimitAmplification")) == 1,
                "exactly one amplification adapter is inserted");
            Require(patched[insertion].opcode == OpCodes.Call && patched[insertion].operand is MethodInfo helper &&
                helper.Name == "LimitAmplification" && helper.IsStatic && helper.ReturnType == typeof(float) &&
                helper.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(float) }),
                "only the combined float bonus enters the adapter, before native division and multiplication");
            for (int i = 0; i < original.Count; i++)
            {
                var mapped = patched[i < insertion ? i : i + 1];
                Require(original[i].opcode == mapped.opcode && Equals(original[i].operand, mapped.operand),
                    "every native arithmetic, stat read, write and callback instruction is preserved");
                var entry = patched[i <= insertion ? i : i + 1];
                Require(original[i].labels.SequenceEqual(entry.labels) && original[i].blocks.SequenceEqual(entry.blocks),
                    "branch labels and exception boundaries still enter the complete calculation");
            }
            Require(patched[insertion + 1].labels.Count == 0 && patched[insertion + 1].blocks.Count == 0,
                "moved entry metadata is not duplicated");
            Require(Reads(patched).Length == 2, "the adapter does not re-read or rewrite native stats");
        }
        CheckRewrite(native);
        var labeled = native.Select(i => new CodeInstruction(i)).ToList();
        var bonusLabel = generator.DefineLabel();
        var continuationLabel = generator.DefineLabel();
        labeled[consume + 1].labels.Add(bonusLabel);
        labeled[insertion].labels.Add(continuationLabel);
        labeled[insertion].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        CheckRewrite(labeled);
        Require(labeled[consume + 1].labels.Contains(bonusLabel) && labeled[insertion].labels.Contains(continuationLabel) &&
            labeled[insertion].blocks.Count == native[insertion].blocks.Count + 1, "rewriting leaves the input instruction metadata intact");

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
        Reject(native.Where((_, index) => index != own), "missing attacker read rejects installation");
        Reject(native.Where((_, index) => index != leader), "missing leader read rejects installation");
        Reject(native.Concat(native.Skip(own - 2).Take(4)), "additional amplification read requires a new audit");
        RejectAt(own - 1, new(OpCodes.Ldc_I4, -1), "changed attacker stat cannot match amplification");
        RejectAt(leader - 1, new(OpCodes.Ldc_I4, -1), "changed leader stat cannot match amplification");
        RejectAt(own + 1, new(OpCodes.Stloc, bonus + 100), "attacker bonus must initialize the consumed local");
        RejectAt(leader - 4, new(OpCodes.Ldloc, bonus + 100), "leader addition must load the attacker's bonus");
        RejectAt(leader - 3, new(OpCodes.Ldloc, source + 100), "leader must belong to the same attacking avatar");
        RejectAt(leader - 2, new(OpCodes.Nop), "missing leader lookup rejects installation");
        RejectAt(leader + 1, new(OpCodes.Sub), "changed leader arithmetic requires a new audit");
        RejectAt(leader + 2, new(OpCodes.Stloc, bonus + 100), "leader addition must update the consumed local");
        RejectAt(consume - 1, new(OpCodes.Ldfld, AccessTools.DeclaredField(damage, "criticalChancePercent")),
            "another damage field must not masquerade as the base-damage contribution");
        RejectAt(consume + 1, new(OpCodes.Conv_R8), "changed bonus conversion requires a new audit");
        RejectAt(consume + 2, new(OpCodes.Ldc_R4, 1f), "changed percent denominator requires a new audit");
        RejectAt(consume + 3, new(OpCodes.Mul), "changed percent arithmetic requires a new audit");
        RejectAt(consume + 4, new(OpCodes.Add), "changed base-damage operation requires a new audit");
        Reject(native.Concat(native.Skip(consume - 2).Take(7)), "a second combined-bonus consumption cannot be patched ambiguously");
        Reject(Rewrite(native), "the same instruction stream cannot receive the limit twice");

        var pipeline = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireTranspiler", true)!, "Rewrite");
        var combined = ((IEnumerable<CodeInstruction>)pipeline.Invoke(null, new object[] { native, generator })!).ToList();
        int limited = combined.FindIndex(i => Call(i, "FriendlyFireRuntime", "LimitAmplification"));
        int finalScale = combined.FindIndex(i => Call(i, "FriendlyFireRuntime", "Scale"));
        Require(limited > 0 && limited < finalScale && combined.Count(i => Call(i, "FriendlyFireRuntime", "LimitAmplification")) == 1,
            "shared ApplyDamage rewrite limits native amplification once before final PvP damage scaling");
        foreach (string helper in new[] { "ContinueHit", "EvasionChance", "GuardMpCost", "HarvestHp", "HarvestMpAmount", "Scale", "Sanitize" })
            Require(combined.Count(i => Call(i, "FriendlyFireRuntime", helper)) == 1, "amplification preserves the existing " + helper + " adapter");
        Require(Reads(combined).Length == 2, "the combined pipeline retains both native stat reads");
        Console.WriteLine("Verified native combined attacker/leader amplification consumption, one float adapter, preserved IL/labels, drift rejection and shared damage-pipeline composition (not live Unity).");
    }

    private static int Local(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Stloc_0) return 0;
        if (instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Stloc_1) return 1;
        if (instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Stloc_2) return 2;
        if (instruction.opcode == OpCodes.Ldloc_3 || instruction.opcode == OpCodes.Stloc_3) return 3;
        return instruction.operand switch
        {
            LocalVariableInfo local => local.LocalIndex,
            byte index => index,
            short index => index,
            int index => index,
            _ => -1
        };
    }
}
