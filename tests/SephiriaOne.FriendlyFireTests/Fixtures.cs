using System.Runtime.CompilerServices;
namespace UnityEngine
{
    public class Object
    {
        public static implicit operator bool(Object obj) => obj is not null;
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
    internal static class SessionSettings { internal static FriendlyFireSettings FriendlyFireForHit; }
}
public enum EApplyDamageResult { Success, Fail_Absolute }
public enum EMonsterType { Normal, Dummy }
public enum EPersonality { Aggressive }
public enum ERelationBehaviour { Friendly, Hostile }
public class CombatBehaviour : UnityEngine.Object { }
public class DamageInstance
{
    public CombatBehaviour origin;
    public long targetFactionLayers;
    public bool isSystemDamage;
    public float damage = 10;
}
public class RuntimeFactionManager : UnityEngine.Object
{
    public static RuntimeFactionManager Instance = new();
    public ERelationBehaviour GetRelationBehaviour(string a, string b, EPersonality personality) =>
        a == b || a == "friends" || b == "friends" ? ERelationBehaviour.Friendly : ERelationBehaviour.Hostile;
}
public static class CombatManager
{
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool ContainsAttackableFaction(long layers, string faction) => faction == "enemy" || layers == -1;
}
public class UnitAvatar : CombatBehaviour
{
    public string faction = "players";
    public string Name { get; set; } = "Unit";
    public bool IsDead, IsInvulnerable, isForcedChaosDamage;
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
    public Action<DamageInstance> OnDeath;
    public bool Revive;
    public int Hits;
    public Action<UnitAvatar, DamageInstance> OnHit;
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
            OnCalculateDamage?.Invoke(damage);
            float resolved = damage.damage - Defense + TrueDamage;
            if (resolved > 0)
            {
            resolved -= GetCustomStatUnsafe("TOUGHNESS");
            if (resolved < 1) resolved = 1;
            if (Shield > 0) { float absorbed = Math.Min(resolved,Shield); Shield -= absorbed; resolved -= absorbed; }
            }
            if (GetCustomStatUnsafe("MPSHIELD") > 0)
            {
                if ((float)Mp < resolved) { resolved -= Mp; Mp = 0; }
                else { Mp -= (int)resolved; resolved = 0; }
            }
            Hp -= resolved; Hits++;
            OnHit?.Invoke(this,damage);
            if (Hp <= 0) { if (Revive) { Hp = 60; Revive = false; } else Die(5, damage); }
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void Die(int hitLevel, DamageInstance diedFrom)
    {
        if (IsDead) return;
        IsDead = true; NetworkLeader = null; OnDeath?.Invoke(diedFrom);
    }
}
public class PlayerAvatar : UnitAvatar { }
public class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance = new();
    public bool isServer = true;
    public List<string> Messages = new();
    public void Chat(PlayerAvatar avatar, string name, string message) => Messages.Add(message);
}
