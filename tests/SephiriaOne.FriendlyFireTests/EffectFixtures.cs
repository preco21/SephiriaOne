using System.Collections;
using System.Runtime.CompilerServices;

public class CharacterDebuff : UnityEngine.Object
{
    public Action<UnitAvatar, UnitAvatar> OnApply;
    public UnitAvatar NetworkAttacker, NetworkTarget;
    public bool IsEndBuff;
    public Action OnTick, OnExpire;
    [MethodImpl(MethodImplOptions.NoInlining)] public void InitializeAndSpawn(UnitAvatar attacker, UnitAvatar target, float amplified = 1)
    { NetworkAttacker = attacker; NetworkTarget = target; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update()
    { OnTick?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public void Destroy()
    { IsEndBuff = true; OnExpire?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)] public virtual void AddStack() { OnTick?.Invoke(); }
}
public class Charm_Basic
{
    public UnitAvatar NetworkAvatar { get; set; }
    public UnitAvatar Target;
    public bool Selected;
    public bool Activated;
    public bool Ready = true;
    public DamageInstance Damage;
}
public class WeaponAddonCommon_BurnRing : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void DamageNearbyEnemies() =>
        Selected = Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_FireFeather : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_FlameGround_Meteor : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate() =>
        Activated = NetworkAvatar && !NetworkAvatar.IsDead && !CombatManager.Instance.PeaceMode && NetworkAvatar.IsInBattle && Ready;
}
public class ComboEffect_DarkCloud : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() =>
        Activated = NetworkAvatar && !NetworkAvatar.IsDead && !CombatManager.Instance.PeaceMode && NetworkAvatar.IsInBattle && Ready;
    [MethodImpl(MethodImplOptions.NoInlining)] public void FireLightning() => ((CombatBehaviour)Target).ApplyDamage(Damage);
    public IEnumerator UseCloudCoroutine()
    {
        yield return null;
        Selected = Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    }
}

// Each method keeps its native faction/battle call visible to the production
// Harmony transpiler. The remaining gates model native item prerequisites.
public class Charm_IceBat : Charm_Basic
{
    public bool HasOwnedFrostbite = true;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate() =>
        Selected = NetworkAvatar && !NetworkAvatar.IsDead && !CombatManager.Instance.PeaceMode && NetworkAvatar.IsInBattle && Ready &&
            Target && !Target.IsDead && HasOwnedFrostbite &&
            CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_ElectricEarring : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate() =>
        Activated = NetworkAvatar && !NetworkAvatar.IsDead && !CombatManager.Instance.PeaceMode && NetworkAvatar.IsInBattle && Ready;
}
public class Charm_AttackChim : Charm_Basic
{
    public CharacterDebuff Debuff;
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void HandleAddedDebuffOnTarget() => Target.ApplyDebuff(Debuff, NetworkAvatar);
}
public class Charm_EchoOfTheGlacier : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void CreateFrostbite() => ((CombatBehaviour)Target).ApplyDamage(Damage);
}
public class Charm_GrowthParry : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchNearestTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_Guillotine : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool FindTargetsInRange() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_IceHammer : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
}
public class Charm_IceSpear : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTargetByWeaponDirection() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate() =>
        Activated = NetworkAvatar && !NetworkAvatar.IsDead && !CombatManager.Instance.PeaceMode && NetworkAvatar.IsInBattle && Ready;
}
public class GreenBat : Charm_Basic
{
    public UnitAvatar NetworkOwner { get => NetworkAvatar; set => NetworkAvatar = value; }
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkOwner.faction);
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() =>
        Activated = NetworkOwner && !NetworkOwner.IsDead && !CombatManager.Instance.PeaceMode && NetworkOwner.IsInBattle && Ready;
}
public class DaggerGrowthBullet : CombatBehaviour
{
    public UnitAvatar NetworkOwner, Target;
    public bool Selected;
    [MethodImpl(MethodImplOptions.NoInlining)] public void HitCheck() =>
        Selected = Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(NetworkOwner.GetHostileFactionLayers(EDamageFromType.DirectAttack), Target.faction);
}
public class Bullet : CombatBehaviour
{
    public UnitAvatar NetworkOwner;
    public EDamageFromType FromType;
    public string damageId;
    public UnitAvatar Candidate;
    public UnitAvatar HomingTarget { [MethodImpl(MethodImplOptions.NoInlining)] get; private set; }
    public void SetHomingTarget(UnitAvatar value) => HomingTarget = value;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update()
    {
        if (HomingTarget && HomingTarget.IsDead) HomingTarget = null;
        if (!HomingTarget && Candidate && CombatManager.ContainsAttackableFaction(NetworkOwner.GetHostileFactionLayers(EDamageFromType.None), Candidate.faction))
            HomingTarget = Candidate;
    }
}
public static class PlayerInputController
{
    public static List<UnitAvatar> Candidates { get; } = new();
    [MethodImpl(MethodImplOptions.NoInlining)] public static UnitAvatar SearchTargetNearestPoint(UnitAvatar avatar, UnityEngine.Vector2 point, float squaredRadius)
    {
        UnitAvatar result = null;
        foreach (var candidate in Candidates)
        {
            if (!candidate || candidate == avatar || !candidate.canBeTarget.IsTrue() || candidate.IsDead) continue;
            if ((avatar.GetHostileFactionLayers(EDamageFromType.None) & RuntimeFactionManager.Instance.FindFactionLayer(candidate.faction)) == 0) continue;
            float distance = ((UnityEngine.Vector2)candidate.transform.position - point).sqrMagnitude;
            if (distance > squaredRadius) continue;
            if (!result || distance < ((UnityEngine.Vector2)result.transform.position - point).sqrMagnitude) result = candidate;
        }
        return result;
    }
}
public class Charm_GuardCounter : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void FireRipostelaser() =>
        Target = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
}
public class Charm_RockElephant : Charm_Basic
{
    public UnitAvatar SecondTarget;
    [MethodImpl(MethodImplOptions.NoInlining)] public void SpawnFlag()
    {
        Target = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
        SecondTarget = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
    }
}
public class Charm_IceBow : Charm_Basic
{
    public UnitAvatar SecondTarget;
    [MethodImpl(MethodImplOptions.NoInlining)] public void FireCastingServer()
    {
        Target = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
        SecondTarget = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
    }
    public IEnumerator FireCoroutine()
    {
        yield return null;
        Target = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
        SecondTarget = PlayerInputController.SearchTargetNearestPoint(NetworkAvatar, NetworkAvatar.transform.position, 10);
    }
}
public class Charm_FrostiumRing : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void HandleAttackUnit() => ((CombatBehaviour)Target).ApplyDamage(Damage);
}
public class Charm_TheTyphoonSheetmusic : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void HandleAttackUnit() => ((CombatBehaviour)Target).ApplyDamage(Damage);
}
public class Charm_Reddew : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void SummonDueFromVictim() => ((CombatBehaviour)Target).ApplyDamage(Damage);
}
public class Charm_TuningForks : Charm_Basic
{
    [MethodImpl(MethodImplOptions.NoInlining)] public void CreateAttack() => ((CombatBehaviour)Target).ApplyDamage(Damage);
}
public class Charm_ThunderousSteps : Charm_Basic
{
    public IEnumerator CreateAttack()
    {
        yield return null;
        Selected = Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    }
}
public class Charm_FireChakram : Charm_Basic
{
    public long FactionMask = 2;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnUpdate()
    {
        long ownFaction = FactionMask;
        if (ownFaction == 0) return;
        Selected = Target && !Target.IsDead && (Target.GetHostileFactionLayers(EDamageFromType.None) & ownFaction) != 0;
    }
}
