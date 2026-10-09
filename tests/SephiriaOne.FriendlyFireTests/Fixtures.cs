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
}
namespace Mirror { public static class NetworkServer { public static bool active = true; } public static class NetworkClient { public static bool active = true; } }
namespace Mirror { public class SyncList<T> : List<T> { [MethodImpl(MethodImplOptions.NoInlining)] public new bool Contains(T item) => base.Contains(item); } }
namespace SephiriaOne
{
    internal static class DeathmatchRuntime
    {
        internal static bool Protect;
        internal static PlayerAvatar LastDead, LastRevived;
        internal static bool ProtectDeath(PlayerAvatar player) => Protect && player != null;
        internal static void Died(PlayerAvatar player) => LastDead = player;
        internal static void Reviving(PlayerAvatar player) => LastRevived = player;
        internal static bool EnterDeath(PlayerAvatar player) => false;
        internal static void LeaveDeath(PlayerAvatar player, bool entered) { }
    }
    internal static class ReviveAllFeature { internal static bool Available = true; }
    internal static class SessionSettings
    {
        private static FriendlyFireSettings settings;
        internal static FriendlyFireSettings FriendlyFireForHit
        { get => settings; set { settings = value; FriendlyFireKda.SetEnabled(value.Enabled); } }
    }
}
public enum EApplyDamageResult { Success, Fail_Absolute, Fail_Block }
public enum EDamageFailType { None, Deny, Block }
public enum EDamageFromType { None, BasicAttack, Magic }
public enum EDamageType { Slice, Projectile, ElementalEffectDamage }
public enum EMonsterType { Normal, Dummy }
public enum EPersonality { Aggressive }
public enum ERelationBehaviour { Neutral, Friendly, Hostile }
public class CombatBehaviour : UnityEngine.Object { }
public class DamageInstance
{
    public CombatBehaviour origin;
    public long targetFactionLayers;
    public bool isSystemDamage;
    public EDamageFailType failed;
    public float damage = 10;
    public string id = "";
    public EDamageFromType fromType;
    public EDamageType damageType;
}
public class RuntimeFactionManager : UnityEngine.Object
{
    public static RuntimeFactionManager Instance { get; } = new();
    public ERelationBehaviour GetRelationBehaviour(string a, string b, EPersonality personality) =>
        a == b || a == "friends" || b == "friends" ? ERelationBehaviour.Friendly : ERelationBehaviour.Hostile;
    public int GetRelationValue(string a, string b) => a == b ? 100 : 50;
}
public static class CombatManager
{
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool ContainsAttackableFaction(long layers, string faction) => faction == "enemy" || layers == -1;
}
public class UnitAvatar : CombatBehaviour
{
    public uint netId;
    public string faction = "players";
    public string Name { get; set; } = "Unit";
    public bool IsDead, IsInvulnerable, isForcedChaosDamage;
    public bool IsGuarding;
    public bool IsParrying;
    public Action<DamageInstance> OnParry;
    public Action<DamageInstance> OnGuardSucceeded;
    public int GuardHits;
    public EMonsterType monsterType;
    public EPersonality attackableTargetSelector;
    public UnitAvatar NetworkLeader { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public Mirror.SyncList<UnitAvatar> followers = new();
    public float Hp = 100;
    public float Shield { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public float TrueDamage, Defense;
    public int Mp = 100;
    public bool IsMpShield;
    public Action<DamageInstance> OnCalculateDamage;
    public Action<UnitAvatar, DamageInstance> OnAttackUnitBeforeOperation;
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
    [MethodImpl(MethodImplOptions.NoInlining)] private float GetCustomStatUnsafe(string key) => key == "MPSHIELD" && IsMpShield ? 1 : 0;
    [MethodImpl(MethodImplOptions.NoInlining)] public EApplyDamageResult ApplyDamage(DamageInstance damage)
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
            // Native parry and guard callbacks run before ordinary damage/veto.
            if (IsParrying) { OnParry?.Invoke(damage); return EApplyDamageResult.Fail_Block; }
            if (IsGuarding)
            {
                GuardHits++; damage.failed = EDamageFailType.Block;
                OnGuardSucceeded?.Invoke(damage); Mp -= 10;
                return EApplyDamageResult.Fail_Block;
            }
            OnCalculateDamage?.Invoke(damage);
            if (damage.failed != EDamageFailType.None && damage.damageType != EDamageType.ElementalEffectDamage)
                return damage.failed == EDamageFailType.Deny ? EApplyDamageResult.Fail_Absolute : EApplyDamageResult.Fail_Block;
            float resolved = damage.damage - Defense + TrueDamage;
            float shieldDamage = 0, mpDamage = 0;
            if (resolved > 0)
            {
            resolved -= GetCustomStatUnsafe("TOUGHNESS");
            if (resolved < 1) resolved = 1;
            if (Shield > 0) { float absorbed = Math.Min(resolved,Shield); Shield -= absorbed; resolved -= absorbed; shieldDamage = absorbed; }
            }
            if (GetCustomStatUnsafe("MPSHIELD") > 0)
            {
                int oldMp = Mp;
                if ((float)Mp < resolved) { resolved -= Mp; Mp = 0; }
                else { Mp -= (int)resolved; resolved = 0; }
                mpDamage = oldMp - Mp;
            }
            if (shieldDamage > 0 && !damage.isSystemDamage) AddReceivedDamage(shieldDamage);
            if (mpDamage > 0 && !damage.isSystemDamage) AddReceivedDamage(mpDamage);
            Hp -= resolved; Hits++;
            if (resolved > 0 && !damage.isSystemDamage) AddReceivedDamage(resolved);
            OnHit?.Invoke(this,damage);
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
    public string playerNameSource = "";
    public PlayerSpawner spawner;
    public bool safeMode;
    public PlayerAvatar() { OnAttackUnitBeforeOperation += HandleBeforeAttack; }
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
public class DungeonManager : UnityEngine.Object
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
public class UnitAI_NewBasic
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
