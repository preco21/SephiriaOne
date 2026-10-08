using System.Reflection;
using HarmonyLib;
using SephiriaOne;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
typeof(FriendlyFireSettings).Assembly.GetType("SephiriaOne.FriendlyFireHooks")?.GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null,null);
var attacker = new PlayerAvatar { Name = "Alice" };
var victim = new PlayerAvatar { Name = "Bob" };
DamageInstance Hit(float amount = 10) => new() { origin = attacker, damage = amount };
Check(victim.ApplyDamage(Hit()) == EApplyDamageResult.Fail_Absolute && victim.Hp == 100, "Default preserves ally protection");
SessionSettings.FriendlyFireForHit = new(true,50);
victim.ApplyDamage(Hit());
Check(victim.Hp == 95, "Enabled allied hit applies 50 percent damage");
victim.Shield = 20; victim.TrueDamage = 10; victim.Defense = 4;
victim.ApplyDamage(Hit());
Check(victim.Shield == 12 && victim.Hp == 95, "Scale resolved defense and true damage before shields");
victim.Shield = 0; victim.TrueDamage = victim.Defense = 0;
var enemy = new UnitAvatar { faction = "enemy" };
enemy.ApplyDamage(Hit()); Check(enemy.Hp == 90, "Enemy damage is unchanged");
var follower = new UnitAvatar { NetworkLeader = attacker, faction = "enemy" };
follower.ApplyDamage(Hit()); Check(follower.Hp == 95, "Leader can damage allied follower without changing its faction");
Check(follower.NetworkLeader == attacker, "Leader relationship is preserved");
var friendlyNpc = new UnitAvatar { faction = "friends" };
friendlyNpc.ApplyDamage(Hit()); Check(friendlyNpc.Hp == 95, "Friendly faction receives allied damage");
var self = Hit(); self.targetFactionLayers = -1;
attacker.ApplyDamage(self); Check(attacker.Hp == 90, "Existing self damage is not scaled");
var system = Hit(); system.isSystemDamage = true;
victim.ApplyDamage(system); Check(victim.Hp == 95, "System damage retains original faction check");
victim.IsInvulnerable = true; victim.ApplyDamage(Hit()); Check(victim.Hp == 95, "Invulnerability survives patch"); victim.IsInvulnerable = false;
SessionSettings.FriendlyFireForHit = new(true,0);
int hits = victim.Hits; victim.ApplyDamage(Hit());
Check(victim.Hp == 95 && victim.Hits == hits, "Zero scale rejects damage and on-hit effects");
SessionSettings.FriendlyFireForHit = new(true,300);
victim.OnHit = (_,_) => attacker.ApplyDamage(new() { origin = victim, damage = 10 });
victim.ApplyDamage(Hit()); Check(victim.Hp == 65 && attacker.Hp == 90, "Recursive ally retaliation is suppressed");
victim.OnHit = null;
var reused = Hit(); victim.ApplyDamage(reused); enemy.ApplyDamage(reused);
Check(reused.damage == 10 && victim.Hp == 35 && enemy.Hp == 80, "Reused damage object does not leak allied scaling to enemies");
victim.Revive = true; victim.ApplyDamage(Hit(100));
Check(!victim.IsDead && DungeonManager.Instance.Messages.Count == 0, "Extra life does not log a kill");
victim.OnDeath = d => d.origin = enemy;
victim.ApplyDamage(Hit(100));
Check(victim.IsDead && DungeonManager.Instance.Messages.Single().Contains("Alice") && DungeonManager.Instance.Messages[0].Contains("Bob"), "Confirmed friendly kill retains attacker across pooled damage reuse in death callbacks");
victim.Die(5,Hit()); Check(DungeonManager.Instance.Messages.Count == 1, "Repeated death does not log twice");
victim = new PlayerAvatar(); SessionSettings.FriendlyFireForHit = default;
victim.ApplyDamage(Hit()); Check(victim.Hp == 100, "Off takes effect on existing attacks immediately");
SessionSettings.FriendlyFireForHit = new(true,100); Mirror.NetworkServer.active = false;
victim.ApplyDamage(Hit()); Check(victim.Hp == 100, "Guests cannot cause authoritative allied damage");
Mirror.NetworkServer.active = true;
victim.ApplyDamage(Hit(float.NaN)); victim.ApplyDamage(Hit(float.PositiveInfinity)); victim.ApplyDamage(Hit(-10));
Check(victim.Hp == 100 && victim.Hits == 0, "Invalid or negative allied damage cannot turn into a native minimum hit");
victim.OnHit = (_,_) => throw new InvalidOperationException("fixture failure");
try { victim.ApplyDamage(Hit()); } catch (InvalidOperationException) { }
victim.OnHit = null;
float before = victim.Hp; victim.ApplyDamage(Hit());
Check(victim.Hp == before - 10, "Finalizer restores context after an exception");
victim.followers.Add(attacker);
victim.ApplyDamage(Hit());
Check(victim.Hp == before - 20, "Native follower-list protection is bypassed only for allied hits");
Check(FriendlyFireRuntime.SafeName("<color=red>Alice</color>\n") == "Alice", "Chat strips markup and line breaks");
SessionSettings.FriendlyFireForHit = new(true,300);
var result = EApplyDamageResult.Success;
FriendlyFireRuntime.BeforeHit(victim, Hit(), ref result, out var saved);
Check(FriendlyFireRuntime.Scale(float.MaxValue) > 0 && FriendlyFireRuntime.Scale(float.MaxValue) < int.MaxValue,
    "Scaling cannot overflow downstream native integer damage conversion");
FriendlyFireRuntime.AfterHit(null, saved);
victim = new PlayerAvatar { IsMpShield = true, Mp = int.MaxValue };
victim.ApplyDamage(Hit(float.MaxValue));
Check(victim.Mp == 127 && victim.Hp == 100, "Extreme allied damage is safely converted by native MP shield");
foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -10f })
{
    victim = new PlayerAvatar { IsMpShield = true, OnCalculateDamage = d => d.damage = bad };
    victim.ApplyDamage(Hit());
    Check(victim.Mp == 100 && victim.Hp == 100, "Invalid derived damage cannot corrupt HP or MP: " + bad);
}
var code = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(UnitAvatar), "ApplyDamage")).ToList();
Check(FriendlyFireTranspiler.Validate(code), "Original fixture IL satisfies contract");
foreach (string anchor in new[] { "TOUGHNESS", "MPSHIELD" })
{
    var changed = code.Select(i => new CodeInstruction(i)).ToList();
    changed.First(i => Equals(i.operand, anchor)).operand = "CHANGED";
    Check(!FriendlyFireTranspiler.Validate(changed), "Changed native arithmetic contract rejects: " + anchor);
}
FriendlyFireHooks.Uninstall();
victim = new PlayerAvatar(); victim.ApplyDamage(Hit());
Check(victim.Hp == 100, "Unload removes ally exception and restores native behavior");
Console.WriteLine($"Passed {checks} friendly-fire executable hook checks.");
