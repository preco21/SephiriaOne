using HarmonyLib;
using SephiriaOne;
using System.Reflection.Emit;

int checks = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
EventSpawnHooks.Install();
try
{
    var choice = new StageEntity_Choice(); var town = new StageEntity_GrasslandTown();
    foreach (decimal multiplier in new[] { 0m, .5m, 1m, 2m, 10m, 10000m })
    {
        SessionSettings.Current = new(multiplier);
        for (int seed = 0; seed < 200; seed++)
        {
            var random = new Random(seed); double roll = new Random(random.Next() + 10000).NextDouble(); int next = random.Next();
            int expected = roll < 0 || multiplier == 0 ? 0 : roll <= Math.Min(.003 * (double)multiplier, 1) ? 2 : roll <= Math.Min(.043 * (double)multiplier, 1) ? 1 : 0;
            foreach (var data in new[] { choice.GenerateStage(seed)[0], town.GenerateStage(seed)[0] })
            { Check(data.randomRoomCount == expected, "Native cumulative probabilities scale correctly"); Check(data.seed == next, "Subsequent native RNG stream unchanged"); }
        }
    }
    Check(EventSpawnHooks.Threshold(.043) == 1 && EventSpawnHooks.Threshold(.003) == 1, "High multipliers cap at 100%, never more than two rooms");
    SessionSettings.Current = new(0);
    Check(EventSpawnHooks.Threshold(.043) < 0 && EventSpawnHooks.Threshold(.003) < 0, "Zero excludes even exact-zero RNG outcome");
    SessionSettings.Current = new(2);
    Check(EventSpawnHooks.Threshold(.043) == .086 && EventSpawnHooks.Threshold(.043) == .086, "Repeated calls do not compound");
    var existing = choice.GenerateStage(5)[0]; int before = existing.randomRoomCount;
    SessionSettings.Current = default;
    Check(existing.randomRoomCount == before && EventSpawnHooks.Threshold(.043) == .043, "Reset leaves generated data alone and restores native threshold");
    Mirror.NetworkServer.active = false; SessionSettings.Current = new(2);
    Check(EventSpawnHooks.Threshold(.043) == .043, "Clients never apply host policy locally"); Mirror.NetworkServer.active = true;
    SessionSettings.Throw = true;
    Check(EventSpawnHooks.Threshold(.043) == .043 && EventSpawnHooks.Threshold(.003) == .003 && UnityEngine.Debug.Warnings.Count == 1, "Unavailable scope falls back without warning spam");
    SessionSettings.Throw = false;
    var code = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(StageEntity_Choice), "GenerateStage")).ToList();
    int at = code.FindIndex(i => i.opcode == OpCodes.Ldc_R8 && Equals(i.operand,.003d));
    var changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[at].operand = .004d;
    Check(!EventSpawnHooks.Validate(changed), "Changed native odds reject compatibility");
    changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[at+1].opcode = OpCodes.Blt_Un;
    Check(!EventSpawnHooks.Validate(changed), "Changed comparison direction rejected");
    changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[at+2].opcode = OpCodes.Ldc_I4_3;
    Check(!EventSpawnHooks.Validate(changed), "Changed count semantics rejected");
    changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[at+4].operand = changed[at+1].operand;
    Check(!EventSpawnHooks.Validate(changed), "Two-room branch must not fall into the one-room comparison");
    int initial = code.FindIndex(i => i.opcode == OpCodes.Ldc_I4_0);
    changed = code.Select(i => new CodeInstruction(i)).ToList(); changed[initial].opcode = OpCodes.Ldc_I4_3;
    Check(!EventSpawnHooks.Validate(changed), "Default room count must be zero");
    EventSpawnHooks.Uninstall();
    Check(choice.GenerateStage(5)[0].randomRoomCount == (new Random(new Random(5).Next()+10000).NextDouble() <= .003 ? 2 : new Random(new Random(5).Next()+10000).NextDouble() <= .043 ? 1 : 0), "Unload restores native path");
    Console.WriteLine($"Passed {checks} random event threshold/RNG hook fixture checks.");
}
finally { EventSpawnHooks.Uninstall(); }
