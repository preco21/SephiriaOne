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
attacker.ApplyDamage(self); Check(attacker.Hp == 100, "Native self-protection is preserved");
var system = Hit(); system.isSystemDamage = true;
victim.ApplyDamage(system); Check(victim.Hp == 95, "System damage retains original faction check");
victim.IsInvulnerable = true; victim.ApplyDamage(Hit()); Check(victim.Hp == 95, "Invulnerability survives patch"); victim.IsInvulnerable = false;
SessionSettings.FriendlyFireForHit = new(true,0);
int hits = victim.Hits; victim.ApplyDamage(Hit());
Check(victim.Hp == 95 && victim.Hits == hits, "Zero scale rejects damage and on-hit effects");
SessionSettings.FriendlyFireForHit = new(true,300);
victim.OnHit = (_,_) => attacker.ApplyDamage(new() { origin = victim, damage = 10 });
victim.ApplyDamage(Hit()); Check(victim.Hp == 65 && attacker.Hp == 100, "Recursive ally retaliation is suppressed");
victim.OnHit = null;
var reused = Hit(); victim.ApplyDamage(reused); enemy.ApplyDamage(reused);
Check(reused.damage == 10 && victim.Hp == 35 && enemy.Hp == 80, "Reused damage object does not leak allied scaling to enemies");
victim.ExtraLife = true; victim.ApplyDamage(Hit(100));
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
SessionSettings.FriendlyFireForHit = new(true,50);
var guarded = new PlayerAvatar { IsGuarding = true };
Check(guarded.ApplyDamage(Hit()) == EApplyDamageResult.Fail_Block && guarded.Hp == 100 && guarded.Mp == 90 && guarded.GuardHits == 1,
    "Native sword-and-shield guard still spends MP and blocks the hit");
guarded.IsGuarding = false;
Check(guarded.ApplyDamage(Hit()) == EApplyDamageResult.Success && guarded.Hp == 95 && guarded.Mp == 90,
    "Lowering the guard permits scaled HP damage despite the player's native team-protection callback");
var overflow = new PlayerAvatar { Shield = 2 };
overflow.ApplyDamage(Hit());
Check(overflow.Shield == 0 && overflow.Hp == 97, "Shield overflow reaches HP after scaling once");
var mpOverflow = new PlayerAvatar { IsMpShield = true, Mp = 2 };
mpOverflow.ApplyDamage(Hit());
Check(mpOverflow.Mp == 0 && mpOverflow.Hp == 97, "MP shield overflow reaches HP after scaling once");
attacker.safeMode = true;
var team = new PlayerAvatar(); team.ApplyDamage(Hit());
Check(team.Hp == 95, "Enabled team friendly fire works while native NPC safe mode is enabled");
var teamFollower = new UnitAvatar { NetworkLeader = new PlayerAvatar() };
teamFollower.ApplyDamage(Hit());
Check(teamFollower.Hp == 95, "Other players' allied followers bypass only team protection");
var protectedNpc = new UnitAvatar(); protectedNpc.ApplyDamage(Hit());
Check(protectedNpc.Hp == 100, "High-friendship NPC protection is preserved");
protectedNpc.faction = "friends"; protectedNpc.ApplyDamage(Hit());
Check(protectedNpc.Hp == 100, "NPC safe mode still denies an attack");
attacker.safeMode = false;
DungeonManager.Instance.ReasonShieldAllows = false;
int reasonChecks = DungeonManager.Instance.ReasonChecks;
Check(protectedNpc.ApplyDamage(Hit()) == EApplyDamageResult.Fail_Block && protectedNpc.Hp == 100 &&
    DungeonManager.Instance.ReasonChecks == reasonChecks + 1, "Native NPC Shield of Reason/crime handling still runs");
DungeonManager.Instance.ReasonShieldAllows = true;
foreach (bool denyBeforeNative in new[] { false, true })
{
    var callbacks = attacker.OnAttackUnitBeforeOperation;
    Action<UnitAvatar, DamageInstance> deny = (_, d) => d.failed = EDamageFailType.Deny;
    attacker.OnAttackUnitBeforeOperation = denyBeforeNative ? deny + callbacks : callbacks + deny;
    try
    {
        team = new PlayerAvatar(); team.ApplyDamage(Hit());
        Check(team.Hp == 100, "Other before-attack subscribers can still deny a hit, before native callback=" + denyBeforeNative);
    }
    finally { attacker.OnAttackUnitBeforeOperation = callbacks; }
}
team = new PlayerAvatar { OnCalculateDamage = d => d.failed = EDamageFailType.Block };
Check(team.ApplyDamage(Hit()) == EApplyDamageResult.Fail_Block && team.Hp == 100, "Victim callbacks retain native blocking");
team = new PlayerAvatar();
team.ApplyDamage(new() { origin = new UnitAvatar { faction = "enemy" }, targetFactionLayers = -1, damage = 10 });
Check(team.Hp == 90, "NPC-origin hits retain native damage and are not scaled as player friendly fire");

var contextHit = Hit();
FriendlyFireRuntime.BeforeHit(team, contextHit, ref result, out saved);
try
{
    attacker.InvokeBeforeAttack(team, contextHit);
    Check(contextHit.failed == EDamageFailType.None, "Exact active team hit bypasses only native team protection");
    contextHit.failed = EDamageFailType.Block;
    attacker.InvokeBeforeAttack(team, contextHit);
    Check(contextHit.failed == EDamageFailType.Block, "Exception never clears a pre-existing failure");
    var otherHit = Hit(); attacker.InvokeBeforeAttack(team, otherHit);
    Check(otherHit.failed == EDamageFailType.Deny, "Different damage instance cannot borrow active team exception");
    contextHit.failed = EDamageFailType.None;
    attacker.InvokeBeforeAttack(new PlayerAvatar(), contextHit);
    Check(contextHit.failed == EDamageFailType.Deny, "Different victim cannot borrow active team exception");
    contextHit.failed = EDamageFailType.None;
    new PlayerAvatar().InvokeBeforeAttack(team, contextHit);
    Check(contextHit.failed == EDamageFailType.Deny, "Different attacker cannot borrow active team exception");
    Mirror.NetworkServer.active = false; contextHit.failed = EDamageFailType.None;
    attacker.InvokeBeforeAttack(team, contextHit);
    Check(contextHit.failed == EDamageFailType.Deny, "Authority loss cannot keep the native protection exception active");
}
finally { Mirror.NetworkServer.active = true; FriendlyFireRuntime.AfterHit(null, saved); }
var outside = Hit(); attacker.InvokeBeforeAttack(team, outside);
Check(outside.failed == EDamageFailType.Deny, "Callback outside active hit context retains native behavior");
var protection = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(PlayerAvatar), "HandleBeforeAttack")).ToList();
Check(FriendlyFireHooks.ValidatePlayerProtection(protection), "Native player protection fixture contract is recognized");
var changedProtection = protection.Select(i => new CodeInstruction(i)).ToList();
changedProtection.RemoveAt(changedProtection.FindIndex(i => i.operand is MethodInfo m && m.Name == "BreakShieldOfReason"));
Check(!FriendlyFireHooks.ValidatePlayerProtection(changedProtection), "Missing NPC protection contract disables exception");
changedProtection = protection.Select(i => new CodeInstruction(i)).ToList();
changedProtection.Add(CodeInstruction.Call(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.Clear)));
Check(!FriendlyFireHooks.ValidatePlayerProtection(changedProtection), "Additional callback work is not silently skipped");
CompanionTests.Run(Check);
ReflectionTests.Run(Check);
var relationCode = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(UnitAI_NewBasic), "GetRelation")).ToList();
Check(FriendlyFireHooks.ValidateCompanionRelation(relationCode), "Companion relation fixture matches native contract");
relationCode.RemoveAt(relationCode.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_NetworkLeader"));
Check(!FriendlyFireHooks.ValidateCompanionRelation(relationCode), "Changed ownership contract rejects companion hook");
var updateCode = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(UnitAI_NewBasic), "OnAIUpdate")).ToList();
var targetCode = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(UnitAI_NewBasic), "SetTarget")).ToList();
Check(FriendlyFireHooks.ValidateCompanionUpdate(updateCode, targetCode), "Native battle-state/target-loss fixture is recognized");
targetCode.RemoveAt(targetCode.FindIndex(i => i.operand is MethodInfo m && m.Name == "OnLostTarget"));
Check(!FriendlyFireHooks.ValidateCompanionUpdate(updateCode, targetCode), "Missing native attack cleanup rejects companion update hook");
KdaTests.Run(Check);
KillLogTests.Run(Check);
EffectTests.Run(Check);
FriendlyFireHooks.Uninstall();
Check(PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAvatar), "AddReceivedDamage"))?.Postfixes.Count is null or 0 &&
    PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAvatar), "Revive"))?.Prefixes.Count is null or 0 &&
    PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAvatar), "Die"))?.Finalizers.Count is null or 0,
    "Unload removes KDA accounting, revival and death hooks");
var nativeRing = new WeaponAddonCommon_BurnRing { NetworkAvatar = new PlayerAvatar(), Target = new PlayerAvatar() };
nativeRing.DamageNearbyEnemies();
Check(!nativeRing.Selected, "Unload restores native item filters");
Check(PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAvatar), "ApplyDebuff"))?.Prefixes.Count is null or 0 &&
    PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(CharacterDebuff), "Update"))?.Prefixes.Count is null or 0,
    "Unload removes debuff admission and lifetime hooks");
victim = new PlayerAvatar(); victim.ApplyDamage(Hit());
Check(victim.Hp == 100, "Unload removes ally exception and restores native behavior");
Check(PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(PlayerAvatar), "HandleBeforeAttack"))?.Prefixes.Count is null or 0,
    "Unload removes native player protection patch");
var unpatchedAi = new UnitAI_NewBasic { Avatar = new UnitAvatar { NetworkLeader = new PlayerAvatar() }, CurrentTarget = new PlayerAvatar() };
Check(!unpatchedAi.ShouldAttack && PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAI_NewBasic), "GetRelation"))?.Postfixes.Count is null or 0,
    "Unload restores native companion relations");
Check(PatchProcessor.GetPatchInfo(AccessTools.Method(typeof(UnitAI_NewBasic), "OnAIUpdate"))?.Prefixes.Count is null or 0,
    "Unload removes companion transition cleanup");
Console.WriteLine($"Passed {checks} friendly-fire executable hook checks.");
