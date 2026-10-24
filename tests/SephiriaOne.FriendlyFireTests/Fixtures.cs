using System.Runtime.CompilerServices;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object obj) => obj is not null && !obj.Destroyed;
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a,b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a,b);
        public override bool Equals(object obj) => ReferenceEquals(this,obj);
        public override int GetHashCode() => base.GetHashCode();
    }
    public static class Debug { public static void LogWarning(object message) {} }
    public static class Time { public static float time, unscaledTime; public static float deltaTime { get; set; } }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static float SqrMagnitude(Vector2 value) => value.sqrMagnitude;
        public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt((a - b).sqrMagnitude);
        public static implicit operator Vector2(Vector3 value) => new(value.x, value.y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static implicit operator Vector3(Vector2 value) => new(value.x, value.y);
    }
    public class Transform { public Vector3 position; }
    public class GameObject : Object { public bool activeSelf = true; public bool activeInHierarchy => activeSelf; }
}
namespace Mirror { public static class NetworkServer { public static bool active = true; } public static class NetworkClient { public static bool active = true; } }
namespace Mirror { public class SyncList<T> : List<T> { [MethodImpl(MethodImplOptions.NoInlining)] public new bool Contains(T item) => base.Contains(item); } }
namespace SephiriaOne
{
    internal static class FriendlyFireDarkCloudVisualHooks
    {
        internal static void TryInstall() { } // Observer transport has its own executable fixtures.
        internal static void Uninstall() { }
    }
    internal static partial class CombatMatchRuntime
    {
        internal static PlayerAvatar Chaser;
        internal static readonly HashSet<PlayerAvatar> Zombies = new();
        internal static readonly HashSet<PlayerAvatar> Humans = new();
        internal static void DynamicZombieRatio(int zombies, int humans) =>
            zombieBalance.Retarget(zombies, humans, UnityEngine.Time.unscaledTime);
        internal static void ResetDynamicZombieBalance() => zombieBalance = default;
        internal static bool IsActiveZombie(PlayerAvatar player) => player && !player.IsDead && Zombies.Contains(player);
        private static bool TryActiveZombieRole(PlayerAvatar player, out bool zombie)
        {
            zombie = IsActiveZombie(player);
            return player && !player.IsDead && (zombie || Humans.Contains(player));
        }
        internal static bool IsZombieSurvival => Zombies.Count > 0;
        internal static void CompletedMatchDeath(PlayerAvatar player, long token, bool success, bool anyDeath) { }
        internal static bool IsActiveChaser(PlayerAvatar player) => player && !player.IsDead && ReferenceEquals(player, Chaser);
        internal static Func<PlayerAvatar, PlayerAvatar, bool> PairRule;
        internal static bool KeepScores;
        internal static bool AllowsPair(PlayerAvatar a, PlayerAvatar b) => PairRule?.Invoke(a, b) ?? true;
        internal static bool ScoresEnabled(bool normal) => KeepScores || normal;
        internal static void SelectionChanged() { }
        internal static bool Protect;
        internal static PlayerAvatar LastDead, LastRevived;
        internal static bool ProtectDeath(PlayerAvatar player) => Protect && player != null;
        internal static void Died(PlayerAvatar player) => LastDead = player;
        internal static void Reviving(PlayerAvatar player) => LastRevived = player;
        internal static bool EnterDeath(PlayerAvatar player) => false;
        internal static void LeaveDeath(PlayerAvatar player, bool entered) { }
        internal static long MatchToken => 0;
        internal static bool IsPlayTagExplosion(PlayerAvatar player) => false;
        internal static Action<FriendlyFireParticipants.Receipt, FriendlyFireParticipants.Receipt, long> KillCompleted;
        internal static void ConfirmedKill(FriendlyFireParticipants.Receipt killer, FriendlyFireParticipants.Receipt victim, long token, string killerName = null) =>
            KillCompleted?.Invoke(killer, victim, token);
        internal static void Revived(FriendlyFireParticipants.Receipt receipt, long match) { }
        internal static void ReviveFailed(FriendlyFireParticipants.Receipt receipt, long match) { }
    }
    internal static class ReviveAllFeature { internal static bool Available = true; }
    internal static class SessionSettings
    {
        internal static bool ZombieDynamicBalance;
        internal static CombatRoleSettings PlayTagChaser, ZombieModifiers;
        internal static bool ZombieAnyDeath => false;
        private static FriendlyFireSettings settings;
        internal static long CombatBalanceRevision;
        internal static long CombatFrequencyRevision;
        internal static long CombatRecoveryRevision;
        internal static FriendlyFireSettings FriendlyFireForHit
        { get => settings; set { if (settings.Balance != value.Balance) CombatBalanceRevision++; if (settings.HitFrequency != value.HitFrequency || !settings.Rapid.SameFrequency(value.Rapid)) CombatFrequencyRevision++; if (!settings.Rapid.SameRecovery(value.Rapid)) CombatRecoveryRevision++; settings = value; FriendlyFireKda.SetEnabled(value.Enabled); } }
    }
}
public enum EApplyDamageResult { Success, Fail_Absolute, Fail_Block }
public enum EDamageFailType { None, Deny, Block }
public enum EDamageFromType { None, BasicAttack, Magic, DirectAttack }
public enum EDamageType { Slice, Projectile, ElementalEffectDamage, Fall }
public enum EDamageElementalType { None, IceAndLightning, FireAndLightning }
public enum EMonsterType { Normal, Dummy }
public enum EPersonality { Aggressive }
public enum ERelationBehaviour { Neutral, Friendly, Hostile }
public class CombatBehaviour : UnityEngine.Object
{
    public UnityEngine.Transform transform = new();
    public UnityEngine.GameObject gameObject = new();
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual EApplyDamageResult ApplyDamage(DamageInstance damage) => EApplyDamageResult.Fail_Absolute;
}
public class BoolFlag
{
    public bool Value = true;
    public bool IsFalse() => !Value;
    public bool IsTrue() => Value;
}
public class DamageInstance
{
    public EDamageElementalType elementalType;
    public CombatBehaviour origin;
    public long targetFactionLayers;
    public bool isSystemDamage;
    public EDamageFailType failed;
    public float damage = 10;
    public float hpSteal;
    public float absoluteEvasionPercent;
    public bool UsedAbsoluteEvasion;
    public void AddFlag(string flag) { if (flag == "USE_ABSOLUTEEVASION_STAT") UsedAbsoluteEvasion = true; }
    public string id = "";
    public string flag = "";
    public EDamageFromType fromType;
    public EDamageType damageType;
}
public partial class RuntimeFactionManager : UnityEngine.Object
{
    public static RuntimeFactionManager Instance { get; } = new();
    public ERelationBehaviour GetRelationBehaviour(string a, string b, EPersonality personality) =>
        a == b || a == "friends" || b == "friends" ? ERelationBehaviour.Friendly : ERelationBehaviour.Hostile;
    public int GetRelationValue(string a, string b) => a == b ? 100 : 50;
    [MethodImpl(MethodImplOptions.NoInlining)] public long FindFactionLayer(string faction) => faction == "enemy" ? 1 : 2;
}
public class CombatManager : UnityEngine.Object
{
    public static CombatManager Instance { get; } = new();
    public bool PeaceMode;
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool ContainsAttackableFaction(long layers, string faction) => faction == "enemy" || layers == -1;
}
public partial class UnitAvatar : CombatBehaviour
{
    public uint netId;
    public string faction = "players";
    public string Name { get; set; } = "Unit";
    public bool IsDead, IsInvulnerable, isForcedChaosDamage;
    public bool IsInBattle { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public BoolFlag canBeTarget = new();
    public bool IsGuarding;
    public sbyte parryInvincibleApplied;
    public bool IsParrying { get => parryInvincibleApplied > 0; set => parryInvincibleApplied = (sbyte)(value ? 1 : 0); }
    public Timer parrySuccessBonusTimer = new(0.35f);
    public Action<DamageInstance> OnParry;
    public Action<DamageInstance> OnGuardSucceeded;
    public int GuardHits;
    public EMonsterType monsterType;
    public EPersonality attackableTargetSelector;
    public UnitAvatar NetworkLeader { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public Mirror.SyncList<UnitAvatar> followers = new();
    public float Hp = 100;
    public float StunRemaining;
    public bool StartStun(float seconds) { StunRemaining = seconds; return true; }
    public float MaxHp { get; set; } = 100;
    public float Shield { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public float TrueDamage, Defense;
    public int AllDamageBonus;
    public float ObservedAmplificationDamage;
    public int mp = 100;
    public bool IsMpShield;
    public bool SuperArmor, ClampHpDamage;
    public Action<DamageInstance> OnCalculateDamage;
    public Action<UnitAvatar, DamageInstance> OnAttackUnitBeforeOperation;
    public Action<UnitAvatar, UnitAvatar, DamageInstance> OnAttackUnitBeforeOperation_AsLeader;
    public Action<DamageInstance> OnDeath;
    public bool ExtraLife;
    public int Hits;
    public Action<UnitAvatar, DamageInstance> OnHit;
    public bool DebuffImmune;
    public int DebuffApplications;
    [MethodImpl(MethodImplOptions.NoInlining)] public long GetHostileFactionLayers(EDamageFromType type) => faction == "enemy" ? -1 : 1;
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void ApplyDebuff(CharacterDebuff debuffPrefab, UnitAvatar caster)
    {
        if (debuffPrefab == null || DebuffImmune || !caster) return;
        DebuffApplications++;
        debuffPrefab.InitializeAndSpawn(caster, this);
        debuffPrefab.OnApply?.Invoke(caster, this);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public int GetCustomStatUnsafe(string key) => key switch
    {
        "PHYSICALDAMAGE" => ConvertedElementForTest(0),
        "FIREDAMAGE" => ConvertedElementForTest(1),
        "ICEDAMAGE" => ConvertedElementForTest(2),
        "LIGHTNINGDAMAGE" => ConvertedElementForTest(3),
        "MPSHIELD" => IsMpShield ? 1 : 0,
        "IGNOREEVASION" => IgnoreEvasion,
        "ABSOLUTEEVASION" => AbsoluteEvasion,
        "INFINITYMP" => InfiniteMp ? 1 : 0,
        _ => 0
    };
    [MethodImpl(MethodImplOptions.NoInlining)] public override EApplyDamageResult ApplyDamage(DamageInstance damage)
    {
        if (!Mirror.NetworkServer.active || IsDead || IsInvulnerable) return EApplyDamageResult.Fail_Absolute;
        EApplyDamageResult result = EApplyDamageResult.Success;
        var attacker = damage.origin as UnitAvatar;
        if (attacker)
        {
            if (result == EApplyDamageResult.Success && NetworkLeader == attacker) result = EApplyDamageResult.Fail_Absolute;
            if (result == EApplyDamageResult.Success && followers.Contains(attacker)) result = EApplyDamageResult.Fail_Absolute;
            if (result == EApplyDamageResult.Success && monsterType != EMonsterType.Dummy && !CombatManager.ContainsAttackableFaction(damage.targetFactionLayers,faction)) result = EApplyDamageResult.Fail_Absolute;
            if (result == EApplyDamageResult.Success && attacker.isForcedChaosDamage) damage.damage += 0;
        }
        if (result == EApplyDamageResult.Success)
        {
            attacker?.OnAttackUnitBeforeOperation?.Invoke(this, damage);
            if (attacker && attacker.NetworkLeader)
                attacker.NetworkLeader.OnAttackUnitBeforeOperation_AsLeader?.Invoke(attacker, this, damage);
            // Native parry and guard callbacks run before ordinary damage/veto.
            if (isCounterInvincibleApplied > 0) { OnCounter?.Invoke(damage); return EApplyDamageResult.Fail_Block; }
            if (parryInvincibleApplied > 0) { OnParry?.Invoke(damage); parrySuccessBonusTimer.SetTimer(0f); return EApplyDamageResult.Fail_Block; }
            if (isHitInvincibleEnabled) return EApplyDamageResult.Fail_Absolute;
            if (IsGuarding)
            {
                float guardCost = 10f;
                guardCost -= guardCost * GetCustomStat(ECustomStat.GuardResist) / 100f;
                if (guardCost < 0) guardCost = 0;
                if (PerfectGuard) guardCost *= 0.5f;
                GuardHits++; damage.failed = EDamageFailType.Block;
                OnGuardSucceeded?.Invoke(damage);
                if (OnCalculateDurabilityDamage != null)
                    foreach (CalculateDurabilityDamage callback in OnCalculateDurabilityDamage.GetInvocationList())
                        guardCost = callback(guardCost);
                UseMp((int)guardCost);
                if (GetCustomStatUnsafe("INFINITYMP") <= 0 && Mp <= 0) { GuardBroken = true; IsGuarding = false; }
                return EApplyDamageResult.Fail_Block;
            }
            OnCalculateDamage?.Invoke(damage);
            if (damage.failed != EDamageFailType.None && damage.damageType != EDamageType.ElementalEffectDamage)
                return damage.failed == EDamageFailType.Deny ? EApplyDamageResult.Fail_Absolute : EApplyDamageResult.Fail_Block;
            // Native stat formula, attacker reduction and separate guaranteed
            // dodge paths. Deterministic RNG exercises the real transpiler.
            if (damage.damageType != EDamageType.ElementalEffectDamage && damage.damageType != EDamageType.Fall)
            {
                float evasion = GetCustomStat(ECustomStat.Evasion);
                if (evasion > 10000f) evasion = 10000f;
                float chance = 100f * UnityEngine.Mathf.Log(evasion / 6200f + 1f) * 0.8f;
                if (chance < 0f) chance = 0f;
                if (attacker)
                {
                    int ignore = attacker.GetCustomStatUnsafe("IGNOREEVASION");
                    if (ignore > 0) chance -= chance * ((float)ignore / 100f);
                }
                bool evaded = chance >= UnityEngine.Random.Range(0f, 100f);
                if (!evaded && damage.absoluteEvasionPercent > 0f && damage.absoluteEvasionPercent >= UnityEngine.Random.Range(0f, 100f))
                    evaded = true;
                bool usedAbsolute = false;
                if (!evaded && GetCustomStatUnsafe("ABSOLUTEEVASION") > 0) { evaded = true; usedAbsolute = true; }
                if (evaded && !EvasionDisabled && !(IsMpShield && Mp > 0 && Shield <= 0))
                {
                    if (usedAbsolute) damage.AddFlag("USE_ABSOLUTEEVASION_STAT");
                    OnEvade?.Invoke(damage);
                    return EApplyDamageResult.Fail_Block;
                }
            }
            float resolved = damage.damage - Defense + TrueDamage;
            if (attacker)
            {
                int bonus = attacker.GetCustomStat(ECustomStat.AllDamageBonus);
                if (attacker.NetworkLeader) bonus += attacker.NetworkLeader.GetCustomStat(ECustomStat.AllDamageBonus);
                float amplified = damage.damage * ((float)bonus / 100f);
                ObservedAmplificationDamage = amplified;
                resolved += amplified;
            }
            float shieldDamage = 0, mpDamage = 0;
            if (resolved > 0)
            {
            resolved -= GetCustomStatUnsafe("TOUGHNESS");
            if (resolved < 1) resolved = 1;
            if (Shield > 0) { float beforeShield = Shield; resolved = UseShield(resolved); shieldDamage = beforeShield - Shield; }
            }
            if (GetCustomStatUnsafe("MPSHIELD") > 0)
            {
                int oldMp = Mp;
                if ((float)Mp < resolved) { resolved -= Mp; UseMp(mp); }
                else { UseMp((int)resolved); resolved = 0; }
                mpDamage = oldMp - Mp;
            }
            if (shieldDamage > 0 && !damage.isSystemDamage) AddReceivedDamage(shieldDamage);
            if (mpDamage > 0 && !damage.isSystemDamage) AddReceivedDamage(mpDamage);
            if (SuperArmor) resolved *= 0.65f;
            if (ClampHpDamage && resolved > Hp) resolved = Hp;
            Hp -= resolved; Hits++;
            if (resolved > 0 && !damage.isSystemDamage) AddReceivedDamage(resolved);
            OnHit?.Invoke(this,damage);
            // Representative native Harvest block: actual HP damage, optional
            // per-hit HP rate, then native recovery (before death callbacks).
            if (resolved > 0 && attacker)
            {
                if (monsterType != EMonsterType.Dummy)
                {
                    float hpHarvest = 0;
                    int hpRate = attacker.GetCustomStat(ECustomStat.HPSteal);
                    if (hpRate > 0) hpHarvest += resolved * (hpRate / 1000f);
                    if (damage.hpSteal > 0) hpHarvest += resolved * (damage.hpSteal / 1000f);
                    if (hpHarvest > 0)
                    {
                        if (HarvestReceivedIncrease > 0) hpHarvest /= 1 + HarvestReceivedIncrease / 100f;
                        attacker.Heal(hpHarvest);
                    }
                }
                int mpRate = attacker.GetCustomStat(ECustomStat.MPSteal);
                if (mpRate > 0)
                {
                    float mpHarvest = resolved * (mpRate / 1000f);
                    if (HarvestReceivedIncrease > 0) mpHarvest /= 1 + HarvestReceivedIncrease / 100f;
                    attacker.HealMpFloat(mpHarvest);
                }
            }
            if (Hp <= 0) { if (ExtraLife) { Hp = 60; ExtraLife = false; } else Die(5, damage); }
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void AddReceivedDamage(float damage) { }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Revive(float hpAmount) { IsDead = false; Hp = hpAmount; }
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void Die(int hitLevel, DamageInstance diedFrom)
    {
        if (IsDead) return;
        IsDead = true; NetworkLeader = null; OnDeath?.Invoke(diedFrom);
    }
}
public class PlayerAvatar : UnitAvatar
{
    private WeaponControllerSimple weaponController = new();
    public WeaponControllerSimple CounterController { get => weaponController; set => weaponController = value; }
    public string currentFloorGuid = "floor-1";
    public string playerNameSource = "";
    public PlayerSpawner spawner;
    public bool safeMode;
    public PlayerAvatar() { OnAttackUnitBeforeOperation += HandleBeforeAttack; weaponController.unitAvatar = this; }
    [MethodImpl(MethodImplOptions.NoInlining)] private void HandleBeforeAttack(UnitAvatar target, DamageInstance damage)
    {
        if (RuntimeFactionManager.Instance.GetRelationBehaviour(target.faction, faction, target.attackableTargetSelector) == ERelationBehaviour.Hostile) return;
        if (RuntimeFactionManager.Instance.GetRelationValue(target.faction, faction) >= 80) damage.failed = EDamageFailType.Deny;
        else if (target.monsterType != EMonsterType.Dummy)
        {
            if (safeMode) damage.failed = EDamageFailType.Deny;
            else if (!DungeonManager.Instance.BreakShieldOfReason(target)) damage.failed = EDamageFailType.Block;
        }
    }
    public void InvokeBeforeAttack(UnitAvatar target, DamageInstance damage) => HandleBeforeAttack(target, damage);
}
public class PlayerSpawner : UnityEngine.Object
{
    public static List<PlayerSpawner> MultiplayerList { get; } = new();
    public ulong steamID;
    public PlayerAvatar PlayerAvatar;
    public int currentPlayerIdx = -1;
    public bool AllDead = true;
    public int GameOvers;
    public void BindDeath(PlayerAvatar player) { PlayerAvatar = player; player.spawner = this; player.OnDeath += HandleDieServerside; }
    [MethodImpl(MethodImplOptions.NoInlining)] private void HandleDieServerside(DamageInstance damage)
    { if (PlayerAvatar.IsDead && AllDead) RpcGameOver(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void RpcGameOver() { GameOvers++; }
}
public partial class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance { get; } = new();
    public bool isServer = true;
    public List<string> Messages = new();
    public int ReasonChecks;
    public bool ReasonShieldAllows = true;
    public bool BreakShieldOfReason(UnitAvatar target) { ReasonChecks++; return ReasonShieldAllows; }
    public void Chat(PlayerAvatar avatar, string name, string message) => Messages.Add(message);
}

// Compact native relation and decision fixture; native IL checks verify the
// installed game's search/update consumers and guard placement separately.
public partial class UnitAI_NewBasic : UnityEngine.Object
{
    public UnitAvatar Avatar { get; set; }
    public UnitAvatar CurrentTarget { get; set; }
    private ERelationBehaviour behaviourInPrevFrame;
    private bool targetFound;
    private bool targetSearched;
    private bool isInBattleActiveByAI;
    public int LostTargets;
    [MethodImpl(MethodImplOptions.NoInlining)] public ERelationBehaviour GetRelation(UnitAvatar target)
    {
        if (!target) return ERelationBehaviour.Neutral;
        if (Avatar.NetworkLeader)
            return RuntimeFactionManager.Instance.GetRelationBehaviour(target.faction, Avatar.NetworkLeader.faction, target.attackableTargetSelector);
        return RuntimeFactionManager.Instance.GetRelationBehaviour(Avatar.faction, target.faction, Avatar.attackableTargetSelector);
    }
    public bool ShouldAttack => CurrentTarget && !CurrentTarget.IsDead && !CurrentTarget.IsInvulnerable && GetRelation(CurrentTarget) == ERelationBehaviour.Hostile;
    public void SetTarget(UnitAvatar target)
    {
        if (targetFound) { OnLostTarget(); targetFound = false; }
        CurrentTarget = target;
        if (CurrentTarget) targetFound = true;
    }
    protected virtual void OnLostTarget() { LostTargets++; }
    protected virtual void OnAIUpdate_FoundEnemy() { }
    protected virtual void OnAIUpdate_FollowLeader(UnitAvatar leader) { }
    public void SearchForFixture(UnitAvatar target) { targetSearched = true; SetTarget(target); }
    [MethodImpl(MethodImplOptions.NoInlining)] protected virtual void OnAIUpdate()
    {
        var relation = CurrentTarget ? GetRelation(CurrentTarget) : ERelationBehaviour.Friendly;
        if (targetSearched) behaviourInPrevFrame = relation;
        if (relation == ERelationBehaviour.Hostile)
        {
            if (!isInBattleActiveByAI) isInBattleActiveByAI = true;
            OnAIUpdate_FoundEnemy();
        }
        else
        {
            if (isInBattleActiveByAI) isInBattleActiveByAI = false;
            if (Avatar.NetworkLeader) OnAIUpdate_FollowLeader(Avatar.NetworkLeader);
        }
    }
    public void Tick() => OnAIUpdate();
}

// Native archers release held weapon input on target loss, not on following.
public sealed class ArcherFixture : UnitAI_NewBasic
{
    public bool TriggerHeld;
    protected override void OnAIUpdate_FoundEnemy() { TriggerHeld = true; }
    protected override void OnLostTarget() { base.OnLostTarget(); TriggerHeld = false; }
}
