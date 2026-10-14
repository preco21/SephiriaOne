using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireDebuffTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = addon.GetType("SephiriaOne.FriendlyFireDebuffBalanceHooks", true)!;
        var rewrite = AccessTools.Method(hooks, "Rewrite");
        void Require(bool condition, string message) { if (!condition) throw new Exception("PvP debuff cadence: " + message); }
        bool Call(CodeInstruction i, string type, string method) =>
            i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == method;

        var updates = ((IEnumerable<MethodInfo>)AccessTools.Method(hooks, "Updates").Invoke(null, null)!).ToArray();
        Require(updates.Length == 5, "five audited periodic debuff types");
        foreach (var update in updates)
        {
            var original = PatchProcessor.GetOriginalInstructions(update).ToList();
            List<CodeInstruction> Map(List<CodeInstruction> code) =>
                ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { code, update })!).ToList();
            var mapped = Map(original);
            int helper = mapped.FindIndex(i => Call(i, "FriendlyFireRuntime", "DebuffTickDelta"));
            Require(mapped.Count == original.Count + 2 && helper >= 2 && mapped[helper - 1].opcode == OpCodes.Ldarg_0 &&
                (mapped[helper - 2].opcode == OpCodes.Mul || Call(mapped[helper - 2], "Time", "get_deltaTime")) &&
                Call(mapped[helper + 1], "Timer", "Update"), update + " wraps only the periodic timer delta");
            mapped.RemoveRange(helper - 1, 2);
            Require(mapped.Zip(original).All(p => p.First.opcode == p.Second.opcode && Equals(p.First.operand, p.Second.operand) &&
                p.First.labels.SequenceEqual(p.Second.labels) && p.First.blocks.SequenceEqual(p.Second.blocks)),
                update + " preserves all native damage, stacking, charge, HUD, speed and control flow");
            void Reject(Action<List<CodeInstruction>> mutate, string why)
            {
                var changed = original.Select(i => new CodeInstruction(i)).ToList(); mutate(changed);
                bool rejected = false;
                try { Map(changed); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                Require(rejected, update + ": " + why);
            }
            int tick = original.FindIndex(i => Call(i, "Timer", "Update"));
            int field = original.FindLastIndex(tick, i => i.operand is FieldInfo f && f.FieldType.Name == "Timer");
            Reject(c => c.RemoveAt(field), "missing periodic timer rejected");
            Reject(c => c.RemoveAt(c.FindLastIndex(tick, i => Call(i, "Time", "get_deltaTime"))), "missing elapsed input rejected");
            Reject(c => c[tick - 1].opcode = OpCodes.Add, "changed tick calculation rejected");
            Reject(c => c.Add(new CodeInstruction(c[tick])), "ambiguous timer update rejected");
            Reject(c => c[tick].labels.Add(default), "branch into wrapped call rejected");
        }
        var install = PatchProcessor.GetOriginalInstructions(AccessTools.Method(addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!, "Install"));
        Require(install.Any(i => Call(i, "FriendlyFireDebuffBalanceHooks", "Install")), "shared FF install includes debuff hooks");
        var duration = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("CharacterDebuff", true)!, "Update"));
        Require(duration.Any(i => Call(i, "Time", "get_deltaTime")) && duration.Any(i => Call(i, "Timer", "Update")),
            "native expiry remains in a separate unmodified method");
        var freeze = game.GetType("CharacterDebuff_Freeze", true)!;
        var frozen = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(freeze, "ApplyDamage")).ToList();
        int damage = frozen.FindIndex(i => Call(i, "CombatBehaviour", "ApplyDamage"));
        int stun = frozen.FindIndex(i => Call(i, "UnitAvatar", "StartStun"));
        Require(damage >= 0 && frozen[damage + 1].opcode == OpCodes.Pop && stun > damage &&
            frozen[stun - 1].operand is MethodInfo durationRead && durationRead.Name == "get_BuffDuration",
            "Freeze starts its native-duration stun independently after the damage result is discarded");
        Require(frozen.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Debuff_Freeze")), "native Freeze damage identifier");
        foreach (string name in new[] { "CharacterDebuff_Electric", "CharacterDebuff_Plasma" })
        {
            var type = game.GetType(name, true)!;
            var ctor = PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(type));
            Require(ctor.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Debuff_Electric")), "native shock identifier: " + name);
            var attack = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, "Attack"));
            Require(attack.Any(i => Call(i, "CombatBehaviour", "ApplyDamage")), "shock refresh/expiry/luck share the damage path: " + name);
        }
        var frostStack = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("CharacterDebuff_Frostbite", true)!, "AddStack"));
        Require(frostStack.Any(i => Call(i, "UnitAvatar", "ApplyDebuff")), "native Frostbite-to-Freeze application remains separate from ticks");
        Console.WriteLine("Verified five PvP debuff timers, shock identifiers, preserved Freeze stun/expiry and changed-contract rejection (not live gameplay).");
    }
}
