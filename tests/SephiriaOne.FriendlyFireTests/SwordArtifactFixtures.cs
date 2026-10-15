using System.Runtime.CompilerServices;

public class Charm_IceSword : Charm_Basic { }

// Preserve the native inline mask/source stack shape. Physics/RPCs are modeled
// here; the installed-game contracts verify the real collision and FX methods.
public class IceSword
{
    private readonly Charm_IceSword parentCharm;
    public CombatBehaviour Target;
    public bool IsServerObject = true, EffectEnabled = true, Overlapping = true;
    public float Now, LastHit = -100;
    public int HitEffects;
    public IceSword(Charm_IceSword parent) => parentCharm = parent;

    [MethodImpl(MethodImplOptions.NoInlining)] public void Update()
    {
        if (!parentCharm.NetworkAvatar || parentCharm.NetworkAvatar.IsDead || !EffectEnabled || !IsServerObject ||
            !Overlapping || !Target || Now - LastHit <= 0.5f) return;
        if (!(Target is UnitAvatar unitAvatar) ||
            (unitAvatar.GetHostileFactionLayers(EDamageFromType.None) & RuntimeFactionManager.Instance.FindFactionLayer(parentCharm.NetworkAvatar.faction)) != 0L)
        {
            var damage = new DamageInstance { origin = parentCharm.NetworkAvatar, id = "Charm_PlitviceWaterDrop", damage = 20,
                targetFactionLayers = parentCharm.NetworkAvatar.GetHostileFactionLayers(EDamageFromType.None), damageType = EDamageType.Projectile };
            if (Target.ApplyDamage(damage) == EApplyDamageResult.Success) HitEffects++;
            LastHit = Now;
        }
    }
}

public class ComboEffect_FlameSword : ComboEffectBase
{
    [MethodImpl(MethodImplOptions.NoInlining)] public bool SearchTarget() =>
        Target && !Target.IsDead && Target != Networkavatar && Target.canBeTarget.IsTrue() && Target.gameObject.activeSelf &&
        CombatManager.ContainsAttackableFaction(Target.GetHostileFactionLayers(EDamageFromType.None), Networkavatar.faction) &&
        (Target.transform.position - Networkavatar.transform.position).sqrMagnitude <= 25;
}
