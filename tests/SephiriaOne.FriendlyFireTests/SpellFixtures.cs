using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

// Native-shaped bodies for executing the audited forward/reverse filter,
// coroutine and cached-target rewrites. Installed IL is checked separately.
public class ActiveSkill : UnityEngine.Object
{
    public UnitAvatar NetworkCaster, Candidate;
    public Vector2 CastTargetPos;
    public bool Selected;
}
public class ActiveSkill_Ball : ActiveSkill
{
    public UnitAvatar Target;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() =>
        Target = PlayerInputController.SearchTargetNearestPoint(NetworkCaster, CastTargetPos, 5);
}
public class ActiveSkill_FireBulletsToEnemies : ActiveSkill
{
    public Collider2D Collider;
    public IEnumerator FireCoroutine()
    {
        yield return null;
        var target = Collider != null ? Collider.GetComponent<UnitAvatar>() : Candidate;
        Selected = target && !target.IsDead && CombatManager.ContainsAttackableFaction(NetworkCaster.GetHostileFactionLayers(EDamageFromType.Magic), target.faction);
    }
}

namespace UnityEngine
{
    public class Component : Object
    {
        public UnitAvatar Avatar;
        public Hitbox Hitbox;
        [MethodImpl(MethodImplOptions.NoInlining)] public T GetComponent<T>() where T : Object =>
            typeof(T) == typeof(UnitAvatar) ? Avatar as T : Hitbox as T;
    }
    public class Collider2D : Component { }
}
public class Hitbox : UnityEngine.Object
{
    public CombatBehaviour Target;
    public CombatBehaviour GetCombatBehaviour(int floor) => Target;
}
public class ActiveSkill_MeteorShower : ActiveSkill
{
    public IEnumerator CreateMeteorCoroutine()
    {
        yield return null;
        Selected = Candidate && !Candidate.IsDead && CombatManager.ContainsAttackableFaction(NetworkCaster.GetHostileFactionLayers(EDamageFromType.Magic), Candidate.faction);
    }
}
public class AutoMagicCaster
{
    public UnitAvatar owner, Candidate, aimedEnemy;
    public bool Selected;
    [MethodImpl(MethodImplOptions.NoInlining)] public UnitAvatar SearchTarget() =>
        Candidate && Candidate != owner && !Candidate.IsDead && CombatManager.ContainsAttackableFaction(owner.GetHostileFactionLayers(EDamageFromType.None), Candidate.faction) ? Candidate : null;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Update() => Selected = aimedEnemy && !aimedEnemy.IsDead;
}
public partial class DungeonManager
{
    public UnitAvatar Candidate;
    [MethodImpl(MethodImplOptions.NoInlining)] public UnitAvatar SearchTargetForChainLightning(List<UnitAvatar> alreadyHitList, UnitAvatar owner, Vector3 fromPosition, UnitAvatar hitTarget) =>
        Candidate && !alreadyHitList.Contains(Candidate) && !Candidate.IsDead && Candidate != hitTarget && !Candidate.canBeTarget.IsFalse() &&
        CombatManager.ContainsAttackableFaction(Candidate.GetHostileFactionLayers(EDamageFromType.None), owner.faction) ? Candidate : null;
}
