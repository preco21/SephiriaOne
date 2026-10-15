using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameHarvestTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var avatar = game.GetType("UnitAvatar", true)!;
        var apply = AccessTools.DeclaredMethod(avatar, "ApplyDamage");
        var code = PatchProcessor.GetOriginalInstructions(apply).ToList();
        var hp = AccessTools.DeclaredMethod(avatar, "Heal", new[] { typeof(float) });
        var mp = AccessTools.DeclaredMethod(avatar, "HealMpFloat", new[] { typeof(float) });
        var rewrite = AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.FriendlyFireHarvestHooks", true)!, "Rewrite");
        List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> input) =>
            ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { input })!).ToList();
        bool Calls(CodeInstruction i, string method) => i.operand is MethodInfo m && m.DeclaringType == avatar && m.Name == method;
        void Require(bool pass, string why) { if (!pass) throw new Exception("Harvest contract: " + why); }
        var actual = Rewrite(code);
        int cursor = 0;
        foreach (var native in code)
        {
            bool recovery = native.Calls(hp) || native.Calls(mp);
            if (recovery)
            {
                Require(actual[cursor].operand is MethodInfo m && m.DeclaringType?.Name == "FriendlyFireRuntime" && m.Name == "HarvestAmount" &&
                    actual[cursor].labels.SequenceEqual(native.labels) && actual[cursor].blocks.SequenceEqual(native.blocks),
                    "only each Harvest amount is adjusted, with branch/exception boundaries preserved");
                cursor++;
            }
            Require(actual[cursor].opcode == native.opcode && Equals(actual[cursor].operand, native.operand) &&
                (recovery ? actual[cursor].labels.Count == 0 && actual[cursor].blocks.Count == 0 :
                    actual[cursor].labels.SequenceEqual(native.labels) && actual[cursor].blocks.SequenceEqual(native.blocks)),
                "all native arithmetic, recovery calls and control flow remain intact");
            cursor++;
        }
        Require(cursor == actual.Count && actual.Count == code.Count + 2, "exactly two amount helpers added");
        void Reject(IEnumerable<CodeInstruction> changed, string why)
        {
            bool rejected = false;
            try { Rewrite(changed); }
            catch (InvalidOperationException) { rejected = true; }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
            Require(rejected, why);
        }
        Reject(code.Where(i => !i.Calls(hp)), "missing HP recovery rejects installation");
        Reject(code.Where(i => !i.Calls(mp)), "missing MP recovery rejects installation");
        Reject(code.Concat(code.Where(i => i.Calls(hp))), "additional healing site requires an audit");
        Reject(code.Where(i => i.operand is not FieldInfo f || f.Name != "hpSteal"), "per-hit HP Harvest contract is guarded");
        var changedStat = code.Select(i => new CodeInstruction(i)).ToList();
        var hpEnum = Convert.ToInt32(Enum.Parse(game.GetType("ECustomStat", true)!, "HPSteal"));
        int hpRead = Enumerable.Range(1, changedStat.Count - 1).FirstOrDefault(i =>
            Calls(changedStat[i], "GetCustomStat") && changedStat[i - 1].LoadsConstant(hpEnum));
        Require(hpRead > 0, "native HPSteal query found");
        changedStat[hpRead - 1] = new(OpCodes.Ldc_I4, -1);
        Reject(changedStat, "different stat query cannot masquerade as Harvest");

        int hpGrant = code.FindIndex(i => i.Calls(hp)), mpGrant = code.FindIndex(i => i.Calls(mp));
        Require(code.FindIndex(i => Calls(i, "set_Networkhp")) < hpGrant && hpGrant < mpGrant &&
            mpGrant < code.FindIndex(i => Calls(i, "Die")), "Harvest follows actual HP damage and precedes death");
        var hpHeal = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, "Heal", new[] { typeof(float), typeof(bool), typeof(bool) })).ToList();
        var mpHeal = PatchProcessor.GetOriginalInstructions(mp).ToList();
        Require(hpHeal.Any(i => Calls(i, "set_Networkhp")) && hpHeal.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "HEALINGPENALTY")),
            "native HP penalties/caps and authoritative health writes remain in the called method");
        Require(mpHeal.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "mpStack") &&
            mpHeal.Any(i => Calls(i, "set_Networkmp")) && mpHeal.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "MPREGENMULTIPLE")),
            "native fractional MP, recovery multiplier and authoritative MP writes remain in the called method");
        foreach (string setter in new[] { "set_Networkhp", "set_Networkmp" })
            Require(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(avatar, setter)).Any(i =>
                i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter"), "stock clients receive " + setter);
        Console.WriteLine("Verified HP/MP Harvest call-site isolation, exact native IL preservation, drift rejection and stock HP/MP replication (not live multiplayer).");
    }
}
