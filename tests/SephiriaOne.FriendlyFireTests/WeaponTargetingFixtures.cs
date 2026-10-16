using System.Runtime.CompilerServices;
using UnityEngine;

// Minimal native-shaped consumers; installed-game IL/contracts are tested too.
public class WeaponSimple_Katana
{
    public UnitAvatar Owner, Target;
    public bool useMagicBlade = true;
    [MethodImpl(MethodImplOptions.NoInlining)] public void OnCreateMagicServerside(ActiveSkill skill)
    {
        Target = useMagicBlade ? PlayerInputController.SearchTargetNearestPoint(Owner, Owner.transform.position, 5) : null;
    }
}
public class WeaponSimple_SwordAndShield
{
    public UnitAvatar Owner, Target;
    public bool Ready = true;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() =>
        Target = Ready ? PlayerInputController.SearchTargetNearestPoint(Owner, Owner.transform.position, 5) : null;
}
public class WeaponAddonKatana_Nastrond
{
    public UnitAvatar Candidate;
    [MethodImpl(MethodImplOptions.NoInlining)] public UnitAvatar SearchTarget(UnitAvatar avatar) =>
        Candidate && !Candidate.IsDead && Candidate != avatar && !Candidate.canBeTarget.IsFalse() &&
        CombatManager.ContainsAttackableFaction(Candidate.GetHostileFactionLayers(EDamageFromType.None), avatar.faction) ? Candidate : null;
}
public class BulletMoveModule
{
    public Bullet Bullet { get; set; }
    public UnitAvatar Candidate;
    public bool Ready = true, Triggered;
    public float RangeSquared = 4;
    public virtual int OnMove() => 0;
}
public class BulletMoveModule_CrossbowMine : BulletMoveModule
{
    public long hostileLayers = 2;
    [MethodImpl(MethodImplOptions.NoInlining)] public override int OnMove()
    {
        var target = Candidate;
        Triggered = Ready && target && !target.IsDead && !target.canBeTarget.IsFalse() && target != Bullet.NetworkOwner &&
            CombatManager.ContainsAttackableFaction(hostileLayers, target.faction) &&
            ((Vector2)target.transform.position).sqrMagnitude < RangeSquared;
        return 0;
    }
}
public class BulletMoveModule_CrossbowMineHoming : BulletMoveModule
{
    public long hostileLayers = 2;
    [MethodImpl(MethodImplOptions.NoInlining)] public override int OnMove()
    {
        var target = Candidate;
        Triggered = Ready && target && !target.IsDead && !target.canBeTarget.IsFalse() && target != Bullet.NetworkOwner &&
            CombatManager.ContainsAttackableFaction(hostileLayers, target.faction) &&
            ((Vector2)target.transform.position).sqrMagnitude < RangeSquared;
        return 0;
    }
}
