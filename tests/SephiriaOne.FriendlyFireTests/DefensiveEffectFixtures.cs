using System.Runtime.CompilerServices;

public sealed class CharacterDash
{
    public int Restored;
    public System.Action OnRestore;
    [MethodImpl(MethodImplOptions.NoInlining)] public void RestoreDashCount(int count)
    { Restored += count; OnRestore?.Invoke(); }
}
public sealed class Charm_EvasionRestore : Charm_Basic
{
    public int[] restoreDashCountByLevel = { 1, 1, 2 };
    public int Level { get => limitedEffectEnabledLevel; set => limitedEffectEnabledLevel = value; }
    [MethodImpl(MethodImplOptions.NoInlining)] private void HandleEvade(DamageInstance instance) =>
        NetworkAvatar.CurrentDashModule.RestoreDashCount(restoreDashCountByLevel.SafeRandomAccess(CurrentLevelToIdx()));
    public void Attach() => NetworkAvatar.OnEvade += HandleEvade;
    public void Detach() => NetworkAvatar.OnEvade -= HandleEvade;
    public void Invoke(DamageInstance damage) => HandleEvade(damage);
}
public static class DefensiveFixtureExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)] public static int SafeRandomAccess(this int[] values, int index, int mode = 0) => values[index];
}
public sealed class WeaponControllerSimple : UnityEngine.Object
{
    public UnitAvatar unitAvatar;
    private float furyInvincibleTimer;
    private bool isFuryInvincibleApplied;
    public float furyInvincibleTime;
    public void SetCounter(float elapsed, float duration = 0.935f, bool active = true)
    { furyInvincibleTimer = elapsed; furyInvincibleTime = duration; isFuryInvincibleApplied = active; }
    public float Elapsed => furyInvincibleTimer;
}
public partial class UnitAvatar
{
    public sbyte isCounterInvincibleApplied;
    public System.Action<DamageInstance> OnCounter;
    public CharacterDash CurrentDashModule = new();
}
