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
}
public class ComboEffect_DarkCloud : Charm_Basic
{
    public IEnumerator UseCloudCoroutine()
    {
        yield return null;
        Selected = Target && !Target.IsDead && CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), NetworkAvatar.faction);
    }
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
