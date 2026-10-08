using System.Reflection;
using HarmonyLib;
using SephiriaOne;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
typeof(JarSpawnSettings).Assembly.GetType("SephiriaOne.JarSpawnHooks")?.GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null,null);
MysticPot Spawn(int seed, float rate = .18f, bool random = true, int chapter = 0)
{
    var pot = new MysticPot { RandomID = seed, appearRate = rate, useRandomAppear = random, minChapterNum = chapter };
    pot.OnStartServer(); return pot;
}
for (int seed = 0; seed < 20; seed++)
    Check(Spawn(seed).isGenerated == (new Random(seed).NextDouble() <= .18f), "Native seeded outcome is unchanged");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,100);
Check(Spawn(0).isGenerated, "100% override admits an eligible jar rejected by native 18%");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,0);
Check(!Spawn(0).isGenerated && Spawn(0,random:false).isGenerated, "Zero suppresses random placements without touching guaranteed jars");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,100);
Check(!Spawn(0,chapter:2).isGenerated, "Override cannot bypass chapter restrictions");
DungeonManager.Instance.dungeonEnvironment["ChapterNum"] = 2;
Check(Spawn(0,chapter:2).isGenerated, "Chapter-eligible placement uses override");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Multiplier,2);
foreach (float rate in new[] { .1f, .18f, .4f, .75f })
    for (int seed = 0; seed < 15; seed++)
        Check(Spawn(seed,rate).isGenerated == (new Random(seed).NextDouble() <= Math.Min(rate * 2f,1f)), "Multiplier uses each placement's native baseline");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,100);
for (int seed = 0; seed < 35; seed++)
{
    var hidden = new HiddenRoomRewardSpawner { Seed = seed }; hidden.OnStartServer();
    var rng = new Random(seed); int roll = rng.Next(0,5);
    Check(hidden.Roll == roll, "Hidden reward type stays native");
    if (roll == 4) Check(hidden.Spawned.isGenerated == (new Random(rng.Next()).NextDouble() <= .18f), "Hidden reward jar retains native inner chance");
}
try { new HiddenRoomRewardSpawner { Throw = true }.OnStartServer(); } catch (InvalidOperationException) { }
Check(Spawn(0).isGenerated, "Exception restores hidden-reward scope");
var existing = Spawn(0);
SessionSettings.JarSpawnsForGeneration = default;
Check(existing.isGenerated && !Spawn(0).isGenerated, "Reset affects future checks and does not reroll existing objects");
Mirror.NetworkServer.active = false; SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,100);
Check(!Spawn(0).isGenerated, "Non-host callback cannot apply addon policy");
Mirror.NetworkServer.active = true;
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,0);
Check(JarSpawnHooks.Chance(new MysticPot()) < 0, "Explicit zero rejects even an exact-zero native draw");
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Multiplier,2);
var unchanged = new MysticPot { appearRate = .18f };
float first = JarSpawnHooks.Chance(unchanged), second = JarSpawnHooks.Chance(unchanged);
Check(first == .36f && second == first && unchanged.appearRate == .18f, "Repeated checks cannot compound or rewrite native map chance");
JarSpawnHooks.BeforeHiddenReward(out int outer); JarSpawnHooks.BeforeHiddenReward(out int inner);
Check(JarSpawnHooks.Chance(unchanged) == .18f, "Nested hidden rewards preserve native chance");
JarSpawnHooks.AfterHiddenReward(null,inner);
Check(JarSpawnHooks.Chance(unchanged) == .18f, "Inner cleanup preserves enclosing hidden scope");
JarSpawnHooks.AfterHiddenReward(null,outer);
Check(JarSpawnHooks.Chance(unchanged) == .36f, "Outermost cleanup restores normal override");
var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(typeof(MysticPot),"OnStartServer")).ToList();
var changed = code.Select(i => new CodeInstruction(i)).ToList();
int field = changed.FindIndex(i => i.operand is FieldInfo f && f.Name == "appearRate");
changed[field + 2].opcode = System.Reflection.Emit.OpCodes.Bge_Un_S;
Check(!JarSpawnHooks.Validate(changed), "Inverted native chance comparison rejects compatibility");
changed = code.Select(i => new CodeInstruction(i)).ToList(); changed.RemoveAt(field);
Check(!JarSpawnHooks.Validate(changed), "Missing native chance read rejects compatibility");
JarSpawnHooks.Uninstall();
SessionSettings.JarSpawnsForGeneration = new(JarSpawnMode.Chance,100);
Check(!Spawn(0).isGenerated, "Unload restores native seeded chance");
Console.WriteLine($"Passed {checks} Mystic Jar hook checks.");
